-- ============================================================
-- seed-uid-boxes.sql
-- 1) הוספת עמודת UID לטבלת Box (חד-פעמי, אידמפוטנטי)
-- 2) הכנסת 20 ארגזים חדשים 30×30×30 עם מזהי UID פיזיים
-- ============================================================
-- הרצה:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d OptiLoadDB -i seed-uid-boxes.sql

SET NOCOUNT ON;

-- ─── שלב 1: הוספת עמודת UID ─────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Box' AND COLUMN_NAME = 'UID'
)
BEGIN
    ALTER TABLE Box ADD UID NVARCHAR(20) NULL;
    PRINT 'עמודת UID נוספה לטבלת Box.';
END
ELSE
    PRINT 'עמודת UID קיימת כבר.';
GO

-- ─── שלב 2: הכנסת 20 ארגזים (אידמפוטנטי לפי BoxName) ───────────────────────
SET NOCOUNT ON;

MERGE INTO Box AS target
USING (VALUES
--   BoxName           W    H    D    Kg  Frag  Rot   UID
    ('UID-50D1113B',  30,  30,  30,  1,   0,    1,   '50D1113B'),
    ('UID-E0CF123B',  30,  30,  30,  1,   0,    1,   'E0CF123B'),
    ('UID-50D5113B',  30,  30,  30,  1,   0,    1,   '50D5113B'),
    ('UID-6056113B',  30,  30,  30,  1,   0,    1,   '6056113B'),
    ('UID-1049762C',  30,  30,  30,  1,   0,    1,   '1049762C'),
    ('UID-808C752C',  30,  30,  30,  1,   0,    1,   '808C752C'),
    ('UID-B071103B',  30,  30,  30,  1,   0,    1,   'B071103B'),
    ('UID-20FC0F3B',  30,  30,  30,  1,   0,    1,   '20FC0F3B'),
    ('UID-7075103B',  30,  30,  30,  1,   0,    1,   '7075103B'),
    ('UID-D0120F3B',  30,  30,  30,  1,   0,    1,   'D0120F3B'),
    ('UID-704E113B',  30,  30,  30,  1,   0,    1,   '704E113B'),
    ('UID-60CD113B',  30,  30,  30,  1,   0,    1,   '60CD113B'),
    ('UID-C003103B',  30,  30,  30,  1,   0,    1,   'C003103B'),
    ('UID-1078103B',  30,  30,  30,  1,   0,    1,   '1078103B'),
    ('UID-E0D3123B',  30,  30,  30,  1,   0,    1,   'E0D3123B'),
    ('UID-405A113B',  30,  30,  30,  1,   0,    1,   '405A113B'),
    ('UID-E0CB123B',  30,  30,  30,  1,   0,    1,   'E0CB123B'),
    ('UID-7052113B',  30,  30,  30,  1,   0,    1,   '7052113B'),
    ('UID-100F0F3B',  30,  30,  30,  1,   0,    1,   '100F0F3B'),
    ('UID-2000103B',  30,  30,  30,  1,   0,    1,   '2000103B')
) AS source (BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, UID)
ON target.BoxName = source.BoxName
WHEN NOT MATCHED THEN
    INSERT (BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, UID, CreatedAt)
    VALUES (source.BoxName, source.Width, source.Height, source.Depth,
            source.WeightKg, source.IsFragile, source.AllowRotation, source.UID, GETUTCDATE())
WHEN MATCHED THEN
    UPDATE SET UID = source.UID;

PRINT '20 ארגזים עם UID הוכנסו/עודכנו בהצלחה.';

-- ─── תצוגת תוצאה ─────────────────────────────────────────────────────────────
SELECT BoxId, BoxName, Width, Height, Depth, WeightKg, UID
FROM   Box
WHERE  BoxName LIKE 'UID-%'
ORDER  BY BoxId;
