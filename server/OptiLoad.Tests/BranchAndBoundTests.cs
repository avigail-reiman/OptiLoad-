using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class BranchAndBoundTests
{
    // ─── עזרים ───────────────────────────────────────────────────────────
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx,
        bool fragile = false, bool allowRotation = false) => new()
    {
        BoxDefinition = new Box
        {
            BoxId         = idx,
            BoxName       = $"B{idx}",
            Width         = w,
            Height        = h,
            Depth         = d,
            IsFragile     = fragile,
            AllowRotation = allowRotation
        },
        InstanceIndex = idx
    };

    // ─────────────────────────────────────────────────────────────────────
    // TEST 14
    // קלט ריק — מוחזרת תוצאה עם 0 מכולות, ללא קריסה.
    // משמעות: מקרה קצה שמטרתו לוודא שה-API מחזיר תוצאה תקינה גם ללא קלט.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_EmptyInput_ReturnsZeroBins()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(100, 100, 100));

        var result = solver.Solve(Enumerable.Empty<BoxInstance>());

        Assert.Equal(0, result.BinsUsed);
        Assert.Empty(result.PlacedBoxes);
        Assert.Empty(result.UnplacedBoxes);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 15
    // ארגז אחד קטן ממכולה — מוכנס בדיוק במכולה אחת.
    // משמעות: מקרה בסיס — פתרון אופטימלי מובן מאליו הוא 1 מכולה.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_SingleSmallBox_UsesOneBin()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(100, 100, 100));
        var boxes  = new[] { MakeInstance(10, 10, 10, 0) };

        var result = solver.Solve(boxes);

        Assert.Equal(1, result.BinsUsed);
        Assert.Single(result.PlacedBoxes);
        Assert.Empty(result.UnplacedBoxes);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 16
    // שני ארגזים שמחצית מהמכולה כל אחד — שניהם נכנסים למכולה אחת.
    // משמעות: B&B מוצא פתרון אופטימלי (1 מכולה ולא 2).
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_TwoHalfContainerBoxes_UsesOneBin()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);
        var boxes = new[]
        {
            MakeInstance(100, 100, 50, 0), // חצי מכולה
            MakeInstance(100, 100, 50, 1)  // חצי מכולה
        };

        var result = solver.Solve(boxes);

        Assert.Equal(1, result.BinsUsed);
        Assert.Equal(2, result.PlacedBoxes.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 17
    // ארגז גדול ממכולה — מוחזר כ-unplaced, לא קורס האלגוריתם.
    // משמעות: הקוד מתמודד עם בקשה בלתי-אפשרית בלי exception.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_BoxBiggerThanContainer_ReturnsUnplaced()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(10, 10, 10));
        var boxes  = new[] { MakeInstance(20, 20, 20, 0) };

        var result = solver.Solve(boxes);

        Assert.Single(result.UnplacedBoxes);
        Assert.Empty(result.PlacedBoxes);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 18
    // ארגז שביר נמצא גבוה יותר מארגז לא-שביר שמתחתיו.
    // משמעות: אוכף את כלל "שביר תמיד מעל לא-שביר" — ליב של הפרויקט.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_FragileBox_PlacedAboveNonFragileBox()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);

        var boxes = new[]
        {
            MakeInstance(100, 50, 100, idx: 0, fragile: false), // לא-שביר, גובה 50
            MakeInstance(100, 30, 100, idx: 1, fragile: true)   // שביר, גובה 30
        };

        var result = solver.Solve(boxes);

        Assert.Equal(2, result.PlacedBoxes.Count);

        var nonFragilePlaced = result.PlacedBoxes.First(p => !p.Instance.BoxDefinition.IsFragile);
        var fragilePlaced    = result.PlacedBoxes.First(p =>  p.Instance.BoxDefinition.IsFragile);

        // הארגז השביר חייב להיות מעל (Y1 שלו >= Y2 של הלא-שביר, באותה מכולה)
        bool fragileIsAbove = fragilePlaced.Y1 >= nonFragilePlaced.Y2 - 1e-9;
        Assert.True(fragileIsAbove,
            $"Fragile Y1={fragilePlaced.Y1} should be >= NonFragile Y2={nonFragilePlaced.Y2}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 19
    // שני ארגזים שכל אחד גדול מחצי — חייבים שתי מכולות.
    // משמעות: B&B לא צונח לתוצאה שגויה של מכולה אחת כשלא ניתן.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_TwoBoxesThatCannotShareBin_UsesTwoBins()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);
        var boxes = new[]
        {
            MakeInstance(100, 100, 60, 0), // 60% עומק — לא תיכנס שנייה
            MakeInstance(100, 100, 60, 1)  // עוד 60% — חייב מכולה נפרדת
        };

        var result = solver.Solve(boxes);

        Assert.Equal(2, result.BinsUsed);
        Assert.Equal(2, result.PlacedBoxes.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 20
    // נתוני מבחן אמיתיים: מכולה 589×239×235.
    // שני ארגזים LARGE-B (200×120×150) חוסמים LARGE-FLAT (230×60×80) אם
    // מונחים ברוחב 200 (200+200=400 < 589 אבל נשאר 189 < 230).
    // עם סיבוב לרוחב 120: 120+120=240, נשאר 349 >= 230 → הכל נכנס.
    // משמעות: B&B עם min-X2 מוצא 1 מכולה ולא 2.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_RealContainerData_AllBoxesFitInOneBin()
    {
        // מכולה אמיתית: 589 רוחב × 239 גובה × 235 עומק
        var container = new ContainerDimensions
        {
            Width      = 589,
            Height     = 239,
            Depth      = 235,
            MaxWeightKg = 21770
        };
        var solver = new BranchAndBoundSolver(container);

        // LARGE-B ×2: 200×150×120 — עם סיבוב, W מינימלי = 120
        // LARGE-FLAT ×1: 230×80×60 — רוחב 230
        // קטנים ×7 שמתאימים בקלות
        var boxes = new List<BoxInstance>
        {
            // LARGE-B ×2 — מרכזי: אם W=200, חוסמים LARGE-FLAT; אם W=120, לא
            MakeInstance(200, 120, 150, 0, allowRotation: true),
            MakeInstance(200, 120, 150, 1, allowRotation: true),
            // LARGE-FLAT ×1
            MakeInstance(230,  80,  60, 2, allowRotation: true),
            // קטנים
            MakeInstance( 40,  50,  50, 3, allowRotation: true),
            MakeInstance( 40,  50,  50, 4, allowRotation: true),
            MakeInstance( 40,  50,  50, 5, allowRotation: true),
            MakeInstance( 70,  90,  50, 6, allowRotation: true),
            MakeInstance( 70,  90,  50, 7, allowRotation: true),
            MakeInstance( 20,  20,  50, 8, allowRotation: true),
            MakeInstance( 20,  20,  50, 9, allowRotation: true),
        };

        var result = solver.Solve(boxes);

        Assert.Equal(1,          result.BinsUsed);
        Assert.Equal(10,         result.PlacedBoxes.Count);
        Assert.Empty(result.UnplacedBoxes);
    }
}
