namespace SWPdm.Sample.Data;

using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data.Entities;

public sealed class PdmDbContext : DbContext
{
    public PdmDbContext(DbContextOptions<PdmDbContext> options)
        : base(options)
    {
    }

    public DbSet<PdmDocument> Documents => Set<PdmDocument>();

    public DbSet<PdmDocumentVersion> DocumentVersions => Set<PdmDocumentVersion>();

    public DbSet<PdmCustomProperty> CustomProperties => Set<PdmCustomProperty>();

    public DbSet<PdmBomOccurrence> BomOccurrences => Set<PdmBomOccurrence>();

    public DbSet<PdmNumberingRule> NumberingRules => Set<PdmNumberingRule>();

    public DbSet<PdmNumberSequence> NumberSequences => Set<PdmNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureDocuments(modelBuilder);
        ConfigureDocumentVersions(modelBuilder);
        ConfigureCustomProperties(modelBuilder);
        ConfigureBomOccurrences(modelBuilder);
        ConfigureNumbering(modelBuilder);
    }

    private static void ConfigureDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdmDocument>(entity =>
        {
            entity.ToTable("pdm_documents", table =>
            {
                table.HasCheckConstraint("ck_pdm_documents_extension", "file_extension IN ('.sldprt', '.sldasm', '.slddrw')");
                table.HasCheckConstraint("ck_pdm_documents_type", "document_type IN ('Part', 'Assembly', 'Drawing')");
            });
            entity.HasKey(x => x.DocumentId);
            entity.Property(x => x.DocumentId).HasColumnName("document_id");
            entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
            entity.Property(x => x.FileExtension).HasColumnName("file_extension").HasMaxLength(10);
            entity.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(20);
            entity.Property(x => x.PartNumber).HasColumnName("part_number").HasMaxLength(100);
            entity.Property(x => x.RevisionLabel).HasColumnName("revision_label").HasMaxLength(50);
            entity.Property(x => x.Material).HasColumnName("material").HasMaxLength(100);
            entity.Property(x => x.Designer).HasColumnName("designer").HasMaxLength(100);
            entity.Property(x => x.CurrentVersionId).HasColumnName("current_version_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CheckedOutBy).HasColumnName("checked_out_by").HasMaxLength(100);
            entity.Property(x => x.CheckedOutAt).HasColumnName("checked_out_at");

            entity.HasMany(x => x.Versions)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CurrentVersion)
                .WithMany()
                .HasForeignKey(x => x.CurrentVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureDocumentVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdmDocumentVersion>(entity =>
        {
            entity.ToTable("pdm_document_versions", table =>
            {
                table.HasCheckConstraint("ck_pdm_document_versions_lifecycle", "lifecycle_state IN ('WIP', 'InReview', 'Released', 'Obsolete')");
            });
            entity.HasKey(x => x.VersionId);
            entity.Property(x => x.VersionId).HasColumnName("version_id");
            entity.Property(x => x.DocumentId).HasColumnName("document_id");
            entity.Property(x => x.VersionNo).HasColumnName("version_no");
            entity.Property(x => x.RevisionLabel).HasColumnName("revision_label").HasMaxLength(50);
            entity.Property(x => x.StorageFileId).HasColumnName("storage_file_id").HasMaxLength(255);
            entity.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255);
            entity.Property(x => x.SourceFilePath).HasColumnName("source_file_path");
            entity.Property(x => x.VaultRelativePath).HasColumnName("vault_relative_path");
            entity.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256").HasMaxLength(64);
            entity.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(x => x.SourceLastWriteUtc).HasColumnName("source_last_write_utc");
            entity.Property(x => x.ParsedAt).HasColumnName("parsed_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.LifecycleState).HasColumnName("lifecycle_state").HasMaxLength(20).HasDefaultValue("WIP");

            entity.HasIndex(x => x.DocumentId)
                .HasDatabaseName("idx_pdm_document_versions_document_id");

            entity.HasIndex(x => new { x.DocumentId, x.VersionNo })
                .IsUnique()
                .HasDatabaseName("uq_pdm_document_versions_doc_ver");

            entity.HasIndex(x => x.StorageFileId)
                .HasDatabaseName("idx_pdm_document_versions_storage_file_id");
        });
    }

    private static void ConfigureCustomProperties(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdmCustomProperty>(entity =>
        {
            entity.ToTable("pdm_custom_properties");
            entity.HasKey(x => x.CustomPropertyId);
            entity.Property(x => x.CustomPropertyId).HasColumnName("custom_property_id");
            entity.Property(x => x.VersionId).HasColumnName("version_id");
            entity.Property(x => x.ConfigurationName).HasColumnName("configuration_name").HasMaxLength(255);
            entity.Property(x => x.PropertyName).HasColumnName("property_name").HasMaxLength(128);
            entity.Property(x => x.PropertyValue).HasColumnName("property_value");
            entity.Property(x => x.PropertyType).HasColumnName("property_type").HasMaxLength(50);
            entity.Property(x => x.RawExpression).HasColumnName("raw_expression");
            entity.Property(x => x.IsResolved).HasColumnName("is_resolved");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne(x => x.Version)
                .WithMany(x => x.CustomProperties)
                .HasForeignKey(x => x.VersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.VersionId, x.ConfigurationName, x.PropertyName })
                .IsUnique()
                .HasDatabaseName("uq_pdm_custom_properties");

            entity.HasIndex(x => new { x.PropertyName, x.PropertyValue })
                .HasDatabaseName("idx_pdm_custom_properties_lookup");
        });
    }

    private static void ConfigureBomOccurrences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdmBomOccurrence>(entity =>
        {
            entity.ToTable("pdm_bom_occurrences", table =>
            {
                table.HasCheckConstraint(
                    "ck_pdm_bom_occurrences_status",
                    "reference_status IN ('Resolved', 'Broken', 'Virtual', 'Missing')");
            });
            entity.HasKey(x => x.BomOccurrenceId);
            entity.Property(x => x.BomOccurrenceId).HasColumnName("bom_occurrence_id");
            entity.Property(x => x.ParentVersionId).HasColumnName("parent_version_id");
            entity.Property(x => x.ChildVersionId).HasColumnName("child_version_id");
            entity.Property(x => x.OccurrencePath).HasColumnName("occurrence_path").HasMaxLength(1000);
            entity.Property(x => x.ParentConfigurationName).HasColumnName("parent_configuration_name").HasMaxLength(255);
            entity.Property(x => x.ChildConfigurationName).HasColumnName("child_configuration_name").HasMaxLength(255);
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 6);
            entity.Property(x => x.FindNumber).HasColumnName("find_number").HasMaxLength(50);
            entity.Property(x => x.SourceReferencePath).HasColumnName("source_reference_path");
            entity.Property(x => x.PackageRelativePath).HasColumnName("package_relative_path");
            entity.Property(x => x.ReferenceStatus).HasColumnName("reference_status").HasMaxLength(20);
            entity.Property(x => x.IsSuppressed).HasColumnName("is_suppressed");
            entity.Property(x => x.IsVirtual).HasColumnName("is_virtual");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne(x => x.ParentVersion)
                .WithMany(x => x.ParentBomOccurrences)
                .HasForeignKey(x => x.ParentVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ChildVersion)
                .WithMany(x => x.ChildBomOccurrences)
                .HasForeignKey(x => x.ChildVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ParentVersionId)
                .HasDatabaseName("idx_pdm_bom_occurrences_parent");

            entity.HasIndex(x => x.ChildVersionId)
                .HasDatabaseName("idx_pdm_bom_occurrences_child");

            entity.HasIndex(x => new { x.ReferenceStatus, x.IsSuppressed })
                .HasDatabaseName("idx_pdm_bom_occurrences_status");

            entity.HasIndex(x => new { x.ParentVersionId, x.OccurrencePath })
                .IsUnique()
                .HasDatabaseName("uq_pdm_bom_occurrences_parent_path");
        });
    }

    private static void ConfigureNumbering(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdmNumberingRule>(entity =>
        {
            entity.ToTable("pdm_numbering_rules");
            entity.HasKey(x => x.RuleId);
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(20);
            entity.Property(x => x.Pattern).HasColumnName("pattern").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(x => x.DocumentType)
                .IsUnique()
                .HasDatabaseName("uq_pdm_numbering_rules_document_type");
        });

        modelBuilder.Entity<PdmNumberSequence>(entity =>
        {
            entity.ToTable("pdm_number_sequences");
            entity.HasKey(x => x.SequenceId);
            entity.Property(x => x.SequenceId).HasColumnName("sequence_id");
            entity.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(100);
            entity.Property(x => x.CurrentValue).HasColumnName("current_value");
            entity.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at");

            entity.HasIndex(x => x.Prefix)
                .IsUnique()
                .HasDatabaseName("uq_pdm_number_sequences_prefix");
        });
    }
}
