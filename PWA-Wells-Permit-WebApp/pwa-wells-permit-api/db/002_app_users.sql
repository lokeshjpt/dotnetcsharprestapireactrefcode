/* ============================================================================
   002_app_users.sql

   Purpose:
     Move the intra (staff) application allowlist out of appsettings
     (Auth:WhitelistedUsers) and into a database table so access can be granted
     or revoked without a redeploy. The WhitelistAuthorizationFilter reads the
     active rows of this table to decide whether an authenticated Entra user is
     allowed to use the intra app.

   Semantics:
     - active = 1 rows form the allowlist. A user whose Entra email/UPN matches
       (case-insensitively) an active row is allowed; otherwise 403.
     - An empty allowlist (no active rows, or the table missing) leaves the gate
       open for any authenticated user, so a fresh/unseeded environment is never
       bricked.

   Safe to re-run: the table is only created when absent and each seed row is
   inserted only when its email is not already present.
   ============================================================================ */

SET XACT_ABORT ON;
GO

-- APP_USERS
IF OBJECT_ID('[EEAOWN].[app_users]', 'U') IS NULL
BEGIN
    CREATE TABLE [EEAOWN].[app_users]
    (
        email_id   VARCHAR(100) NOT NULL PRIMARY KEY,
        active     BIT NOT NULL,
        created_on DATETIME NOT NULL DEFAULT GETDATE(),
        created_by VARCHAR(100) NOT NULL,
        updated_on DATETIME NOT NULL DEFAULT GETDATE(),
        updated_by VARCHAR(100) NOT NULL
    );
END;
GO

-- Seed the current allowlist (migrated from Auth:WhitelistedUsers). Idempotent.
IF NOT EXISTS (SELECT 1 FROM [EEAOWN].[app_users] WHERE email_id = 'Janu.Sundaram@alamedacountyca.gov')
    INSERT INTO [EEAOWN].[app_users] (email_id, active, created_by, updated_by)
    VALUES ('Janu.Sundaram@alamedacountyca.gov', 1, 'system-migration', 'system-migration');

IF NOT EXISTS (SELECT 1 FROM [EEAOWN].[app_users] WHERE email_id = 'Brent.Dugan@alamedacountyca.gov')
    INSERT INTO [EEAOWN].[app_users] (email_id, active, created_by, updated_by)
    VALUES ('Brent.Dugan@alamedacountyca.gov', 1, 'system-migration', 'system-migration');

IF NOT EXISTS (SELECT 1 FROM [EEAOWN].[app_users] WHERE email_id = 'lokesh.sikharam@acgov.org')
    INSERT INTO [EEAOWN].[app_users] (email_id, active, created_by, updated_by)
    VALUES ('lokesh.sikharam@acgov.org', 1, 'system-migration', 'system-migration');
GO
