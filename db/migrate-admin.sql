-- migrate-admin.sql
-- יצירת טבלת Admin אם אינה קיימת
-- הרצה: sqlcmd -S "(localdb)\MSSQLLocalDB" -d OptiLoadDB -i migrate-admin.sql

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'Admin'
)
BEGIN
    CREATE TABLE Admin (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        Username     NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        PasswordSalt NVARCHAR(500) NOT NULL,
        CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_Admin_Username UNIQUE (Username)
    );
    PRINT 'Admin table created.';
END
ELSE
    PRINT 'Admin table already exists.';
