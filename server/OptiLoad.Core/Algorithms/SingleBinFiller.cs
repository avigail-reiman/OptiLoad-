using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// מילוי מכולה יחידה באמצעות Branch & Bound.
    ///
    /// בסיס: Martello et al. (1997), סעיף 4.2.
    ///
    /// מטרה: בהינתן רשימת ארגזים, מצא כמה מהם ניתן לארגז במכולה אחת,
    /// תוך קביעת מיקום וכיוון מדויק לכל אחד. הפתרון ממקסם את הנפח הממולא.
    ///
    /// ── טיפול בארגזים שבירים ──────────────────────────────────────────
    /// החלוקה לשלבים מבטיחה שארגז לא-שביר לעולם לא יונח מעל ארגז שביר:
    ///
    ///   שלב 1 (fragilePhase=false):
    ///     מטפל אך ורק בארגזים שאינם שבירים. אין בדיקת שבירות.
    ///
    ///   שלב 2 (fragilePhase=true):
    ///     מקבל את מצב המכולה לאחר שלב 1 (existingState) ומוסיף ארגזים שבירים.
    ///     TryPlace בודק שאין ארגז לא-שביר קיים שחוצה את היטל XZ של המועמד
    ///     וגם חוסם אותו בציר Y – כלומר ארגז שביר לא יוכל להיכנס מתחת לארגז קיים.
    /// </summary>
    public class SingleBinFiller
    {
        // ─── קבועים ──────────────────────────────────────────────────────
        private const int    MaxNodes = 300_000;  // מגבלת צמתים
        private const double Epsilon  = 1e-9;    // סבילות השוואה

        /// <summary>
        /// גובה מילוי מקסימלי יחסי (0.0–1.0).
        /// ברירת מחדל: 1.0 = 100% מגובה המכולה.
        /// ניתן לשינוי לפני הרצה – למשל 0.8 לצורכי בטיחות הובלה.
        /// </summary>
        public double MaxFillHeightRatio { get; set; } = 1.0;

        // ─── נתוני ריצה ──────────────────────────────────────────────────
        private readonly ContainerDimensions _container;
        private int _nodeCount;

        public SingleBinFiller(ContainerDimensions container)
        {
            _container = container;
        }

        // ─────────────────────────────────────────────────────────────────
        // ממשק ציבורי
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מנסה למלא מכולה אחת מרשימת ארגזים.
        /// מחזיר את המצב הטוב ביותר שנמצא (לא בהכרח כולל את כולם).
        ///
        /// fragilePhase=false → שלב ראשון: שבץ ארגזים שאינם שבירים בלבד.
        /// fragilePhase=true  → שלב שני:   שבץ ארגזים שבירים מעל המצב הקיים.
        /// existingState      → מצב שלב 1 שממנו ממשיכים בשלב 2.
        /// </summary>
        public PackingState FillBin(
            IEnumerable<BoxInstance> instances,
            bool                     fragilePhase  = false,
            PackingState?            existingState = null)
        {
            // סנן לפי שלב: שלב 1 = לא-שבירים, שלב 2 = שבירים
            var filtered = instances
                .Where(b => b.BoxDefinition.IsFragile == fragilePhase)
                .OrderByDescending(b => b.BoxDefinition.Volume)
                .ToList();

            // בשלב 2 מתחילים מהמצב שנבנה בשלב 1
            var currentState = existingState?.Clone() ?? new PackingState();
            var bestState    = currentState.Clone();
            _nodeCount       = 0;

            SearchRecursive(filtered, currentState, ref bestState, fragilePhase);
            return bestState;
        }

        /// <summary>
        /// גרסה בינארית: האם ניתן למקם את כל הארגזים (שבירים + לא-שבירים) במכולה אחת?
        /// מריץ שני שלבים ברצף ובודק שכולם שובצו.
        /// </summary>
        public bool CanFitAll(IEnumerable<BoxInstance> instances)
        {
            var all = instances.ToList();

            // ── שלב 1: שבץ לא-שבירים ──
            var stateAfterPhase1 = FillBin(all, fragilePhase: false);

            var nonFragile = all.Where(b => !b.BoxDefinition.IsFragile).ToList();
            double requiredNonFragile = nonFragile.Sum(b => b.BoxDefinition.Volume);
            if (stateAfterPhase1.UsedVolume < requiredNonFragile - Epsilon)
                return false;  // לא כל הלא-שבירים נכנסו

            // ── שלב 2: שבץ שבירים מעל המצב הקיים ──
            var stateAfterPhase2 = FillBin(all, fragilePhase: true,
                                           existingState: stateAfterPhase1);

            var fragile = all.Where(b => b.BoxDefinition.IsFragile).ToList();
            double requiredFragile  = fragile.Sum(b => b.BoxDefinition.Volume);
            double addedFragileVol  = stateAfterPhase2.UsedVolume
                                    - stateAfterPhase1.UsedVolume;

            return addedFragileVol >= requiredFragile - Epsilon;
        }

        // ─────────────────────────────────────────────────────────────────
        // Branch & Bound רקורסיבי
        // ─────────────────────────────────────────────────────────────────

        private void SearchRecursive(
            List<BoxInstance> allBoxes,
            PackingState      current,
            ref PackingState  best,
            bool              fragilePhase)
        {
            _nodeCount++;
            if (_nodeCount > MaxNodes) return;

            // עדכן פתרון הטוב ביותר
            if (current.UsedVolume > best.UsedVolume)
                best = current.Clone();

            // חסם עליון: נפח נוכחי + נפח כל הנותרים
            var remaining = GetRemainingBoxes(allBoxes, current);
            double upperBound = current.UsedVolume +
                                remaining.Sum(b => b.BoxDefinition.Volume);

            // קיצוץ: אם upperBound לא יכול לשפר – חתוך
            if (upperBound <= best.UsedVolume + Epsilon) return;

            // מצא נקודות פינה פנויות
            var corners = CornerPointsFinder.Find3DCorners(
                current.PlacedBoxes,
                _container,
                remaining);

            if (corners.Count == 0) return;

            // ענף: לכל ארגז נותר × כל כיוון × כל פינה
            foreach (var instance in remaining)
            {
                foreach (var rotation in instance.BoxDefinition.GetAllowedRotations())
                {
                    foreach (var corner in corners)
                    {
                        if (_nodeCount > MaxNodes) return;

                        if (TryPlace(current, instance, corner, rotation,
                                     fragilePhase, out var placed))
                        {
                            current.AddBox(placed!);
                            SearchRecursive(allBoxes, current, ref best, fragilePhase);
                            current.RemoveLastBox();
                        }
                    }
                }
                break; // אופטימיזציה: ענף על ארגז ראשון זמין בלבד (כמו במאמר)
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ניסיון מיקום ארגז בנקודת פינה
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// גרסה ציבורית של TryPlace — לשימוש הדרגתי מ-BranchAndBoundSolver.
        /// מנסה למקם ארגז בנקודת פינה בכיוון נתון על מצב קיים.
        /// </summary>
        public bool TryPlaceBox(
            PackingState   state,
            BoxInstance    instance,
            Position3D     corner,
            Rotation       rotation,
            bool           fragilePhase,
            out PlacedBox? placed) =>
            TryPlace(state, instance, corner, rotation, fragilePhase, out placed);

        /// <summary>
        /// מנסה למקם ארגז בנקודת פינה בכיוון נתון.
        ///
        /// בדיקות כלליות (שני השלבים):
        ///   ─ גבולות מכולה
        ///   ─ משקל מקסימלי
        ///   ─ אי-חפיפה עם ארגזים קיימים
        ///
        /// בדיקה ייחודית לשלב 2 (fragilePhase=true):
        ///   ─ אין ארגז לא-שביר קיים שנמצא מעל המועמד בציר Y וחוצה אותו ב-XZ.
        ///     כלומר: ארגז שביר לא יכול להיכנס מתחת לארגז לא-שביר שכבר שובץ.
        /// </summary>
        private bool TryPlace(
            PackingState   state,
            BoxInstance    instance,
            Position3D     corner,
            Rotation       rotation,
            bool           fragilePhase,
            out PlacedBox? placed)
        {
            placed = null;

            // ── גבולות מכולה ──
            double maxAllowedHeight = _container.Height * MaxFillHeightRatio;
            if (corner.X + rotation.W > _container.Width  + Epsilon ||
                corner.Y + rotation.H > maxAllowedHeight  + Epsilon ||
                corner.Z + rotation.D > _container.Depth  + Epsilon)
                return false;

            // ── משקל ──
            if (state.UsedWeightKg + instance.BoxDefinition.WeightKg >
                _container.MaxWeightKg + Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

            // ── אי-חפיפה ──
            foreach (var existing in state.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }

            // ── בדיקת שבירות (שלב 2 בלבד) ──────────────────────────────
            // ארגז שביר לא יכול להיות *מתחת* לארגז לא-שביר.
            // אסור: existing (לא-שביר) מתחיל מעל תחתית השביר — כלומר השביר ייכנס מתחתיו.
            // מותר: השביר יושב *מעל* הלא-שביר (existing.Y2 <= candY1).
            if (fragilePhase)
            {
                double candX1 = corner.X,         candX2 = corner.X + rotation.W;
                double candY1 = corner.Y,         candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z,         candZ2 = corner.Z + rotation.D;

                foreach (var existing in state.PlacedBoxes)
                {
                    if (existing.Instance.BoxDefinition.IsFragile) continue;

                    // הלא-שביר נמצא מעל תחתית השביר → השביר מתחת לו → אסור
                    // (אם existing.Y2 <= candY1: הלא-שביר מסתיים מתחת לשביר → מותר)
                    if (existing.Y1 < candY2 - Epsilon &&
                        existing.Y2 > candY1 + Epsilon &&
                        existing.Y1 >= candY1 - Epsilon)
                    {
                        // חפיפה בציר X
                        bool overlapX = existing.X1 < candX2 - Epsilon &&
                                        existing.X2 > candX1 + Epsilon;
                        if (!overlapX) continue;

                        // חפיפה בציר Z
                        bool overlapZ = existing.Z1 < candZ2 - Epsilon &&
                                        existing.Z2 > candZ1 + Epsilon;

                        if (overlapZ)
                            return false;
                    }
                }
            }

            placed = candidate;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // עזר: ארגזים שטרם מוקמו
        // ─────────────────────────────────────────────────────────────────

        private static List<BoxInstance> GetRemainingBoxes(
            List<BoxInstance> allBoxes,
            PackingState      current)
        {
            var placedIds = current.PlacedBoxes
                .Select(pb => pb.Instance.InstanceId)
                .ToHashSet();

            return allBoxes
                .Where(b => !placedIds.Contains(b.InstanceId))
                .ToList();
        }
    }
}
