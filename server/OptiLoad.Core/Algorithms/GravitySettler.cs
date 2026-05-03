using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// כוח משיכה + הידוק: מוריד כל ארגז ב-Y ומקרב אותו ל-X=0 ו-Z=0.
    ///
    /// אחרי ש-B&B מסיים, ארגזים עלולים לשבת עם רווחים בין ביניהם בכל הכיוונים
    /// בגלל מגבלות Corner Points.
    /// Settle מבצע 3 passes: הידוק ב-X, הידוק ב-Z, כבידה ב-Y — 2 פעמים.
    ///
    /// סדר עיבוד: בכל ציר — מהקצה הקרוב ל-0 ראשון.
    /// אילוץ שבירות: ארגז לא-שביר לא ייצנח מתחת לארגז שביר (ב-Y בלבד).
    /// </summary>
    public static class GravitySettler
    {
        private const double Epsilon = 1e-9;

        public static List<PlacedBox> Settle(List<PlacedBox> boxes, ContainerDimensions? container = null)
        {
            if (boxes.Count == 0) return boxes;

            var current = boxes
                .Select(b => new PlacedBox(b.Instance, b.Position, b.Rotation) { BinIndex = b.BinIndex })
                .ToList();

            // passes של: X-compact → Z-compact → Y-gravity עד שאין שינוי (מקסימום 10)
            const int MaxIterations = 10;
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                // שמור מיקומים לפני ה-pass
                var prevPos = current.ToDictionary(
                    b => b.Instance,
                    b => (b.X1, b.Y1, b.Z1));

                current = CompactX(current, container);
                current = CompactZ(current, container);
                current = CompactY(current, container);

                // בדוק אם משהו זז
                bool changed = current.Any(b =>
                    prevPos.TryGetValue(b.Instance, out var p) && (
                        Math.Abs(b.X1 - p.X1) > Epsilon ||
                        Math.Abs(b.Y1 - p.Y1) > Epsilon ||
                        Math.Abs(b.Z1 - p.Z1) > Epsilon));

                if (!changed) break;
            }

            return current;
        }

        // ── הידוק לכיוון X=0 ──────────────────────────────────────────
        private static List<PlacedBox> CompactX(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result  = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.X1))
            {
                double newX = 0.0;
                foreach (var placed in result)
                {
                    bool overlapY = box.Y1 < placed.Y2 - Epsilon && placed.Y1 < box.Y2 - Epsilon;
                    bool overlapZ = box.Z1 < placed.Z2 - Epsilon && placed.Z1 < box.Z2 - Epsilon;
                    if (overlapY && overlapZ)
                        newX = Math.Max(newX, placed.X2);
                }
                // בדיקת גבולות: אם החדש חורג מהמכולה, שמור על המיקום המקורי
                if (container != null && newX + box.Rotation.W > container.Width + Epsilon)
                    newX = box.X1;
                result.Add(new PlacedBox(box.Instance, new Position3D(newX, box.Y1, box.Z1), box.Rotation)
                    { BinIndex = box.BinIndex });
            }
            return result;
        }

        // ── הידוק לכיוון Z=0 ──────────────────────────────────────────
        private static List<PlacedBox> CompactZ(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.Z1))
            {
                double newZ = 0.0;
                foreach (var placed in result)
                {
                    bool overlapX = box.X1 < placed.X2 - Epsilon && placed.X1 < box.X2 - Epsilon;
                    bool overlapY = box.Y1 < placed.Y2 - Epsilon && placed.Y1 < box.Y2 - Epsilon;
                    if (overlapX && overlapY)
                        newZ = Math.Max(newZ, placed.Z2);
                }
                // בדיקת גבולות: אם החדש חורג מהמכולה, שמור על המיקום המקורי
                if (container != null && newZ + box.Rotation.D > container.Depth + Epsilon)
                    newZ = box.Z1;
                result.Add(new PlacedBox(box.Instance, new Position3D(box.X1, box.Y1, newZ), box.Rotation)
                    { BinIndex = box.BinIndex });
            }
            return result;
        }

        // ── כבידה ב-Y ─────────────────────────────────────────────────
        private static List<PlacedBox> CompactY(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.Y1))
            {
                double newY = FindYSupport(box, result);
                // בדיקת גבולות: אם החדש חורג מהמכולה, שמור על המיקום המקורי
                if (container != null && newY + box.Rotation.H > container.Height + Epsilon)
                    newY = box.Y1;
                result.Add(new PlacedBox(box.Instance, new Position3D(box.X1, newY, box.Z1), box.Rotation)
                    { BinIndex = box.BinIndex });
            }
            return result;
        }

        private static double FindYSupport(PlacedBox box, List<PlacedBox> belowBoxes)
        {
            double maxSupportY = 0.0;

            foreach (var other in belowBoxes)
            {
                // אילוץ שבירות: ארגז לא-שביר מעל ארגז שביר – אסור
                if (!box.Instance.BoxDefinition.IsFragile &&
                     other.Instance.BoxDefinition.IsFragile)
                    continue;

                bool overlapX = box.X1 < other.X2 - Epsilon && other.X1 < box.X2 - Epsilon;
                bool overlapZ = box.Z1 < other.Z2 - Epsilon && other.Z1 < box.Z2 - Epsilon;

                if (overlapX && overlapZ)
                    maxSupportY = Math.Max(maxSupportY, other.Y2);
            }

            return maxSupportY;
        }
    }
}
