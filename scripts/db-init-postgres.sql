-- =============================================================
--  SWPdm PostgreSQL 初始化腳本
--  執行方式：psql -U postgres -f scripts/db-init-postgres.sql
-- =============================================================

-- 1. 建立專用資料庫使用者（若已存在則略過）
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'swpdm_user') THEN
        CREATE ROLE swpdm_user WITH LOGIN PASSWORD 'CHANGE_ME';
        RAISE NOTICE 'Role swpdm_user created.';
    ELSE
        RAISE NOTICE 'Role swpdm_user already exists, skipping.';
    END IF;
END
$$;

-- 2. 建立資料庫（若已存在則略過）
SELECT 'CREATE DATABASE swpdm OWNER swpdm_user ENCODING ''UTF8'' LC_COLLATE ''en_US.UTF-8'' LC_CTYPE ''en_US.UTF-8'' TEMPLATE template0;'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'swpdm') \gexec

-- 3. 授予必要權限
GRANT ALL PRIVILEGES ON DATABASE swpdm TO swpdm_user;

-- 4. 切換到 swpdm 資料庫，授予 schema 權限
\c swpdm
GRANT ALL ON SCHEMA public TO swpdm_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO swpdm_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO swpdm_user;

-- 5. 啟用 uuid-ossp 擴充（若專案用到 UUID 欄位）
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 6. 完成提示
\echo ''
\echo '✅  swpdm 資料庫初始化完成！'
\echo '   接下來請執行：./scripts/db-update.sh'
\echo ''
