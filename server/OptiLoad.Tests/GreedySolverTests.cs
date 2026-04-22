using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class GreedySolverTests
{
    // ─── עזרים ───────────────────────────────────────────────────────────
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx = 0,
        bool allowRotation = false) => new()
    {
        BoxDefinition = new Box
        {
            BoxId         = idx,
            BoxName       = $"B{idx}",
            Width         = w,
            Height        = h,
            Depth         = d,
            AllowRotation = allowRotation
        },
        InstanceIndex = idx
    };

    // ─────────────────────────────────────────────────────────────────────
    // TEST 10
    // ארגז אחד שמתאים למכולה — מוכנס ללא בעיה, רשימת unplaced ריקה.
    // משמעות: מקרה בסיס — GreedySolver מצליח לשבץ את הארגז הכי פשוט.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void FillRemaining_SingleFittingBox_NoUnplacedBoxes()
    {
        var container = MakeContainer(100, 100, 100);
        var boxes     = new List<BoxInstance> { MakeInstance(10, 10, 10) };
        var openBins  = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Empty(unplaced);
        Assert.Single(openBins); // נפתחה מכולה אחת חדשה
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 11
    // מספר ארגזים קטנים שכולם נכנסים למכולה אחת — לא נפתחת מכולה שנייה.
    // משמעות: Greedy אורז ביעילות ולא פותח מכולה מיותרת.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void FillRemaining_MultipleSmallBoxes_AllFitInOneBin()
    {
        var container = MakeContainer(100, 100, 100);
        var boxes = Enumerable.Range(0, 8)
            .Select(i => MakeInstance(50, 50, 50, i))
            .ToList();
        var openBins = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Empty(unplaced);
        Assert.Single(openBins); // 8 ארגזים של 50x50x50 = נפח 8*125000 = 1000000 = בדיוק נפח המכולה
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 12
    // ארגז גדול מהמכולה בכיוון כלשהו — GreedySolver מחזיר אותו כ-unplaced.
    // משמעות: אי-אפשר לשבץ ארגז שלא נכנס — הפונקציה לא תקרוס אלא תדווח.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void FillRemaining_BoxLargerThanContainer_ReturnsUnplaced()
    {
        var container = MakeContainer(10, 10, 10);
        var boxes     = new List<BoxInstance> { MakeInstance(20, 5, 5) }; // 20 > 10
        var openBins  = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Single(unplaced);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 13
    // ארגזים ממשיכים לבין פתוח קיים — לא נפתחת מכולה נוספת מיותרת.
    // משמעות: GreedySolver בונה על מכולות שה-B&B פתח, לא פותח מכולה חדשה לשווא.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void FillRemaining_WithExistingOpenBin_DoesNotOpenNewBin()
    {
        var container = MakeContainer(100, 100, 100);

        // מכולה פתוחה קיימת עם ארגז קטן שמשאיר הרבה מקום
        var existingBin = new PackingState();
        var existingBox = new PlacedBox(
            MakeInstance(10, 10, 10, idx: 0),
            new Position3D(0, 0, 0),
            new Rotation(10, 10, 10, 0));
        existingBin.AddBox(existingBox);

        var openBins = new List<PackingState> { existingBin };
        var newBoxes = new List<BoxInstance> { MakeInstance(10, 10, 10, idx: 1) };

        GreedySolver.FillRemaining(newBoxes, openBins, container);

        Assert.Single(openBins); // הארגז נכנס למכולה הקיימת — לא נפתחה אחת חדשה
    }
}
