using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// אלגוריתם חמדני לשיבוץ מהיר של ארגזים נותרים.
    ///
    /// מופעל כשה-B&B חורג ממגבלת הזמן, כדי להבטיח שכל הארגזים
    /// יקבלו מענה ולא תוחזר תוצאה חלקית.
    ///
    /// עיקרון: עבור כל ארגז נותר —
    ///   1. נסה להכניסו למכולה פתוחה קיימת (נקודת פינה ראשונה שמתאימה)
    ///   2. אם לא נכנס לאף מכולה — פתח מכולה חדשה
    ///   3. אם לא נכנס גם במכולה חדשה — הוסף ל-UnplacedBoxes
    ///
    /// אין backtracking, אין חיפוש — החלטה אחת לכל ארגז.
    /// לא מבטיח פתרון אופטימלי אבל מהיר מאוד ומבטיח פתרון מלא.
    /// </summary>
    public static class GreedySolver
    {
        private const double Epsilon = 1e-9;

        // ─────────────────────────────────────────────────────────────────
        // ממשק ציבורי
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// משבץ את כל הארגזים הנותרים בצורה חמדנית.
        /// מעדכן את openBins במקום ומחזיר רשימת ארגזים שלא הצליחו להיכנס.
        /// </summary>
        /// <param name="remainingBoxes">ארגזים שה-B&B לא הספיק לשבץ</param>
        /// <param name="openBins">מכולות פתוחות מה-B&B – ימשיך לבנות עליהן</param>
        /// <param name="container">מידות המכולה</param>
        /// <param name="maxFillHeightRatio">גובה מילוי מקסימלי יחסי</param>
        /// <returns>ארגזים שלא הצליחו להיכנס לשום מכולה</returns>
        public static List<BoxInstance> FillRemaining(
            List<BoxInstance>   remainingBoxes,
            List<PackingState>  openBins,
            ContainerDimensions container,
            double              maxFillHeightRatio = 1.0)
        {
            var unplaced = new List<BoxInstance>();

            // מיין לפי נפח יורד — ארגזים גדולים קודם (מייעל ניצול)
            var sorted = remainingBoxes
                .OrderByDescending(b => b.BoxDefinition.Volume)
                .ToList();

            foreach (var instance in sorted)
            {
                bool placed = false;

                // ── נסה מכולות פתוחות קיימות ── לפי מלאות יורדת (הכי מלאה קודם)
                foreach (var bin in openBins.OrderByDescending(b => b.UsedVolume))
                {
                    if (TryPlaceGreedy(instance, bin, container, maxFillHeightRatio))
                    {
                        placed = true;
                        break;
                    }
                }

                // ── פתח מכולה חדשה אם לא נכנס ──
                if (!placed)
                {
                    var newBin = new PackingState();
                    if (TryPlaceGreedy(instance, newBin, container, maxFillHeightRatio))
                    {
                        openBins.Add(newBin);
                        placed = true;
                    }
                }

                // ── ארגז שלא נכנס בשום פנים ──
                if (!placed)
                    unplaced.Add(instance);
            }

            return unplaced;
        }

        // ─────────────────────────────────────────────────────────────────
        // ניסיון מיקום חמדני במכולה
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מנסה למקם ארגז במכולה נתונה.
        /// מחפש את נקודת הפינה הראשונה שמתאימה בכל אחד מהכיוונים.
        /// מחזיר true ומעדכן את המכולה אם הצליח.
        /// </summary>
        private static bool TryPlaceGreedy(
            BoxInstance         instance,
            PackingState        bin,
            ContainerDimensions container,
            double              maxFillHeightRatio)
        {
            // מצא נקודות פינה במצב הנוכחי של המכולה
            var corners = CornerPointsFinder.Find3DCorners(
                bin.PlacedBoxes,
                container);

            bool isFragile = instance.BoxDefinition.IsFragile;

            // מיין נקודות פינה לפי Y עולה — תמיד נסה הנמוכה ביותר קודם
            var sortedCorners = corners.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();

            foreach (var rotation in instance.BoxDefinition.GetAllowedRotations())
            {
                foreach (var corner in sortedCorners)
                {
                    if (IsValidPlacement(instance, corner, rotation,
                                         bin, container, maxFillHeightRatio, isFragile))
                    {
                        var placed = new PlacedBox(instance, corner, rotation);
                        bin.AddBox(placed);
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────
        // בדיקת תקינות מיקום
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// בודק שהמיקום תקין: גבולות מכולה, גובה מקסימלי, משקל,
        /// אי-חפיפה, ואילוץ שבירות.
        /// </summary>
        private static bool IsValidPlacement(
            BoxInstance         instance,
            Position3D          corner,
            Rotation            rotation,
            PackingState        bin,
            ContainerDimensions container,
            double              maxFillHeightRatio,
            bool                isFragile)
        {
            double maxHeight = container.Height * maxFillHeightRatio;

            // ── גבולות מכולה ──
            if (corner.X + rotation.W > container.Width  + Epsilon ||
                corner.Y + rotation.H > maxHeight        + Epsilon ||
                corner.Z + rotation.D > container.Depth  + Epsilon)
                return false;

            // ── משקל ──
            if (bin.UsedWeightKg + instance.BoxDefinition.WeightKg >
                container.MaxWeightKg + Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

            // ── אי-חפיפה ──
            foreach (var existing in bin.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }

            // ── אילוץ שבירות: ארגז שביר לא יכנס מתחת לארגז לא-שביר ──
            // מותר: שביר *מעל* לא-שביר. אסור: שביר *מתחת* לא-שביר.
            if (isFragile)
            {
                double candX1 = corner.X, candX2 = corner.X + rotation.W;
                double candY1 = corner.Y, candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z, candZ2 = corner.Z + rotation.D;

                foreach (var existing in bin.PlacedBoxes)
                {
                    if (existing.Instance.BoxDefinition.IsFragile) continue;

                    // הלא-שביר נמצא מעל תחתית השביר → השביר מתחת → אסור
                    if (existing.Y1 < candY2 - Epsilon &&
                        existing.Y2 > candY1 + Epsilon &&
                        existing.Y1 >= candY1 - Epsilon)
                    {
                        bool overlapX = existing.X1 < candX2 - Epsilon &&
                                        existing.X2 > candX1 + Epsilon;
                        if (!overlapX) continue;

                        bool overlapZ = existing.Z1 < candZ2 - Epsilon &&
                                        existing.Z2 > candZ1 + Epsilon;

                        if (overlapZ) return false;
                    }
                }
            }

            return true;
        }
    }
}
