using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class BoxTests
{
    // ─── עזר: יצירת ארגז פשוט ─────────────────────────────────────────
    private static Box MakeBox(double w, double h, double d, bool allowRotation = true) => new()
    {
        BoxId         = 1,
        BoxName       = "TestBox",
        Width         = w,
        Height        = h,
        Depth         = d,
        AllowRotation = allowRotation
    };

    // ─────────────────────────────────────────────────────────────────────
    // TEST 1
    // כשסיבוב מושבת — מוחזר רק כיוון אחד (המקורי).
    // משמעות: ארגז שביר שצריך לעמוד זקוף לא יסתובב לתוצאה שגויה.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GetAllowedRotations_WhenRotationDisabled_ReturnsSingleRotation()
    {
        var box = MakeBox(10, 20, 30, allowRotation: false);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Single(rotations);
        Assert.Equal(10, rotations[0].W);
        Assert.Equal(20, rotations[0].H);
        Assert.Equal(30, rotations[0].D);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 2
    // קוביה (W=H=D) — לכל 6 הסיבובים המימדים זהים, אז מוחזר רק סיבוב 1.
    // משמעות: ללא deduplication היינו בודקים 6 מצבים זהים לשווא.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GetAllowedRotations_CubeBox_ReturnsExactlyOneUniqueRotation()
    {
        var box = MakeBox(5, 5, 5);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Single(rotations);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 3
    // ארגז עם שלושה מימדים שונים — יש בדיוק 6 כיוני סיבוב ייחודיים.
    // משמעות: האלגוריתם בוחן את כל הכיוונים האפשריים ולא פחות.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GetAllowedRotations_DistinctDimensions_ReturnsSixUniqueRotations()
    {
        var box = MakeBox(1, 2, 3);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Equal(6, rotations.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 4
    // ארגז עם W=H≠D — יש בדיוק 3 כיוני סיבוב ייחודיים (לא 6).
    // משמעות: deduplication עובד נכון גם עם מימדים חלקית זהים.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GetAllowedRotations_TwoEqualDimensions_ReturnsThreeUniqueRotations()
    {
        var box = MakeBox(2, 2, 5);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Equal(3, rotations.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEST 5
    // נפח הסיבוב זהה לנפח המקורי — בכל כיוון.
    // משמעות: סיבוב לא "מאבד" או "מוסיף" נפח (שגיאה קריטית שתשבש L0).
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GetAllowedRotations_VolumeIsPreservedAcrossAllRotations()
    {
        var box = MakeBox(2, 3, 7);
        double expectedVolume = 2 * 3 * 7;

        foreach (var rot in box.GetAllowedRotations())
        {
            Assert.Equal(expectedVolume, rot.Volume, precision: 9);
        }
    }
}
