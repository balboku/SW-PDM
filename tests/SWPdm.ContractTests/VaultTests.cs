using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data.Entities;
using Xunit;

namespace SWPdm.ContractTests;

public sealed class VaultTests
{
    [Fact]
    public async Task RevisionPropertyFallbackPreservesLegacyDisplayWithoutGuessingConflicts()
    {
        await using var h = await ContractHost.Start();
        await h.WithDb(async db => {
            (await db.DocumentVersions.FindAsync(1L))!.RevisionLabel = null;
            (await db.Documents.FindAsync(1L))!.RevisionLabel = "D";
            db.CustomProperties.AddRange(new PdmCustomProperty { VersionId = 1, PropertyName = "Revision", PropertyValue = "B" },
                new PdmCustomProperty { VersionId = 1, ConfigurationName = "Alternate", PropertyName = "Revision", PropertyValue = "NOT-DOCUMENT-REV" });
            await db.SaveChangesAsync();
        });
        var first = (await Page(h, "query=CONTRACT")).GetProperty("items")[0];
        Assert.Equal("B", first.GetProperty("revisionLabel").GetString());
        await h.WithDb(async db => { db.CustomProperties.Add(new() { VersionId = 1, PropertyName = "版次", PropertyValue = "C" }); await db.SaveChangesAsync(); });
        var conflict = (await Page(h, "query=CONTRACT")).GetProperty("items")[0];
        Assert.Equal("D", conflict.GetProperty("revisionLabel").GetString());
        Assert.Equal(2, conflict.GetProperty("propertySources").GetProperty("revision").GetProperty("conflicts").GetArrayLength());
    }

    [Fact]
    public async Task CompletePagingHasGlobalCountsStableOrderAndNoWrites()
    {
        await using var h = await Fixture(); string before = await Snapshot(h);
        var a = await Page(h, "page=1"); var b = await Page(h, "page=2");
        Assert.Equal(72, a.GetProperty("totalCount").GetInt32()); Assert.Equal(2, a.GetProperty("totalPages").GetInt32());
        Assert.Equal(50, a.GetProperty("items").GetArrayLength()); Assert.Equal(22, b.GetProperty("items").GetArrayLength());
        var ids = a.GetProperty("items").EnumerateArray().Concat(b.GetProperty("items").EnumerateArray()).Select(d => d.GetProperty("documentId").GetInt64()).ToArray();
        Assert.Equal(72, ids.Distinct().Count()); Assert.Equal(Enumerable.Range(3, 50).Select(i => (long)i), ids.Take(50));
        Assert.Equal(a.GetProperty("items").ToString(), (await Page(h, "page=1")).GetProperty("items").ToString());
        Assert.Equal(2, a.GetProperty("missingReferenceDocumentCount").GetInt32());
        Assert.Equal(1, a.GetProperty("newerReferenceDocumentCount").GetInt32());
        Assert.Equal(2, a.GetProperty("checkedOutCount").GetInt32());
        Assert.False(a.GetProperty("nativeFileChecked").GetBoolean());
        Assert.Equal(before, await Snapshot(h));
    }

    [Theory]
    [InlineData("query=DWG-072", 1)]
    [InlineData("query=%E6%B8%AC%E8%A9%A6%E5%85%83%E4%BB%B672", 1)]
    [InlineData("query=CONFIG-ONLY", 1)]
    [InlineData("query=HISTORICAL-ONLY", 0)]
    [InlineData("query=%25", 1)]
    [InlineData("query=%27%20OR%201%3D1--", 0)]
    [InlineData("documentType=Drawing", 1)]
    [InlineData("status=CheckedOut", 2)]
    [InlineData("integrity=Missing", 2)]
    [InlineData("integrity=Newer", 1)]
    public async Task SearchAndFiltersApplyToAllCurrentVersions(string filter, int count)
    {
        await using var h = await Fixture(); string before = await Snapshot(h);
        var result = await Page(h, "page=2&" + filter);
        Assert.Equal(count, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, result.GetProperty("page").GetInt32()); Assert.Equal(before, await Snapshot(h));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("documentType=Invalid")]
    [InlineData("status=Invalid")]
    [InlineData("integrity=Invalid")]
    public async Task InvalidQueryIs400WithoutDataMutation(string query)
    {
        await using var h = await ContractHost.Start(); string before = await Snapshot(h);
        var result = await h.Client.GetAsync("/api/documents/search?paged=true&" + query);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode); Assert.Equal(before, await Snapshot(h));
    }

    [Fact]
    public async Task LegacyArrayAndBeyondLastPageAreExplicit()
    {
        await using var h = await Fixture();
        var legacy = await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/search");
        Assert.Equal(JsonValueKind.Array, legacy.ValueKind); Assert.Equal(50, legacy.GetArrayLength());
        Assert.Equal(2, (await Page(h, "page=99999")).GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task IntegrityUsesStoredEvidenceVirtualIsNotMissingAndNoServerPathEscapes()
    {
        await using var h = await Fixture(); string before = await Snapshot(h);
        var result = await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/3/integrity");
        Assert.False(result.GetProperty("nativeFileChecked").GetBoolean());
        Assert.Equal(2, result.GetProperty("unresolvedCount").GetInt32());
        Assert.Equal(1, result.GetProperty("virtualCount").GetInt32()); Assert.Equal(1, result.GetProperty("suppressedCount").GetInt32());
        Assert.Contains(result.GetProperty("references").EnumerateArray(), r => r.GetProperty("hasNewerVersion").GetBoolean());
        Assert.DoesNotContain("private-source", result.ToString());
        var source = result.GetProperty("propertySources").GetProperty("partName");
        Assert.Equal(JsonValueKind.Null, source.GetProperty("value").ValueKind); Assert.Equal(2, source.GetProperty("conflicts").GetArrayLength());
        Assert.Equal(before, await Snapshot(h));
    }

    [Fact]
    public async Task MissingVersionAndMissingDocumentCannotBeReportedAsHealthy()
    {
        await using var h = await Fixture();
        var result = await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/2/integrity");
        Assert.True(result.GetProperty("currentVersionMissing").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await h.Client.GetAsync("/api/documents/9999/integrity")).StatusCode);
    }

    private static async Task<JsonElement> Page(ContractHost h, string query)
    {
        var response = await h.Client.GetAsync("/api/documents/search?paged=true&" + query);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body); return JsonDocument.Parse(body).RootElement.Clone();
    }
    private static async Task<ContractHost> Fixture()
    {
        var h = await ContractHost.Start();
        try {
        await h.WithDb(async db => {
            var when = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
            var docs = Enumerable.Range(3, 70).Select(i => new PdmDocument { DocumentId = i, FileName = $"VAULT-{i:000}", PartNumber = $"P-{i:000}",
                DocumentType = i == 3 ? "Assembly" : i == 4 ? "Drawing" : "Part", FileExtension = i == 3 ? ".sldasm" : i == 4 ? ".slddrw" : ".sldprt",
                CreatedAt = when, UpdatedAt = when }).ToArray();
            db.AddRange(docs); await db.SaveChangesAsync();
            foreach(var d in docs) {
                var v = new PdmDocumentVersion { DocumentId = d.DocumentId, VersionNo = 1, OriginalFileName = d.FileName + d.FileExtension, CreatedAt = when };
                db.Add(v); await db.SaveChangesAsync(); d.CurrentVersionId = v.VersionId;
                db.CustomProperties.AddRange(new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "品名", PropertyValue = "測試元件" + d.DocumentId },
                    new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "圖號", PropertyValue = $"DWG-{d.DocumentId:000}" });
                if(d.DocumentId == 3) db.CustomProperties.Add(new() { VersionId = v.VersionId, PropertyName = "Description", PropertyValue = "衝突品名" });
                if(d.DocumentId == 72) db.CustomProperties.AddRange(new PdmCustomProperty { VersionId = v.VersionId, ConfigurationName = "Alternate", PropertyName = "Note", PropertyValue = "CONFIG-ONLY" },
                    new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "Note", PropertyValue = "literal%value" });
            }
            var first = await db.Documents.SingleAsync(d => d.DocumentId == 1);
            var old = new PdmCustomProperty { VersionId = first.CurrentVersionId!.Value, PropertyName = "Note", PropertyValue = "HISTORICAL-ONLY" }; db.Add(old);
            var latest = new PdmDocumentVersion { DocumentId = 1, VersionNo = 2, CreatedAt = when }; db.Add(latest); await db.SaveChangesAsync(); first.CurrentVersionId = latest.VersionId;
            long parent = docs.Single(d => d.DocumentId == 3).CurrentVersionId!.Value;
            db.BomOccurrences.AddRange(new PdmBomOccurrence { ParentVersionId = parent, OccurrencePath = "1", ChildVersionId = 1, SourceReferencePath = "C:/private-source/old.sldprt" },
                new PdmBomOccurrence { ParentVersionId = parent, OccurrencePath = "2", IsVirtual = true, ReferenceStatus = "Virtual", SourceReferencePath = "virtual.sldprt" },
                new PdmBomOccurrence { ParentVersionId = parent, OccurrencePath = "3", IsSuppressed = true, ReferenceStatus = "Missing", SourceReferencePath = "missing.sldprt" },
                new PdmBomOccurrence { ParentVersionId = parent, OccurrencePath = "4", ReferenceStatus = "Broken", SourceReferencePath = "ambiguous.sldprt" });
            await db.SaveChangesAsync();
        });
        return h;
        } catch { await h.DisposeAsync(); throw; }
    }
    private static async Task<string> Snapshot(ContractHost h)
    {
        string value = ""; await h.WithDb(async db => value = JsonSerializer.Serialize(new {
            documents = await db.Documents.AsNoTracking().OrderBy(d => d.DocumentId).ToArrayAsync(),
            versions = await db.DocumentVersions.AsNoTracking().OrderBy(v => v.VersionId).ToArrayAsync(),
            bom = await db.BomOccurrences.AsNoTracking().OrderBy(b => b.BomOccurrenceId).ToArrayAsync(),
            properties = await db.CustomProperties.AsNoTracking().OrderBy(p => p.CustomPropertyId).ToArrayAsync()
        })); return value;
    }
}
