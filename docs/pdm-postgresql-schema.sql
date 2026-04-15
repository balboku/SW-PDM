-- PDM database schema for a web-based SolidWorks file management system.
-- Target database: PostgreSQL 14+
--
-- If you use SQL Server instead:
-- 1. Replace "BIGINT GENERATED ALWAYS AS IDENTITY" with "BIGINT IDENTITY(1,1)"
-- 2. Replace "TIMESTAMPTZ" with "DATETIMEOFFSET"
-- 3. Replace "BOOLEAN" with "BIT"
--
-- Design notes:
-- - pdm_documents = logical CAD document master record
-- - pdm_document_versions = one physical uploaded binary per version/revision
-- - pdm_custom_properties = flexible key/value storage for SolidWorks custom properties
-- - pdm_bom_occurrences = version-specific parent/child BOM relationship table
--
-- The BOM table is intentionally version-based, not document-based.
-- This is critical because Assembly Rev A and Assembly Rev B can reference
-- different child files, different child revisions, and even different quantities.

CREATE TABLE pdm_documents (
    document_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    file_name VARCHAR(255) NOT NULL,
    file_extension VARCHAR(10) NOT NULL,
    document_type VARCHAR(20) NOT NULL,
    part_number VARCHAR(100) NULL,
    revision_label VARCHAR(50) NULL,
    material VARCHAR(100) NULL,
    designer VARCHAR(100) NULL,
    current_version_id BIGINT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pdm_documents_extension
        CHECK (file_extension IN ('.sldprt', '.sldasm', '.slddrw')),
    CONSTRAINT ck_pdm_documents_type
        CHECK (document_type IN ('Part', 'Assembly', 'Drawing'))
);

CREATE TABLE pdm_document_versions (
    version_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    document_id BIGINT NOT NULL REFERENCES pdm_documents(document_id) ON DELETE CASCADE,
    version_no INTEGER NOT NULL,
    revision_label VARCHAR(50) NULL,
    google_drive_file_id VARCHAR(255) NOT NULL,
    original_file_name VARCHAR(255) NOT NULL,
    source_file_path TEXT NOT NULL,
    vault_relative_path TEXT NOT NULL,
    checksum_sha256 CHAR(64) NULL,
    file_size_bytes BIGINT NULL,
    source_last_write_utc TIMESTAMPTZ NULL,
    parsed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pdm_document_versions_doc_ver UNIQUE (document_id, version_no),
    CONSTRAINT uq_pdm_document_versions_drive_file_id UNIQUE (google_drive_file_id)
);

ALTER TABLE pdm_documents
    ADD CONSTRAINT fk_pdm_documents_current_version
    FOREIGN KEY (current_version_id)
    REFERENCES pdm_document_versions(version_id)
    ON DELETE SET NULL;

CREATE TABLE pdm_custom_properties (
    custom_property_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    version_id BIGINT NOT NULL REFERENCES pdm_document_versions(version_id) ON DELETE CASCADE,
    configuration_name VARCHAR(255) NOT NULL DEFAULT '',
    property_name VARCHAR(128) NOT NULL,
    property_value TEXT NULL,
    property_type VARCHAR(50) NULL,
    raw_expression TEXT NULL,
    is_resolved BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pdm_custom_properties UNIQUE (version_id, configuration_name, property_name)
);

CREATE TABLE pdm_bom_occurrences (
    bom_occurrence_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    parent_version_id BIGINT NOT NULL REFERENCES pdm_document_versions(version_id) ON DELETE CASCADE,
    child_version_id BIGINT NULL REFERENCES pdm_document_versions(version_id) ON DELETE RESTRICT,
    occurrence_path VARCHAR(1000) NOT NULL,
    parent_configuration_name VARCHAR(255) NOT NULL DEFAULT '',
    child_configuration_name VARCHAR(255) NOT NULL DEFAULT '',
    quantity NUMERIC(18, 6) NOT NULL DEFAULT 1,
    find_number VARCHAR(50) NULL,
    source_reference_path TEXT NOT NULL,
    package_relative_path TEXT NOT NULL,
    reference_status VARCHAR(20) NOT NULL DEFAULT 'Resolved',
    is_suppressed BOOLEAN NOT NULL DEFAULT FALSE,
    is_virtual BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pdm_bom_occurrences_status
        CHECK (reference_status IN ('Resolved', 'Broken', 'Virtual', 'Missing')),
    CONSTRAINT uq_pdm_bom_occurrences_parent_path UNIQUE (parent_version_id, occurrence_path)
);

CREATE INDEX idx_pdm_document_versions_document_id
    ON pdm_document_versions(document_id);

CREATE INDEX idx_pdm_custom_properties_lookup
    ON pdm_custom_properties(property_name, property_value);

CREATE INDEX idx_pdm_bom_occurrences_parent
    ON pdm_bom_occurrences(parent_version_id);

CREATE INDEX idx_pdm_bom_occurrences_child
    ON pdm_bom_occurrences(child_version_id);

CREATE INDEX idx_pdm_bom_occurrences_status
    ON pdm_bom_occurrences(reference_status, is_suppressed);

CREATE VIEW pdm_bom_rollup AS
SELECT
    parent_version_id,
    child_version_id,
    parent_configuration_name,
    child_configuration_name,
    SUM(quantity) AS total_quantity
FROM pdm_bom_occurrences
WHERE reference_status = 'Resolved'
  AND is_suppressed = FALSE
GROUP BY
    parent_version_id,
    child_version_id,
    parent_configuration_name,
    child_configuration_name;

-- Example recursive query for downloading an assembly and all descendants.
-- Replace :root_version_id with your parameter style when integrating.
--
-- WITH RECURSIVE bom_tree AS (
--     SELECT
--         b.parent_version_id,
--         b.child_version_id,
--         b.occurrence_path,
--         1 AS depth,
--         ARRAY[b.parent_version_id, COALESCE(b.child_version_id, -1)] AS visited_chain
--     FROM pdm_bom_occurrences b
--     WHERE b.parent_version_id = :root_version_id
--       AND b.reference_status = 'Resolved'
--       AND b.is_suppressed = FALSE
--
--     UNION ALL
--
--     SELECT
--         b.parent_version_id,
--         b.child_version_id,
--         b.occurrence_path,
--         bt.depth + 1,
--         bt.visited_chain || COALESCE(b.child_version_id, -1)
--     FROM pdm_bom_occurrences b
--     INNER JOIN bom_tree bt
--         ON b.parent_version_id = bt.child_version_id
--     WHERE b.reference_status = 'Resolved'
--       AND b.is_suppressed = FALSE
--       AND NOT COALESCE(b.child_version_id, -1) = ANY(bt.visited_chain)
-- )
-- SELECT DISTINCT v.*
-- FROM (
--     SELECT :root_version_id AS version_id
--     UNION
--     SELECT child_version_id
--     FROM bom_tree
--     WHERE child_version_id IS NOT NULL
-- ) needed
-- INNER JOIN pdm_document_versions v
--     ON v.version_id = needed.version_id;
