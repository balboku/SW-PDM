using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;

namespace ThumbnailRepair
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            
            string connString = "Host=localhost;Port=5433;Database=swpdm;Username=swpdm_user;Password=CHANGE_ME";
            
            services.AddDbContext<PdmDbContext>(options =>
                options.UseNpgsql(connString));

            services.AddLogging(builder => builder.AddConsole());
            
            var options = new SolidWorksDocumentManagerOptions {
                LicenseKey = "MAXIMABIOTECHINC:swdocmgr_general-11785-02051-00064-33793-08754-34307-00007-39520-03578-03601-61651-42049-29685-00692-24582-23083-05252-41135-59262-09739-34061-22530-01332-09569-01333-09481-20797-03349-09505-03385-14337-27746-58970-57546-25690-25696-1062,swdocmgr_xml-11785-02051-00064-33793-08754-34307-00007-28136-50833-39146-04404-30281-31587-62036-12290-05400-58305-43417-32067-30916-12319-23153-01332-09569-01333-09481-20797-03349-09505-03385-14337-27746-58970-57546-25690-25696-1061,swdocmgr_previews-11785-02051-00064-33793-08754-34307-00007-11608-10823-45570-54688-00650-25099-32636-02049-01550-21429-63941-08467-39714-16934-22979-01332-09569-01333-09481-20797-03349-09505-03385-14337-27746-58970-57546-25690-25696-1068,swdocmgr_tessellation-11785-02051-00064-33793-08754-34307-00007-13816-49312-12196-34389-59686-05906-12740-08192-08047-55839-01174-39429-45969-48480-23130-01332-09569-01333-09481-20797-03349-09505-03385-14337-27746-58970-57546-25690-25696-1069"
            };
            services.AddSingleton(Options.Create(options));
            services.AddSingleton<SolidWorksDocumentManagerServiceFactory>();
            services.AddSingleton(new LocalStorageService("vault_storage"));

            var provider = services.BuildServiceProvider();
            var dbContext = provider.GetRequiredService<PdmDbContext>();
            var factory = provider.GetRequiredService<SolidWorksDocumentManagerServiceFactory>();
            var storage = provider.GetRequiredService<LocalStorageService>();

            var versions = await dbContext.DocumentVersions
                .Where(v => v.ThumbnailStorageId == null)
                .ToListAsync();

            Console.WriteLine($"Found {versions.Count} versions without thumbnails.");

            using var docMgrFactory = factory.Create();

            foreach (var v in versions)
            {
                try {
                    string fullPath = Path.Combine("vault_storage", v.StorageFileId);
                    if (!File.Exists(fullPath)) {
                         // Check API subfolder as fallback
                         fullPath = Path.Combine("src", "SWPdm.Api", "vault_storage", v.StorageFileId);
                    }

                    if (!File.Exists(fullPath)) {
                        Console.WriteLine($"File not found for version {v.VersionId}: {v.StorageFileId}");
                        continue;
                    }

                    Console.WriteLine($"Processing version {v.VersionId}: {v.OriginalFileName}");
                    
                    var parseResult = docMgrFactory.Parse(fullPath);
                    if (parseResult.ThumbnailData is { Length: > 0 })
                    {
                        string thumbnailFileName = $"{Path.GetFileNameWithoutExtension(v.OriginalFileName)}_thumbnail.png";
                        string thumbId = await storage.UploadBytesAsync(parseResult.ThumbnailData, thumbnailFileName, "Thumbnails");
                        v.ThumbnailStorageId = thumbId;
                        dbContext.DocumentVersions.Update(v);
                        Console.WriteLine($"Successfully extracted thumbnail for version {v.VersionId}");
                    } else {
                        Console.WriteLine($"No thumbnail data found in file for version {v.VersionId}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Error processing version {v.VersionId}: {ex.Message}");
                }
            }

            await dbContext.SaveChangesAsync();
            Console.WriteLine("Repair completed.");
        }
    }
}
