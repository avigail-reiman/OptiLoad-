-- migrate-sessions.sql
-- טבלאות: PackingSession, SessionUser, AccessRequest, SessionBox, BoxAuditLog
-- הרצה: sqlcmd -S "(localdb)\MSSQLLocalDB" -d OptiLoadDB -i db\migrate-sessions.sql

-- 1. PackingSession – תוכנית שיבוץ עם קישור שיתוף
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PackingSession')
BEGIN
    CREATE TABLE PackingSession (
        SessionId   INT IDENTITY(1,1) PRIMARY KEY,
        AdminId     INT           NOT NULL,
        Name        NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NULL,
        LinkToken   CHAR(36)      NOT NULL,
        Status      NVARCHAR(20)  NOT NULL DEFAULT 'Open',   -- Open / Closed
        CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_PackingSession_LinkToken UNIQUE (LinkToken),
        CONSTRAINT FK_PackingSession_Admin FOREIGN KEY (AdminId) REFERENCES Admin(Id)
    );
    PRINT 'PackingSession table created.';
END
ELSE PRINT 'PackingSession table already exists.';

-- 2. SessionUser – אדם שקיבל קישור ומבקש גישה
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SessionUser')
BEGIN
    CREATE TABLE SessionUser (
        SessionUserId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId     INT           NOT NULL,
        DisplayName   NVARCHAR(100) NOT NULL,
        Email         NVARCHAR(200) NULL,
        Token         CHAR(36)      NOT NULL,
        CreatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_SessionUser_Token   UNIQUE (Token),
        CONSTRAINT FK_SessionUser_Session FOREIGN KEY (SessionId) REFERENCES PackingSession(SessionId)
    );
    PRINT 'SessionUser table created.';
END
ELSE PRINT 'SessionUser table already exists.';

-- 3. AccessRequest – בקשת הרשאה מסשן-יוזר
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AccessRequest')
BEGIN
    CREATE TABLE AccessRequest (
        RequestId     INT IDENTITY(1,1) PRIMARY KEY,
        SessionId     INT          NOT NULL,
        SessionUserId INT          NOT NULL,
        Status        NVARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending / Approved / Denied
        RequestedAt   DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
        RespondedAt   DATETIME2    NULL,
        CONSTRAINT FK_AccessRequest_Session FOREIGN KEY (SessionId)     REFERENCES PackingSession(SessionId),
        CONSTRAINT FK_AccessRequest_User    FOREIGN KEY (SessionUserId) REFERENCES SessionUser(SessionUserId)
    );
    PRINT 'AccessRequest table created.';
END
ELSE PRINT 'AccessRequest table already exists.';

-- 4. SessionBox – ארגזים שהוכנסו לתוכנית שיבוץ
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SessionBox')
BEGIN
    CREATE TABLE SessionBox (
        SessionBoxId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId    INT           NOT NULL,
        BoxId        INT           NOT NULL,
        Quantity     INT           NOT NULL DEFAULT 1,
        AddedBy      NVARCHAR(100) NOT NULL,
        AddedAt      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_SessionBox_Session FOREIGN KEY (SessionId) REFERENCES PackingSession(SessionId),
        CONSTRAINT FK_SessionBox_Box     FOREIGN KEY (BoxId)     REFERENCES Box(BoxId)
    );
    PRINT 'SessionBox table created.';
END
ELSE PRINT 'SessionBox table already exists.';

-- 5. BoxAuditLog – לוג שינויים בארגזים
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BoxAuditLog')
BEGIN
    CREATE TABLE BoxAuditLog (
        LogId         INT IDENTITY(1,1) PRIMARY KEY,
        SessionId     INT           NOT NULL,
        Action        NVARCHAR(20)  NOT NULL,        -- Added / Deleted
        BoxId         INT           NULL,
        BoxName       NVARCHAR(200) NOT NULL,
        Quantity      INT           NULL,
        ChangedBy     NVARCHAR(100) NOT NULL,
        ChangedByType NVARCHAR(20)  NOT NULL,        -- Admin / User
        ChangedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_BoxAuditLog_Session FOREIGN KEY (SessionId) REFERENCES PackingSession(SessionId)
    );
    PRINT 'BoxAuditLog table created.';
END
ELSE PRINT 'BoxAuditLog table already exists.';

-- 6. Add RootToken column to SessionUser (for join-request retry chain / DDoS prevention)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'SessionUser' AND COLUMN_NAME = 'RootToken')
BEGIN
    ALTER TABLE SessionUser ADD RootToken CHAR(36) NOT NULL DEFAULT '';
    -- Backfill via dynamic SQL so the column name is resolved at runtime (not parse time)
    EXEC sp_executesql N'UPDATE SessionUser SET RootToken = Token WHERE RootToken = ''''';
    PRINT 'SessionUser.RootToken column added and backfilled.';
END
ELSE PRINT 'SessionUser.RootToken column already exists.';
