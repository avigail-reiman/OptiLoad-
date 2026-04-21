using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// מוצא נקודות פינה (Corner Points) בתלת-ממד.
    ///
    /// בסיס: Martello et al. (1997), סעיפים 4 ו-4.1.
    ///
    /// Property 1: בפתרון אופטימלי לא ניתן להזיז שום ארגז שמאלה, מטה או לאחור.
    /// לכן, כל ארגז חייב לגעת בקיר או בקצה ארגז קיים.
    ///
    /// נקודות פינה הן הנקודות שבהן המעטפת עוברת ממגמה אנכית לאופקית.
    /// רק בנקודות אלו כדאי לנסות למקם ארגז הבא.
    /// </summary>
    public static class CornerPointsFinder
    {
        // ─────────────────────────────────────────────────────────────────
        // 2D-CORNERS – נקודות פינה במישור XY (לשכבה אחת)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מוצא נקודות פינה במישור XY עבור קבוצת ארגזים מוקרנים.
        /// אלגוריתם 2D-CORNERS מהמאמר – שלושה שלבים.
        /// </summary>
        /// <param name="projectedBoxes">ארגזים מוקרנים: (x1,x2,y1,y2)</param>
        /// <param name="W">רוחב מכולה</param>
        /// <param name="H">גובה מכולה</param>
        /// <param name="remainingBoxes">ארגזים שעוד לא מוקמו (לסינון נקודות)</param>
        private static List<(double x, double y)> Find2DCorners(
            List<(double x1, double x2, double y1, double y2)> projectedBoxes,
            double W, double H,
            List<(double minW, double minH)>? remainingBoxes = null)
        {
            if (projectedBoxes.Count == 0)
                return new List<(double, double)> { (0.0, 0.0) };

            // כל ערכי X האפשריים: 0 + כל x2 של ארגז ממוקם
            var candidateX = new SortedSet<double> { 0.0 };
            // כל ערכי Y האפשריים: 0 + כל y2 של ארגז ממוקם
            var candidateY = new SortedSet<double> { 0.0 };

            foreach (var b in projectedBoxes)
            {
                candidateX.Add(b.x2);
                candidateY.Add(b.y2);
            }

            // מכפלה קרטזית: כל שילוב (cx, cy) הוא מועמד לפינה
            var corners = new List<(double x, double y)>();
            foreach (double cx in candidateX)
                foreach (double cy in candidateY)
                    corners.Add((cx, cy));

            // הסרת נקודות לא-ישימות לפי גודל ארגזים נותרים
            if (remainingBoxes != null && remainingBoxes.Count > 0)
            {
                double minRemainingW = remainingBoxes.Min(r => r.minW);
                double minRemainingH = remainingBoxes.Min(r => r.minH);

                corners = corners
                    .Where(c => c.x + minRemainingW <= W && c.y + minRemainingH <= H)
                    .ToList();
            }

            return corners;
        }

        // ─────────────────────────────────────────────────────────────────
        // 3D-CORNERS – נקודות פינה בתלת-ממד
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מוצא נקודות פינה בתלת-ממד עבור קבוצת ארגזים שכבר שובצו.
        /// אלגוריתם 3D-CORNERS מהמאמר – מריץ 2D-CORNERS לכל ערך Z אפשרי.
        ///
        /// סיבוכיות: O(n²)
        /// </summary>
        /// <param name="placedBoxes">ארגזים שכבר שובצו</param>
        /// <param name="container">מידות המכולה</param>
        /// <param name="remainingInstances">ארגזים שטרם שובצו</param>
        public static List<Position3D> Find3DCorners(
            IReadOnlyList<PlacedBox>  placedBoxes,
            ContainerDimensions       container,
            IEnumerable<BoxInstance>? remainingInstances = null)
        {
            if (placedBoxes.Count == 0)
                return new List<Position3D> { new Position3D(0, 0, 0) };

            // קבע גדלי ארגזים נותרים לסינון
            var remaining = remainingInstances?
                .Select(b =>
                {
                    var box = b.BoxDefinition;
                    double minW, minH, minD;
                    if (box.AllowRotation)
                    {
                        var dims = new[] { box.Width, box.Height, box.Depth };
                        minW = dims.Min();
                        minH = dims.Min();
                        minD = dims.Min();
                    }
                    else
                    {
                        minW = box.Width;
                        minH = box.Height;
                        minD = box.Depth;
                    }
                    return (minW, minH, minD);
                })
                .ToList() ?? new List<(double, double, double)>();

            // ערכי Z אפשריים: 0 + כל z2 של ארגז ממוקם
            var zCoords = new SortedSet<double> { 0.0 };
            foreach (var pb in placedBoxes)
                zCoords.Add(pb.Z2);

            var result          = new List<Position3D>();
            List<(double x, double y)>? prevCorners2D = null;

            foreach (double z0 in zCoords)
            {
                // בדוק שיש ארגז שיוכל להיכנס אחרי z0
                if (remaining.Count > 0)
                {
                    double minD = remaining.Min(r => r.minD);
                    if (z0 + minD > container.Depth) break;
                }
                else if (z0 >= container.Depth) break;

                // ארגזים שמסתיימים אחרי z0 (zi + di > z0)
                var activeBoxes = placedBoxes
                    .Where(pb => pb.Z2 > z0)
                    .Select(pb => (x1: pb.X1, x2: pb.X2, y1: pb.Y1, y2: pb.Y2))
                    .ToList();

                var remainingForFilter = remaining
                    .Select(r => (r.minW, r.minH))
                    .ToList();

                var corners2D = Find2DCorners(
                    activeBoxes,
                    container.Width,
                    container.Height,
                    remainingForFilter.Count > 0 ? remainingForFilter : null);

                // הוסף את כל נקודות הפינה לשכבת ה-z הנוכחית.
                // TryPlace יסנן מיקומים לא-תקינים (חפיפה, שבירות וכו').
                foreach (var (cx, cy) in corners2D)
                    result.Add(new Position3D(cx, cy, z0));

                prevCorners2D = corners2D;
            }

            return result;
        }
    }
}
