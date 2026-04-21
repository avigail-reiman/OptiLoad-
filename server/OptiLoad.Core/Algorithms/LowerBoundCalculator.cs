using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// חסמים תחתונים לבעיית 3D-BPP לפי Martello, Pisinger & Vigo (1997).
    ///
    /// ‣ L0 – חסם רציף: ⌈ΣVol / BinVol⌉          – O(n), worst-case = 1/8
    /// ‣ L1 – חסם חד-ממדי (רדוקציה ל-1D-BPP)      – O(n²)
    /// ‣ L2 – חסם תלת-ממדי (Kv, Kl, Ks)            – O(n²), L2 ≥ L1 ≥ L0
    /// </summary>
    public static class LowerBoundCalculator
    {
        // ─────────────────────────────────────────────────────────────────
        // L0 – חסם רציף (Continuous Lower Bound)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// נוסחה (1) מהמאמר: L0 = ⌈ΣVol(boxes) / Vol(bin)⌉
        /// </summary>
        public static int ComputeL0(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            double totalVolume = boxes.Sum(b => b.BoxDefinition.Volume);
            return (int)Math.Ceiling(totalVolume / container.Volume);
        }

        // ─────────────────────────────────────────────────────────────────
        // L1 – חסם חד-ממדי
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// נוסחה (7) מהמאמר: L1 = max(L1_WH, L1_WD, L1_HD)
        /// לכל זוג ממדים, מרדד ל-1D-BPP ומחשב חסם לפי נוסחה (4).
        /// </summary>
        public static int ComputeL1(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();

            int l1WH = ComputeL1OneDim(boxList,
                b => b.Width, b => b.Height, b => b.Depth,
                container.Width, container.Height, container.Depth);

            int l1WD = ComputeL1OneDim(boxList,
                b => b.Width, b => b.Depth, b => b.Height,
                container.Width, container.Depth, container.Height);

            int l1HD = ComputeL1OneDim(boxList,
                b => b.Height, b => b.Depth, b => b.Width,
                container.Height, container.Depth, container.Width);

            return Math.Max(l1WH, Math.Max(l1WD, l1HD));
        }

        /// <summary>
        /// חסם L1 לזוג ממדים ספציפי (נוסחאות 3–6 מהמאמר).
        /// dim1, dim2 = שני הממדים "הגדולים"; dim3 = הממד השלישי (bin capacity).
        /// </summary>
        private static int ComputeL1OneDim(
            List<BoxInstance> boxes,
            Func<Box, double> getDim1,
            Func<Box, double> getDim2,
            Func<Box, double> getDim3,
            double W, double H, double D)
        {
            // ארגזים ב-J^WH: dim1 > W/2 AND dim2 > H/2
            var jWH = boxes
                .Where(b =>
                {
                    var box = b.BoxDefinition;
                    return getDim1(box) > W / 2.0 && getDim2(box) > H / 2.0;
                })
                .ToList();

            if (jWH.Count == 0) return 0;

            // מספר ארגזים שעומקם > D/2 (כל אחד דורש מכולה משלו בציר D)
            int largeDepthCount = jWH.Count(b => getDim3(b.BoxDefinition) > D / 2.0);

            int best = largeDepthCount;

            // מעבר על ערכי p (נוסחה 4) – כל ערך מגודל ארגז
            var pValues = jWH
                .Select(b => getDim3(b.BoxDefinition))
                .Where(p => p >= 1 && p <= D / 2.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            foreach (double p in pValues)
            {
                // J_l(p) = ארגזים ב-J^WH שעומקם: D-p <= d <= D/2
                var jL = jWH.Where(b =>
                {
                    double d = getDim3(b.BoxDefinition);
                    return d >= D - p && d <= D / 2.0;
                }).ToList();

                // J_s(p) = ארגזים ב-J^WH שעומקם: D/2 <= d <= p
                var jS = jWH.Where(b =>
                {
                    double d = getDim3(b.BoxDefinition);
                    return d >= D / 2.0 && d <= p;
                }).ToList();

                if (jL.Count == 0 && jS.Count == 0) continue;

                double sumDs   = jS.Sum(b => getDim3(b.BoxDefinition));
                double sumDl   = jL.Sum(b => getDim3(b.BoxDefinition));
                int    countLarge = jWH.Count(b => getDim3(b.BoxDefinition) > D / 2.0);

                // נוסחה (4) – גרסה ראשונה
                double numerator1 = sumDs - (jL.Count * D - sumDl);
                int    bound1     = countLarge + (int)Math.Ceiling(Math.Max(0, numerator1) / D);

                // נוסחה (4) – גרסה שנייה
                double floorDP    = Math.Floor(D / p);
                double sumFloors  = jL.Sum(b => Math.Floor(getDim3(b.BoxDefinition) / p));
                double numerator2 = jS.Count - sumFloors;
                int    bound2     = countLarge + (int)Math.Ceiling(Math.Max(0, numerator2) / floorDP);

                best = Math.Max(best, Math.Max(bound1, bound2));
            }

            return best;
        }

        // ─────────────────────────────────────────────────────────────────
        // L2 – חסם תלת-ממדי (הטוב ביותר)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// נוסחה (13) מהמאמר: L2 = max(L2_WH, L2_WD, L2_HD).
        /// Theorem 4: L2 ≥ L1 ≥ L0 תמיד.
        /// </summary>
        public static int ComputeL2(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();
            if (boxList.Count == 0) return 0;

            int l2WH = ComputeL2OneDim(boxList,
                b => b.Width, b => b.Height, b => b.Depth,
                container.Width, container.Height, container.Depth);

            int l2WD = ComputeL2OneDim(boxList,
                b => b.Width, b => b.Depth, b => b.Height,
                container.Width, container.Depth, container.Height);

            int l2HD = ComputeL2OneDim(boxList,
                b => b.Height, b => b.Depth, b => b.Width,
                container.Height, container.Depth, container.Width);

            return Math.Max(l2WH, Math.Max(l2WD, l2HD));
        }

        /// <summary>
        /// נוסחה (11) + (14) מהמאמר – L2 לזוג ממדים.
        /// מחשב L2^WH(p,q) לכל זוגות (p,q) ולוקח מקסימום.
        /// </summary>
        private static int ComputeL2OneDim(
            List<BoxInstance> boxes,
            Func<Box, double> getDim1,
            Func<Box, double> getDim2,
            Func<Box, double> getDim3,
            double W, double H, double D)
        {
            // L1^WH (בסיס)
            int l1Base = ComputeL1OneDim(boxes,
                getDim1, getDim2, getDim3,
                W, H, D);

            double binVolume = W * H * D;
            int best = l1Base;

            // ערכי p – כל dim1 עם p <= W/2
            var pValues = boxes
                .Select(b => getDim1(b.BoxDefinition))
                .Where(p => p >= 1 && p <= W / 2.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // ערכי q – כל dim2 עם q <= H/2
            var qValues = boxes
                .Select(b => getDim2(b.BoxDefinition))
                .Where(q => q >= 1 && q <= H / 2.0)
                .Distinct()
                .OrderBy(q => q)
                .ToList();

            foreach (double p in pValues)
            {
                foreach (double q in qValues)
                {
                    // Kv(p,q) – ארגזים ענקיים: dim1 > W-p AND dim2 > H-q
                    var kv = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return getDim1(box) > W - p && getDim2(box) > H - q;
                    }).ToList();

                    // Kl(p,q) – ארגזים בינוניים: dim1 > W/2 AND dim2 > H/2 (ולא ב-Kv)
                    var kl = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return !kv.Contains(b) &&
                               getDim1(box) > W / 2.0 &&
                               getDim2(box) > H / 2.0;
                    }).ToList();

                    // Ks(p,q) – ארגזים קטנים: dim1 >= p AND dim2 >= q (ולא ב-Kv∪Kl)
                    var ks = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return !kv.Contains(b) && !kl.Contains(b) &&
                               getDim1(box) >= p && getDim2(box) >= q;
                    }).ToList();

                    if (ks.Count == 0 && kl.Count == 0) continue;

                    // נוסחה (11)
                    double sumKvDepth  = kv.Sum(b => getDim3(b.BoxDefinition));
                    double sumKlKsVol  = kl.Sum(b => b.BoxDefinition.Volume)
                                       + ks.Sum(b => b.BoxDefinition.Volume);

                    double availableVolume = binVolume * l1Base - W * H * sumKvDepth;
                    double numerator       = sumKlKsVol - availableVolume;
                    int    extraBins       = (int)Math.Ceiling(Math.Max(0, numerator) / binVolume);

                    int bound = l1Base + extraBins;
                    if (bound > best) best = bound;
                }
            }

            return best;
        }

        // ─────────────────────────────────────────────────────────────────
        // ממשק מאוחד: חשב את הטוב ביותר מבין L0, L1, L2
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מחשב L0, L1, L2 ומחזיר את המקסימום.
        /// L2 תמיד >= L1 >= L0, אך חישוב מפורש מבטיח נכונות.
        /// </summary>
        public static int ComputeBestLowerBound(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();
            if (boxList.Count == 0) return 0;

            int l0 = ComputeL0(boxList, container);
            int l1 = ComputeL1(boxList, container);
            int l2 = ComputeL2(boxList, container);

            return Math.Max(l0, Math.Max(l1, l2));
        }
    }
}
