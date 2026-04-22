using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class LowerBoundTests
{
    // ─── עזר ─────────────────────────────────────────────────────────────
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx = 0) => new()
    {
        BoxDefinition = new Box { BoxId = idx, BoxName = $"B{idx}", Width = w, Height = h, Depth = d },
        InstanceIndex = idx
    };

    // ─────────────────────────────────────────────────────────────────────
    // TEST 6
    // ארגז אחד שנפחו בדיוק מחצית נפח המכולה → L0 = 1.
    // משמעות: כאשר כל הארגזים נכנסים (תיאורטית) למכולה אחת —
    //         החסם התחתון הוא 1, כלומר לא ניתן לפתור ב-0 מכולות.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void ComputeL0_SingleBoxHalfVolume_ReturnsOne()
    {
        var container = MakeContainer(10, 10, 10); // נפח 1000
        var boxes     = new[] { MakeInstance(5, 10, 10) }; // נפח 500

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(1, l0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 7
    // שני ארגזים שנפחם הכולל חורג מנפח מכולה אחת → L0 = 2.
    // משמעות: החסם מבטיח שלעולם לא נקבל תוצאה של מכולה 1 כשאי-אפשר.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void ComputeL0_TwoBoxesExceedOneBin_ReturnsTwo()
    {
        var container = MakeContainer(10, 10, 10); // נפח 1000
        var boxes = new[]
        {
            MakeInstance(8, 10, 10, 0), // נפח 800
            MakeInstance(5, 10, 10, 1)  // נפח 500 — סה"כ 1300 > 1000
        };

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(2, l0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 8
    // ארגז גדול בדיוק כמו המכולה → L0 = 1.
    // גם נפח = 100% נחשב כ-1 מכולה (לא 2).
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void ComputeL0_BoxExactlyFillsContainer_ReturnsOne()
    {
        var container = MakeContainer(10, 10, 10);
        var boxes     = new[] { MakeInstance(10, 10, 10) };

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(1, l0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 9
    // ריצה על רשימה ריקה → L0 = 0.
    // משמעות: מקרה קצה — אין ארגזים, אין מכולות נדרשות.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void ComputeL0_EmptyBoxList_ReturnsZero()
    {
        var container = MakeContainer(10, 10, 10);

        int l0 = LowerBoundCalculator.ComputeL0(Enumerable.Empty<BoxInstance>(), container);

        Assert.Equal(0, l0);
    }
}
