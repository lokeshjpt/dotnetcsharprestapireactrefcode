/* ============================================================================
   001_widen_audit_by_columns.sql

   Purpose:
     Widen every audit "*_by" column (add_by / update_by / approved_by /
     extend_by / final_approval_by) to VARCHAR(100) so the signed-in Entra
     identity (an email such as "Lokesh.Sikharam@acgov.org", 25 chars) fits.

     The columns were VARCHAR(20)/CHAR(20) (a few VARCHAR(50)), which caused
     SQL Server error 2628 ("String or binary data would be truncated ...
     column 'update_by'") on every staff edit from the intra app.

   Scope:
     EEAOWN schema. Nullability is preserved exactly as it exists today
     (restated on each ALTER because ALTER COLUMN would otherwise reset it).
     CHAR columns are converted to VARCHAR to avoid trailing-space padding.

   Safe to re-run: widening an already-VARCHAR(100) column is a no-op change.
   ============================================================================ */

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

/* ---- APPLICATION_INFO ------------------------------------------------------ */
ALTER TABLE [EEAOWN].[APPLICATION_INFO] ALTER COLUMN [add_by]       VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[APPLICATION_INFO] ALTER COLUMN [update_by]    VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[APPLICATION_INFO] ALTER COLUMN [approved_by]  VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[APPLICATION_INFO] ALTER COLUMN [extend_by]    VARCHAR(100) NULL;
GO

/* ---- APP_HAZARD_INFO ------------------------------------------------------- */
ALTER TABLE [EEAOWN].[APP_HAZARD_INFO] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[APP_HAZARD_INFO] ALTER COLUMN [UPDATE_By]  VARCHAR(100) NULL;
GO

/* ---- APP_WORKS ------------------------------------------------------------- */
ALTER TABLE [EEAOWN].[APP_WORKS] ALTER COLUMN [add_by]     VARCHAR(100) NOT NULL;
ALTER TABLE [EEAOWN].[APP_WORKS] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- APP_WORK_SPECS -------------------------------------------------------- */
ALTER TABLE [EEAOWN].[APP_WORK_SPECS] ALTER COLUMN [add_by]     VARCHAR(100) NOT NULL;
ALTER TABLE [EEAOWN].[APP_WORK_SPECS] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- APP_WORK_PERMITS ------------------------------------------------------ */
ALTER TABLE [EEAOWN].[APP_WORK_PERMITS] ALTER COLUMN [add_by]     VARCHAR(100) NOT NULL;
ALTER TABLE [EEAOWN].[APP_WORK_PERMITS] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- APP_PAYMENT_INFO ------------------------------------------------------ */
ALTER TABLE [EEAOWN].[APP_PAYMENT_INFO] ALTER COLUMN [add_by]     VARCHAR(100) NOT NULL;
ALTER TABLE [EEAOWN].[APP_PAYMENT_INFO] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- APP_DOCUMENT_LINKS ---------------------------------------------------- */
ALTER TABLE [EEAOWN].[APP_DOCUMENT_LINKS] ALTER COLUMN [ADD_BY]  VARCHAR(100) NULL;
GO

/* ---- APP_NOTES ------------------------------------------------------------- */
ALTER TABLE [EEAOWN].[APP_NOTES] ALTER COLUMN [add_by]  VARCHAR(100) NOT NULL;
GO

/* ---- INSPECTION_ASSIGNMENTS (already VARCHAR(50); widen for consistency) --- */
ALTER TABLE [EEAOWN].[INSPECTION_ASSIGNMENTS] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[INSPECTION_ASSIGNMENTS] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

/* ---- INSPECTION_NOTES ------------------------------------------------------ */
ALTER TABLE [EEAOWN].[INSPECTION_NOTES] ALTER COLUMN [ADD_BY]  VARCHAR(100) NULL;
GO

/* ---- INSPECTORS (CHAR(20) -> VARCHAR(100) to drop space padding) ----------- */
ALTER TABLE [EEAOWN].[INSPECTORS] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[INSPECTORS] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

/* ---- HIST_PERMITS ---------------------------------------------------------- */
ALTER TABLE [EEAOWN].[HIST_PERMITS] ALTER COLUMN [add_by]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[HIST_PERMITS] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- HIST_WELL_LOC --------------------------------------------------------- */
ALTER TABLE [EEAOWN].[HIST_WELL_LOC] ALTER COLUMN [add_by]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[HIST_WELL_LOC] ALTER COLUMN [update_by]  VARCHAR(100) NULL;
GO

/* ---- X_INSPECTION_WORKBOOK ------------------------------------------------- */
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK] ALTER COLUMN [ADD_BY]            VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK] ALTER COLUMN [UPDATE_BY]         VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK] ALTER COLUMN [FINAL_APPROVAL_BY] VARCHAR(100) NULL;
GO

/* ---- X_INSPECTION_WORKBOOK_BORING ------------------------------------------ */
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_BORING] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_BORING] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

/* ---- X_INSPECTION_WORKBOOK_CONSTRUCTION ------------------------------------ */
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_CONSTRUCTION] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_CONSTRUCTION] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

/* ---- X_INSPECTION_WORKBOOK_DESTRUCTION ------------------------------------- */
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_DESTRUCTION] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_DESTRUCTION] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

/* ---- X_INSPECTION_WORKBOOK_MONITORING -------------------------------------- */
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_MONITORING] ALTER COLUMN [ADD_BY]     VARCHAR(100) NULL;
ALTER TABLE [EEAOWN].[X_INSPECTION_WORKBOOK_MONITORING] ALTER COLUMN [UPDATE_BY]  VARCHAR(100) NULL;
GO

COMMIT TRANSACTION;
GO

/* ---- Verification: every "*_by" column should now report max_length = 100 --- */
SELECT s.name AS [schema], t.name AS [table], c.name AS [column],
       ty.name AS [type], c.max_length, c.is_nullable
FROM sys.columns c
JOIN sys.tables  t  ON t.object_id = c.object_id
JOIN sys.schemas s  ON s.schema_id = t.schema_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
WHERE c.name LIKE '%[_]by'
  AND ty.name IN ('varchar','nvarchar','char','nchar')
ORDER BY t.name, c.name;
GO
