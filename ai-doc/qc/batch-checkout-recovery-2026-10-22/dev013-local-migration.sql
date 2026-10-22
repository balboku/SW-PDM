-- Exact DEV-013 Up SQL with non-mutating preconditions. No other migration.
START TRANSACTION;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = public, pg_catalog;
LOCK TABLE pdm_documents, pdm_document_versions, pdm_custom_properties,
    pdm_bom_occurrences, pdm_document_identity_changes, pdm_numbering_rules,
    pdm_number_sequences IN SHARE MODE;
LOCK TABLE "__EFMigrationsHistory" IN SHARE ROW EXCLUSIVE MODE;
DO $guard$
DECLARE actual text[]; business_count bigint;
BEGIN
    IF NOT ((current_database()='swpdm' AND current_setting('port')='5432') OR
            (current_database()='dev013_schema_verification' AND current_setting('port')='55433')) THEN
        RAISE EXCEPTION 'Unexpected migration target';
    END IF;
    SELECT array_agg("MigrationId"::text ORDER BY "MigrationId") INTO actual FROM "__EFMigrationsHistory";
    IF actual IS DISTINCT FROM ARRAY[
        '20260415104839_InitialCreate','20260415113504_ChangeGoogleDriveToLocalStorage',
        '20260415131806_EnterpriseFeatures','20260415160318_EnterpriseNumbering',
        '20260423105804_AllowDuplicateStorageFileIds','20260430121116_AddThumbnailStorageId',
        '20260507105814_AddDocumentVersionChangeReason','20260804090408_AddDocumentIdentityChanges'] THEN
        RAISE EXCEPTION 'Existing migration history differs from reviewed baseline';
    END IF;
    IF to_regclass('public.pdm_checkout_batches') IS NOT NULL OR to_regclass('public.pdm_checkout_batch_items') IS NOT NULL THEN
        RAISE EXCEPTION 'DEV-013 objects already exist';
    END IF;
    SELECT (SELECT count(*) FROM pdm_documents)+(SELECT count(*) FROM pdm_document_versions)+
        (SELECT count(*) FROM pdm_custom_properties)+(SELECT count(*) FROM pdm_bom_occurrences)+
        (SELECT count(*) FROM pdm_document_identity_changes)+(SELECT count(*) FROM pdm_numbering_rules)+
        (SELECT count(*) FROM pdm_number_sequences) INTO business_count;
    IF business_count <> 0 THEN RAISE EXCEPTION 'Business data changed since reviewed empty baseline'; END IF;
END $guard$;

CREATE TABLE pdm_checkout_batches (
    batch_id uuid NOT NULL,
    root_document_id bigint NOT NULL,
    actor character varying(100) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_pdm_checkout_batches" PRIMARY KEY (batch_id)
);

CREATE TABLE pdm_checkout_batch_items (
    batch_id uuid NOT NULL,
    document_id bigint NOT NULL,
    version_id bigint NOT NULL,
    lock_time timestamp with time zone NOT NULL,
    restored_at timestamp with time zone,
    CONSTRAINT "PK_pdm_checkout_batch_items" PRIMARY KEY (batch_id, document_id),
    CONSTRAINT "FK_pdm_checkout_batch_items_pdm_checkout_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES pdm_checkout_batches (batch_id) ON DELETE RESTRICT
);

CREATE INDEX idx_checkout_batches_root_created ON pdm_checkout_batches (root_document_id, created_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261022073552_AddCheckoutBatches', '8.0.11');

DO $post$
BEGIN
    IF (SELECT count(*) FROM pdm_checkout_batches) <> 0 OR (SELECT count(*) FROM pdm_checkout_batch_items) <> 0 THEN
        RAISE EXCEPTION 'New batch tables must remain empty during migration';
    END IF;
    IF (SELECT count(*) FROM "__EFMigrationsHistory") <> 9 THEN RAISE EXCEPTION 'Unexpected history count'; END IF;
END $post$;
COMMIT;
