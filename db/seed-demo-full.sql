-- ============================================================
-- seed-demo-full.sql
-- Demo: מכולה אחת מלאה כמעט לחלוטין (98%)
-- 140 ארגזים: LARGE-B × 3, MED-A × 28, SMALL-A × 61, FRAGILE-S × 48
--
-- מבנה המכולה (600 × 240 × 240):
--
--   Y ↑
--  240 ├── FRAGILE-S (50×40×50) ─── שורת ארגזים שבירים עליונה ──────────────┤
--  200 ├── MED-A (80×80×80) ──────── שכבת אמצע 7×3 ───────────────────────────┤
--  120 ├── LARGE-B (200×120×150) ─── 3 ארגזים גדולים בחזית ──────────────────┤
--    0 └──────────────────────────────────────────────────────────────── Z→ 240
--
-- הרצה: sqlcmd -S "(localdb)\MSSQLLocalDB" -d OptiLoadDB -i db\seed-demo-full.sql
-- ============================================================
SET NOCOUNT ON;
BEGIN TRANSACTION;

-- ── 1. וודא שסוגי הארגזים קיימים ──────────────────────────
MERGE INTO Box AS T
USING (VALUES
    ('LARGE-B',   200, 120, 150, 50, 0, 1),
    ('MED-A',      80,  80,  80, 15, 0, 1),
    ('SMALL-A',    40,  40,  40,  4, 0, 1),
    ('FRAGILE-S',  50,  40,  50,  3, 1, 1)
) AS S(BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation)
ON T.BoxName = S.BoxName
WHEN NOT MATCHED THEN
    INSERT (BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, CreatedAt)
    VALUES (S.BoxName, S.Width, S.Height, S.Depth,
            S.WeightKg, S.IsFragile, S.AllowRotation, GETUTCDATE());

-- ── 2. תבנית מכולה ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM ContainerTemplate WHERE TemplateName = N'Demo – מכולה מלאה')
    INSERT INTO ContainerTemplate (TemplateName, Width, Height, Depth, MaxWeightKg, CreatedAt)
    VALUES (N'Demo – מכולה מלאה', 600, 240, 240, 50000, GETUTCDATE());

DECLARE @ContainerId INT = (
    SELECT TemplateId FROM ContainerTemplate WHERE TemplateName = N'Demo – מכולה מלאה');

-- ── 3. Admin ────────────────────────────────────────────────
DECLARE @AdminId INT = (SELECT TOP 1 Id FROM Admin ORDER BY Id);

-- ── 4. משרת אריזה ───────────────────────────────────────────
INSERT INTO PackingJob
    (ContainerId, AdminId, Status, BinsUsed,
     VolumeUtilization, TotalWeightKg, SolveTimeSeconds,
     IsOptimal, StatusMessage, CreatedAt, CompletedAt)
VALUES
    (@ContainerId, @AdminId, 'Completed', 1,
     97.9, 958, 1.5,
     1, N'Demo: מכולה מלאה – 140 ארגזים', GETUTCDATE(), GETUTCDATE());

DECLARE @JobId INT = SCOPE_IDENTITY();

-- ── 5. מזהי ארגזים ──────────────────────────────────────────
DECLARE @LargeBId INT = (SELECT TOP 1 BoxId FROM Box WHERE BoxName = 'LARGE-B'  ORDER BY BoxId);
DECLARE @MedAId   INT = (SELECT TOP 1 BoxId FROM Box WHERE BoxName = 'MED-A'     ORDER BY BoxId);
DECLARE @SmallAId INT = (SELECT TOP 1 BoxId FROM Box WHERE BoxName = 'SMALL-A'   ORDER BY BoxId);
DECLARE @FragSId  INT = (SELECT TOP 1 BoxId FROM Box WHERE BoxName = 'FRAGILE-S' ORDER BY BoxId);

-- ── 6. PackingJobBox (כמויות) ────────────────────────────────
INSERT INTO PackingJobBox (JobId, BoxId, Quantity) VALUES
    (@JobId, @LargeBId,  3),
    (@JobId, @MedAId,   28),
    (@JobId, @SmallAId, 61),
    (@JobId, @FragSId,  48);

-- ── 7. PlacementResult ──────────────────────────────────────
-- עמודות: JobId, BoxId, InstanceIndex, BinIndex,
--          PosX, PosY, PosZ, PlacedWidth, PlacedHeight, PlacedDepth,
--          RotationIndex, CreatedAt
-- BinIndex = 0 (מכולה יחידה)

DECLARE @Now DATETIME2 = GETUTCDATE();

-- ════════════════════════════════════════════════════════════
-- LARGE-B  (W=200 H=120 D=150)
-- שלושה ארגזים גדולים בחזית, Y=0..120, Z=0..150
-- ════════════════════════════════════════════════════════════
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
VALUES
    (@JobId, @LargeBId, 0, 0,   0, 0, 0, 200, 120, 150, 0, @Now),
    (@JobId, @LargeBId, 1, 0, 200, 0, 0, 200, 120, 150, 0, @Now),
    (@JobId, @LargeBId, 2, 0, 400, 0, 0, 200, 120, 150, 0, @Now);

-- ════════════════════════════════════════════════════════════
-- MED-A  (W=80 H=80 D=80)
-- שורה תחתית-אחורית: Y=0, Z=150, X=0..480   (idx 0-6)
-- שכבת אמצע:          Y=120, Z=0/80/160       (idx 7-27)
-- ════════════════════════════════════════════════════════════
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @MedAId,
       ROW_NUMBER() OVER (ORDER BY PosX) - 1,
       0, PosX, 0, 150, 80, 80, 80, 0, @Now
FROM (VALUES (0),(80),(160),(240),(320),(400),(480)) AS t(PosX);

INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @MedAId,
       6 + ROW_NUMBER() OVER (ORDER BY PosZ, PosX),
       0, PosX, 120, PosZ, 80, 80, 80, 0, @Now
FROM (VALUES (0),(80),(160),(240),(320),(400),(480)) AS x(PosX)
CROSS JOIN (VALUES (0),(80),(160)) AS z(PosZ);

-- ════════════════════════════════════════════════════════════
-- SMALL-A  (W=40 H=40 D=40)
-- א. רצועה עליונה-אחורית: Y=80, Z=150 ו-Z=190  (idx 0-29)
-- ב. טלאי צדדי:            X=560, Y=0-80        (idx 30-33)
-- ג. טלאי אמצע:            X=560, Y=120-200      (idx 34-45)
-- ד. גב עליון:             Y=200, Z=200          (idx 46-60)
-- ════════════════════════════════════════════════════════════

-- א
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @SmallAId,
       ROW_NUMBER() OVER (ORDER BY PosZ, PosX) - 1,
       0, PosX, 80, PosZ, 40, 40, 40, 0, @Now
FROM (VALUES (0),(40),(80),(120),(160),(200),(240),(280),(320),(360),(400),(440),(480),(520),(560)) AS x(PosX)
CROSS JOIN (VALUES (150),(190)) AS z(PosZ);

-- ב
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @SmallAId,
       29 + ROW_NUMBER() OVER (ORDER BY PosZ, PosY),
       0, 560, PosY, PosZ, 40, 40, 40, 0, @Now
FROM (VALUES (0),(40)) AS y(PosY)
CROSS JOIN (VALUES (150),(190)) AS z(PosZ);

-- ג
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @SmallAId,
       33 + ROW_NUMBER() OVER (ORDER BY PosY, PosZ),
       0, 560, PosY, PosZ, 40, 40, 40, 0, @Now
FROM (VALUES (120),(160)) AS y(PosY)
CROSS JOIN (VALUES (0),(40),(80),(120),(160),(200)) AS z(PosZ);

-- ד
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @SmallAId,
       45 + ROW_NUMBER() OVER (ORDER BY PosX),
       0, PosX, 200, 200, 40, 40, 40, 0, @Now
FROM (VALUES (0),(40),(80),(120),(160),(200),(240),(280),(320),(360),(400),(440),(480),(520),(560)) AS x(PosX);

-- ════════════════════════════════════════════════════════════
-- FRAGILE-S  (W=50 H=40 D=50)
-- שכבה עליונה שבירה: Y=200, Z=0/50/100/150  (idx 0-47)
-- כלום לא מונח עליהם — Y2 = 240 = גובה המכולה
-- ════════════════════════════════════════════════════════════
INSERT INTO PlacementResult
    (JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ,
     PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt)
SELECT @JobId, @FragSId,
       ROW_NUMBER() OVER (ORDER BY PosZ, PosX) - 1,
       0, PosX, 200, PosZ, 50, 40, 50, 0, @Now
FROM (VALUES (0),(50),(100),(150),(200),(250),(300),(350),(400),(450),(500),(550)) AS x(PosX)
CROSS JOIN (VALUES (0),(50),(100),(150)) AS z(PosZ);

COMMIT;

PRINT N'✓ Demo job created. JobId = ' + CAST(@JobId AS NVARCHAR(10));
PRINT N'  140 ארגזים | מכולה 600×240×240 | ניצול נפח: 97.9%';
PRINT N'  הצג בממשק: בחר את המשרת "Demo – מכולה מלאה"';
