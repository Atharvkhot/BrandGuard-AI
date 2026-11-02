-- =====================================================================
-- BrandGuard AI — Read-Only Production Schema & Migration Verification Script
-- Target: Neon PostgreSQL (Production) / Local PostgreSQL
-- Mode: STRICTLY READ-ONLY (SELECT statements only, no modifications)
-- =====================================================================

-- 1. Check all user tables in public schema
SELECT 
    table_name,
    (SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = t.table_name) AS column_count
FROM information_schema.tables t
WHERE table_schema = 'public' 
  AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- 2. Inspect ValidationFindings columns (specifically verifying acknowledgment fields)
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'ValidationFindings'
ORDER BY ordinal_position;

-- 3. Check migration history entries (if table exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
    ) THEN
        RAISE NOTICE 'EF Migrations History table exists:';
    ELSE
        RAISE NOTICE 'EF Migrations History table does NOT exist.';
    END IF;
END $$;

SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- 4. Count records across all core domain tables to verify data presence
SELECT 'BrandGuidelines' AS entity, count(*) AS total_records FROM "BrandGuidelines"
UNION ALL
SELECT 'GuidelineChunks', count(*) FROM "GuidelineChunks"
UNION ALL
SELECT 'ContentSubmissions', count(*) FROM "ContentSubmissions"
UNION ALL
SELECT 'ValidationRuns', count(*) FROM "ValidationRuns"
UNION ALL
SELECT 'ValidationFindings', count(*) FROM "ValidationFindings"
UNION ALL
SELECT 'ReviewDecisions', count(*) FROM "ReviewDecisions"
UNION ALL
SELECT 'ValidationRules', count(*) FROM "ValidationRules";

-- 5. Check foreign key constraints
SELECT
    tc.table_name, 
    kcu.column_name, 
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name 
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
  AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
  ON ccu.constraint_name = tc.constraint_name
  AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' 
  AND tc.table_schema = 'public'
ORDER BY tc.table_name;
