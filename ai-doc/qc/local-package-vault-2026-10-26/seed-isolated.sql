BEGIN;
DO $guard$ BEGIN IF current_database()<>'release_http_20261026' OR current_setting('port')<>'55433' THEN RAISE EXCEPTION 'Wrong isolated database'; END IF; END $guard$;
INSERT INTO pdm_documents (document_id,file_name,file_extension,document_type,part_number,is_active,created_at,updated_at,checked_out_by,checked_out_at) VALUES
(1,'synthetic-root.sldasm','.sldasm','Assembly','SYNTH-ROOT',true,now(),now(),null,null),
(2,'synthetic-reused.sldprt','.sldprt','Part','SYNTH-REUSED',true,now(),now(),'User','2026-10-22 00:00:00+00'),
(3,'synthetic-child.sldprt','.sldprt','Part','SYNTH-CHILD',true,now(),now(),null,null),
(4,'synthetic-drawing.slddrw','.slddrw','Drawing','SYNTH-DRAWING',true,now(),now(),null,null);
INSERT INTO pdm_document_versions (version_id,document_id,version_no,revision_label,storage_file_id,original_file_name,source_file_path,vault_relative_path,created_at,lifecycle_state)
SELECT document_id,document_id,1,'A','synthetic-'||document_id,file_name,'','',now(),'WIP' FROM pdm_documents;
UPDATE pdm_documents SET current_version_id=document_id;
INSERT INTO pdm_bom_occurrences (parent_version_id,child_version_id,occurrence_path,parent_configuration_name,child_configuration_name,quantity,source_reference_path,package_relative_path,reference_status,is_suppressed,is_virtual,created_at) VALUES
(1,2,'root/reused','','',1,'synthetic-reused.sldprt','synthetic-reused.sldprt','Resolved',false,false,now()),
(1,3,'root/child','','',1,'synthetic-child.sldprt','synthetic-child.sldprt','Resolved',false,false,now()),
(4,1,'drawing/root','','',1,'synthetic-root.sldasm','synthetic-root.sldasm','Resolved',false,false,now());
COMMIT;
