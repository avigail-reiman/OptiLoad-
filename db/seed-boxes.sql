-- ============================================================
-- seed-boxes.sql
-- הכנסת נתוני ארגזים לטבלת Box
-- DB: OptiLoadDB (LocalDB)
-- ============================================================
-- הרצה: sqlcmd -S "(localdb)\MSSQLLocalDB" -d OptiLoadDB -i seed-boxes.sql

SET NOCOUNT ON;

-- סקריפט אידמפוטנטי: מוסיף רק ארגזים שעדיין לא קיימים לפי שם
MERGE INTO Box AS target
USING (VALUES
--  BoxName        Width  Height  Depth  WeightKg  IsFragile  AllowRotation
    ('TINY-A',      20,    20,     20,    1,         0,         1),
    ('TINY-B',      30,    20,     25,    2,         0,         1),
    ('SMALL-A',     40,    40,     40,    4,         0,         1),
    ('SMALL-B',     60,    50,     55,    7,         0,         1),
    ('SMALL-C',     70,    40,     60,    8,         0,         1),
    ('MED-A',       80,    80,     80,   15,         0,         1),
    ('MED-B',      100,    90,    100,   20,         0,         1),
    ('MED-TALL',    90,   150,     90,   22,         0,         1),
    ('MED-WIDE',   140,    80,    100,   18,         0,         1),
    ('LARGE-A',    150,   100,    120,   35,         0,         1),
    ('LARGE-B',    200,   120,    150,   50,         0,         1),
    ('LARGE-FLAT', 230,    60,    180,   40,         0,         1),
    ('FRAGILE-S',   50,    40,     50,    3,         1,         1),
    ('FRAGILE-M',   90,    70,     90,   10,         1,         1),
    ('XXX',         30,    30,     30,    3,         0,         1),
    ('X',          300,   300,    300,    5,         0,         1)
) AS source (BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation)
ON target.BoxName = source.BoxName
WHEN NOT MATCHED THEN
    INSERT (BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, CreatedAt)
    VALUES (source.BoxName, source.Width, source.Height, source.Depth,
            source.WeightKg, source.IsFragile, source.AllowRotation, GETUTCDATE());

SELECT BoxId, BoxName, Width, Height, Depth, WeightKg, IsFragile
FROM   Box
ORDER  BY BoxId;
