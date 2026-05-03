using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    /// <summary>
    /// אלגוריתם Branch & Bound ראשי לפתרון 3D-BPP.
    ///
    /// בסיס: Martello et al. (1997), סעיפים 5.1–5.2.
    ///
    /// מבנה שני-שכבתי:
    ///   ‣ עץ ראשי (BranchMain):     מחליט לאיזו מכולה מוקצה כל ארגז
    ///   ‣ עץ משני (SingleBinFiller): בודק ישימות בתוך מכולה ומחשב מיקומים
    ///
    /// ── טיפול בארגזים שבירים ──────────────────────────────────────────
    /// Solve() מפריד את הארגזים לשני קבוצות ומריץ שני שלבים ברצף:
    ///
    ///   שלב 1: B&B על ארגזים לא-שבירים בלבד → קובע מיקומי כל הלא-שבירים.
    ///   שלב 2: B&B על ארגזים שבירים בלבד    → מוסיף אותם מעל תוצאת שלב 1.
    ///
    /// SingleBinFiller אוכף שארגז שביר לא ייכנס מתחת לארגז לא-שביר קיים.
    /// מכיוון ששלב 1 מסתיים לפני שלב 2, כל ארגז לא-שביר כבר במקומו
    /// ולא ייוסף מעל ארגז שביר.
    ///
    /// קיצוצים:
    ///   ‣ L2 Pruning:     lower_bound(remaining) + current_bins >= best_bins
    ///   ‣ Volume Pruning: total_remaining_volume > remaining_bin_capacity
    ///   ‣ Weight Pruning: total_remaining_weight > max_weight_per_bin
    ///   ‣ Time Limit:     300 שניות
    /// </summary>
    public class BranchAndBoundSolver
    {
        // ─── קבועים ──────────────────────────────────────────────────────
        private const int MaxGlobalNodes = 10_000_000;

        /// <summary>מגבלת זמן בשניות. ברירת מחדל: 300. ניתן לשינוי לפני Solve().</summary>
        public double TimeLimitSeconds { get; set; } = 3600.0;

        // ─── נתוני ריצה ──────────────────────────────────────────────────
        private readonly ContainerDimensions _container;
        private readonly Stopwatch           _stopwatch = new();

        private int  _globalNodeCount;
        private int  _bestBins;
        private bool _isOptimal;
        private bool _greedyFallbackUsed;
        private List<BoxInstance> _greedyUnplaced = new();

        /// <summary>
        /// גובה מילוי מקסימלי יחסי (0.0–1.0) – מועבר לכל SingleBinFiller שנוצר.
        /// ברירת מחדל: 1.0 = 100% מגובה המכולה.
        /// שינוי לפני קריאה ל-Solve() ישפיע על כל תהליך השיבוץ.
        /// </summary>
        public double MaxFillHeightRatio { get; set; } = 1.0;

        // תוצאה: מיפוי ארגז → (מכולה, מיקום, כיוון)
        private List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)>
            _bestPlacements = new();

        public BranchAndBoundSolver(ContainerDimensions container)
        {
            _container = container;
        }

        // ─────────────────────────────────────────────────────────────────
        // ממשק ציבורי
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// פותר 3D-BPP ומחזיר PackingResult עם מיקום מדויק לכל ארגז.
        ///
        /// זרימה:
        ///   1. חסם תחתון L2
        ///   2. H1 (פתרון ראשוני)
        ///   3. B&B שלב 1 – ארגזים לא-שבירים
        ///   4. B&B שלב 2 – ארגזים שבירים מעל תוצאת שלב 1
        /// </summary>
        public PackingResult Solve(IEnumerable<BoxInstance> instances)
        {
            _stopwatch.Restart();
            _greedyFallbackUsed = false;
            _greedyUnplaced     = new List<BoxInstance>();
            var allBoxes = instances.ToList(); // All boxes to be packed
            if (allBoxes.Count == 0)
                return BuildResult(new(), allBoxes, 0, true);

            // ── חלוקה לשבירים ולא-שבירים ──
            var nonFragile = allBoxes.Where(b => !b.BoxDefinition.IsFragile).ToList();
            var fragile    = allBoxes.Where(b =>  b.BoxDefinition.IsFragile).ToList();

            // ── שלב 1: חסם תחתון + H1 על לא-שבירים ──
            int lowerBound = LowerBoundCalculator.ComputeBestLowerBound(nonFragile, _container);

            var h1           = new HeuristicH1(_container);
            var h1Phase1     = h1.Solve(nonFragile);
            _bestBins        = h1Phase1.binsUsed;
            _bestPlacements  = h1Phase1.placements;

            // ── תיקון: אם H1 השאיר ארגזים ללא שיבוץ —
            // _bestBins נמוך מדי ו-B&B לא יכול לפתוח מכולות נוספות.
            // מריצים Greedy להשלמת הפתרון ומעדכנים _bestBins/_bestPlacements. ──
            {
                var h1PlacedIds = h1Phase1.placements
                    .Select(p => p.instance.InstanceId)
                    .ToHashSet();
                var h1Unplaced = nonFragile
                    .Where(b => !h1PlacedIds.Contains(b.InstanceId))
                    .ToList();
                if (h1Unplaced.Count > 0)
                {
                    var h1Bins = RebuildBinsFromPlacements(h1Phase1.placements);
                    GreedySolver.FillRemaining(h1Unplaced, h1Bins, _container, MaxFillHeightRatio);
                    _bestBins       = h1Bins.Count;
                    _bestPlacements = ExtractPlacements(h1Bins);
                }
            }

            // ── B&B שלב 1: שיבוץ לא-שבירים ──
            _globalNodeCount = 0;
            var sortedNonFragile = nonFragile
                .OrderByDescending(b => b.BoxDefinition.Volume).ToList();

            var phase1Assignment = new List<(BoxInstance instance, int bin)>();
            var phase1OpenBins   = new List<PackingState>();

            BranchMain(sortedNonFragile, phase1Assignment,
                       phase1OpenBins, lowerBound, fragilePhase: false);

            // ── שחזר bins מהפתרון הטוב של שלב 1 (phase1OpenBins עלול להיות ריק אחרי backtracking) ──
            var bestPhase1Bins = RebuildBinsFromPlacements(_bestPlacements);

            // ── שלב 2: שיבוץ שבירים מעל תוצאת שלב 1 ──
            int lbFragile = 0;
            if (fragile.Count > 0)
            {
                lbFragile = LowerBoundCalculator
                    .ComputeBestLowerBound(fragile, _container);

                var sortedFragile = fragile
                    .OrderByDescending(b => b.BoxDefinition.Volume).ToList();

                var phase2Assignment = new List<(BoxInstance instance, int bin)>();

                // מתחילים מה-bins השמורים של שלב 1
                var phase2OpenBins = bestPhase1Bins;

                // ── חסם עליון ראשוני לשלב 2: H1 + Greedy על ארגזים שבירים ───────────
                // נחוץ בפרט כשכל הארגזים שבירים (phase2OpenBins ריק):
                // ללא זה _bestBins = lbFragile+1 אשר עלול להיות נמוך מהפתרון האמיתי,
                // ואז B&B לא מוצא כלום ומחזיר תוצאה ריקה.
                {
                    var h1Fragile       = new HeuristicH1(_container);
                    var h1FragileResult = h1Fragile.Solve(sortedFragile);

                    // גלה ארגזים שבירים שH1 דילג עליהם והשלם בGreedy
                    var h1FragilePlacedIds = h1FragileResult.placements
                        .Select(p => p.instance.InstanceId).ToHashSet();
                    var h1FragileUnplaced = sortedFragile
                        .Where(b => !h1FragilePlacedIds.Contains(b.InstanceId)).ToList();

                    var fragileH1Placements = h1FragileResult.placements
                        .Select(p => (p.instance, p.bin, p.pos, p.rot))
                        .ToList();
                    if (h1FragileUnplaced.Count > 0)
                    {
                        var h1FragileBins = RebuildBinsFromPlacements(fragileH1Placements);
                        GreedySolver.FillRemaining(h1FragileUnplaced, h1FragileBins,
                                                   _container, MaxFillHeightRatio);
                        fragileH1Placements = ExtractPlacements(h1FragileBins);
                    }

                    // הסט מדדי bin של שבירים כך שיבואו אחרי מכולות שלב 1
                    int binOffset = phase2OpenBins.Count;
                    var offsetFragileH1 = fragileH1Placements
                        .Select(p => (p.Item1, p.Item2 + binOffset, p.Item3, p.Item4))
                        .ToList();

                    // פתרון ראשוני מלא = שלב 1 + H1-שבירים
                    var initialSolution = _bestPlacements.Concat(offsetFragileH1).ToList();
                    _bestPlacements = initialSolution;
                    _bestBins = _bestPlacements.Count > 0
                        ? _bestPlacements.Select(p => p.bin).Max() + 1
                        : 0;
                }

                BranchMain(sortedFragile, phase2Assignment,
                           phase2OpenBins, lbFragile, fragilePhase: true,
                           binOffset: bestPhase1Bins.Count);

                // אחרי B&B: בדוק אם נמצא פתרון מלא עם ארגזים שבירים
                bool allFragilePlaced = fragile.All(f =>
                    _bestPlacements.Any(p => p.instance.InstanceId == f.InstanceId));

                if (allFragilePlaced)
                {
                    // B&B מצא פתרון מלא – עדכן מספר מכולות מהפתרון הטוב
                    _bestBins = _bestPlacements.Select(p => p.bin).Max() + 1;
                }
                else
                {
                    // לא הצלחנו לשבץ הכל – שמור את הפתרון החלקי הטוב ביותר שנמצא
                    // (_bestPlacements כבר מכיל את הפתרון מH1/Greedy/B&B-חלקי)
                    _bestBins = _bestPlacements.Count > 0
                        ? _bestPlacements.Select(p => p.bin).Max() + 1
                        : 0;
                }
            }

            // ── שלב Hole-Filling/Refitting: נסה להכניס כל ארגז שלא שובץ לכל מכולה קיימת ──
            var bins = RebuildBinsFromPlacements(_bestPlacements);
            var unplaced = allBoxes.Where(b => !_bestPlacements.Any(p => p.instance.InstanceId == b.InstanceId)).ToList();
            bool added;
            do
            {
                added = false;
                foreach (var box in unplaced.ToList())
                {
                    for (int binIdx = 0; binIdx < bins.Count; binIdx++)
                    {
                        var bin = bins[binIdx];
                        var corners = CornerPointsFinder.Find3DCorners(bin.PlacedBoxes.ToList(), _container, new[] { box });
                        foreach (var corner in corners)
                        {
                            foreach (var rot in box.BoxDefinition.GetAllowedRotations())
                            {
                                var filler = CreateFiller();
                                if (filler.TryPlaceBox(bin, box, corner, rot, false, out var placed))
                                {
                                    bin.AddBox(placed);
                                    _bestPlacements = _bestPlacements.Concat(new[] { (box, binIdx, placed.Position, placed.Rotation) }).ToList();
                                    unplaced.Remove(box);
                                    added = true;
                                    break;
                                }
                            }
                            if (added) break;
                        }
                        if (added) break;
                    }
                    if (added) break;
                }
            } while (added);

            _stopwatch.Stop();
            // חסם תחתון כולל: מקסימום בין שלב 1 לשלב 2
            int overallLowerBound = Math.Max(lowerBound, lbFragile);
            _isOptimal = (_bestBins == overallLowerBound);

            // ── שלב Repacking/Consolidation: נסה להעביר כל ארגז ממכולה גבוהה לנמוכה ──
            var binsForRepack = RebuildBinsFromPlacements(_bestPlacements);
            bool moved;
            do
            {
                moved = false;
                // עבור כל מכולה (2 והלאה)
                for (int fromBin = 1; fromBin < binsForRepack.Count; fromBin++)
                {
                    var boxesToTry = binsForRepack[fromBin].PlacedBoxes.ToList();
                    foreach (PlacedBox box in boxesToTry)
                    {
                        // נסה לשבץ בכל מכולה קודמת
                        for (int toBin = 0; toBin < fromBin; toBin++)
                        {
                            var toBinState = binsForRepack[toBin];
                            var corners = CornerPointsFinder.Find3DCorners(toBinState.PlacedBoxes.ToList(), _container, new[] { box.Instance });
                            bool placed = false;
                            foreach (var corner in corners)
                            {
                                foreach (var rot in box.Instance.BoxDefinition.GetAllowedRotations())
                                {
                                    var filler = CreateFiller();
                                    if (filler.TryPlaceBox(toBinState, box.Instance, corner, rot, false, out var placedBox))
                                    {
                                        toBinState.AddBox(placedBox);
                                        ((List<PlacedBox>)binsForRepack[fromBin].PlacedBoxes).Remove(box);
                                        moved = true;
                                        placed = true;
                                        break;
                                    }
                                }
                                if (placed) break;
                            }
                            if (placed) break;
                        }
                    }
                }
            } while (moved);

            // ── שלב Hole-Filling Brute Force: מלא כל מכולה עד תום ──
            var repackedPlacements = ExtractPlacements(binsForRepack);
            var repackedPlacedIds = repackedPlacements.Select(p => p.Item1.InstanceId).ToHashSet();
            var bruteUnplaced = allBoxes.Where(b => !repackedPlacedIds.Contains(b.InstanceId)).ToList();


            // רזולוציה עדינה מאוד
            double step = 0.1;
            foreach (var bin in binsForRepack.Select((b, idx) => (b, idx)))
            {
                var toPlace = bruteUnplaced.ToList();
                foreach (var box in toPlace)
                {
                    bool placed = false;
                    foreach (var rot in box.BoxDefinition.GetAllowedRotations())
                    {
                        for (double x = 0; x + rot.W <= _container.Width + 1e-6; x += step)
                        {
                            for (double y = 0; y + rot.H <= _container.Height + 1e-6; y += step)
                            {
                                for (double z = 0; z + rot.D <= _container.Depth + 1e-6; z += step)
                                {
                                    var pos = new Position3D(x, y, z);
                                    var filler = CreateFiller();
                                    if (filler.TryPlaceBox(bin.b, box, pos, rot, false, out var placedBox))
                                    {
                                        bin.b.AddBox(placedBox);
                                        bruteUnplaced.Remove(box);
                                        placed = true;
                                        Console.WriteLine($"[HoleFilling] Box {box.InstanceId} placed in bin {bin.idx} at ({x:F2},{y:F2},{z:F2}) rot={rot}");
                                        goto NextBox;
                                    }
                                    else
                                    {
                                        // לוג דיאגנוסטי לכישלון
                                        //Console.WriteLine($"[HoleFilling] Box {box.InstanceId} FAILED at bin {bin.idx} ({x:F2},{y:F2},{z:F2}) rot={rot}");
                                    }
                                }
                            }
                        }
                    }
                NextBox:
                    if (!placed)
                        Console.WriteLine($"[HoleFilling] Box {box.InstanceId} could not be placed in bin {bin.idx}");
                }
            }

            var brutePlacements = ExtractPlacements(binsForRepack);
            int bruteBinsUsed = binsForRepack.Count(b => b.PlacedBoxes.Count > 0);

            return BuildResult(brutePlacements, allBoxes, bruteBinsUsed, _isOptimal);
        }

        // ─────────────────────────────────────────────────────────────────
        // עץ ראשי: הקצאת ארגזים למכולות
        // ─────────────────────────────────────────────────────────────────

        private void BranchMain(
            List<BoxInstance>                     allBoxes,
            List<(BoxInstance instance, int bin)> currentAssignment,
            List<PackingState>                    openBins,
            int                                   lowerBound,
            bool                                  fragilePhase,
            int                                   binOffset = 0)
        {
            _globalNodeCount++;

            // ── בדיקות עצירה ──
            if (_globalNodeCount > MaxGlobalNodes) return;

            // ── חריגת זמן: הפעל fallback חמדני על הארגזים הנותרים ──
            if (_stopwatch.Elapsed.TotalSeconds > TimeLimitSeconds)
            {
                if (!_greedyFallbackUsed)
                {
                    _greedyFallbackUsed = true;

                    // שחזר את הפתרון הטוב ביותר שנמצא (לא המצב החלקי הנוכחי!)
                    var bestBins = RebuildBinsFromPlacements(_bestPlacements);

                    // מצא אילו ארגזים לא שובצו בפתרון הטוב
                    var bestPlacedIds = _bestPlacements
                        .Select(p => p.instance.InstanceId)
                        .ToHashSet();
                    var unassigned = allBoxes
                        .Where(b => !bestPlacedIds.Contains(b.InstanceId))
                        .ToList();

                    _greedyUnplaced = GreedySolver.FillRemaining(
                        unassigned,
                        bestBins,
                        _container,
                        MaxFillHeightRatio);

                    // עדכן פתרון טוב עם תוצאת Greedy מעל הפתרון הטוב
                    _bestBins       = bestBins.Count;
                    _bestPlacements = ExtractPlacements(bestBins);
                }
                return;
            }

            // כל הארגזים שובצו
            if (currentAssignment.Count == allBoxes.Count)
            {
                int usedBins = openBins.Count;
                if (usedBins < _bestBins)
                {
                    _bestBins       = usedBins;
                    _bestPlacements = ExtractPlacements(openBins);
                }
                return;
            }

            // הארגז הבא
            // --- לוגיקת שכבות ---
            // קבע את גובה השכבה הנוכחית לפי הארגז הגדול ביותר שטרם שובץ
            var remainingBoxes = allBoxes.Skip(currentAssignment.Count).ToList();
            double? currentLayerY = null;
            double? currentLayerHeight = null;
            if (remainingBoxes.Count > 0)
            {
                var largestBox = remainingBoxes.OrderByDescending(b => b.BoxDefinition.Height).First();
                currentLayerHeight = largestBox.BoxDefinition.Height;
                // שכבה מתחילה בגובה Y פנוי מינימלי בכל אחת מהמכולות
                currentLayerY = openBins.SelectMany(bin => bin.PlacedBoxes.Select(pb => pb.Y2)).DefaultIfEmpty(0).Max();
            }

            var nextBox = allBoxes[currentAssignment.Count];

            // ── קיצוץ: חסם תחתון ──
            // binOffset = מספר המכולות של שלב 1 (בשלב 2 הן כבר קיימות ולא נספרות כ"חדשות")
            var remaining = allBoxes.Skip(currentAssignment.Count).ToList();

            // חסם L0 מתוקן: מחשב כמה מכולות חדשות נדרשות לארגזים הנותרים,
            // בהתחשב בקיבול הפנוי ב-bins הפתוחות כבר (לא רק bins חדשות).
            // ללא תיקון זה, lb=1 תמיד כשיש ארגזים נותרים, ו-B&B קוצץ כל ניסיון
            // לדחוס ארגזים נוספים לתוך מכולות קיימות.
            double freeCapacityInOpenBins = openBins.Sum(b => _container.Volume - b.UsedVolume);
            double remainingVol = remaining.Sum(b => b.BoxDefinition.Volume);
            int lb = Math.Max(0,
                (int)Math.Ceiling(Math.Max(0.0, remainingVol - freeCapacityInOpenBins)
                                  / _container.Volume));

            int newBinsUsed = openBins.Count - binOffset;  // מכולות שנפתחו בשלב הנוכחי בלבד
            int bestNewBins = _bestBins - binOffset;         // יעד: מכולות נוספות מהפתרון הטוב
            if (newBinsUsed + lb >= bestNewBins) return;

            // ── ענף 1: שים במכולה פתוחה קיימת ──
            // Symmetry breaking: אם המכולה ריקה (UsedVolume=0), ניסינו כבר מכולה ריקה קודמת —
            // כל המכולות הריקות זהות, אין טעם לנסות יותר מאחת.
            // ── Symmetry breaking: ararargzim identical ──
            int minBinIdx = 0;
            if (currentAssignment.Count > 0)
            {
                var prev = currentAssignment[^1];
                if (SameBoxType(nextBox, prev.instance))
                    minBinIdx = prev.bin;
            }

            bool triedEmptyBin = false;
            bool placedInExisting = false;
            int tryCount = 0;
            for (int binIdx = minBinIdx; binIdx < openBins.Count; binIdx++)
            {
                bool isEmpty = openBins[binIdx].UsedVolume < 1e-9;
                if (isEmpty)
                {
                    if (triedEmptyBin) continue;  // symmetry breaking
                    triedEmptyBin = true;
                }

                // ננסה כל פינה חוקית, בכל סיבוב, בכל מכולה קיימת
                if (TryAssignToBin_Exhaustive(nextBox, binIdx, openBins, fragilePhase, out var placement, currentLayerY, currentLayerHeight))
                {
                    placedInExisting = true;
                    currentAssignment.Add((nextBox, binIdx));
                    BranchMain(allBoxes, currentAssignment, openBins, lowerBound, fragilePhase, binOffset);
                    currentAssignment.RemoveAt(currentAssignment.Count - 1);
                    UndoAssignToBin(nextBox, binIdx, openBins, fragilePhase);
                }
                tryCount++;
            }

            // פתח מכולה חדשה רק אם מוצה כל ניסיון שיבוץ בכל המכולות הקיימות
            if (!placedInExisting && openBins.Count - binOffset + 1 < bestNewBins)
            {
                var initFiller    = CreateFiller();
                PlacedBox? initPlacement = null;
                double initBestX2 = double.MaxValue;
                double initBestY2 = double.MaxValue;

                foreach (var rot in nextBox.BoxDefinition.GetAllowedRotations())
                {
                    if (initFiller.TryPlaceBox(new PackingState(), nextBox,
                                               new Position3D(0, 0, 0), rot,
                                               fragilePhase, out var p))
                    {
                        if (p!.X2 < initBestX2 - 1e-9 ||
                            (p.X2 <= initBestX2 + 1e-9 && p.Y2 < initBestY2 - 1e-9))
                        {
                            initBestX2 = p.X2;
                            initBestY2 = p.Y2;
                            initPlacement = p;
                        }
                    }
                }

                if (initPlacement != null)
                {
                    var newBinState = new PackingState();
                    newBinState.AddBox(initPlacement);
                    openBins.Add(newBinState);
                    currentAssignment.Add((nextBox, openBins.Count - 1));

                    Console.WriteLine($"[OptiLoad][DEBUG] פותח מכולה חדשה לארגז {nextBox.InstanceId} לאחר שמוצה כל ניסיון שיבוץ בכל המכולות הקיימות ({tryCount} ניסיונות)");

                    BranchMain(allBoxes, currentAssignment, openBins, lowerBound, fragilePhase, binOffset);

                    currentAssignment.RemoveAt(currentAssignment.Count - 1);
                    openBins.RemoveAt(openBins.Count - 1);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ניסיון הוספת ארגז למכולה פתוחה
        // ─────────────────────────────────────────────────────────────────

        // ממצה כל פינה חוקית, בכל סיבוב, בכל מכולה קיימת
        private bool TryAssignToBin_Exhaustive(
            BoxInstance        box,
            int                binIdx,
            List<PackingState> openBins,
            bool               fragilePhase,
            out PlacedBox? placement,
            double? currentLayerY = null,
            double? currentLayerHeight = null)
        {
            var currentState = openBins[binIdx];
            var corners = CornerPointsFinder.Find3DCorners(
                currentState.PlacedBoxes.ToList(), _container, new[] { box });

            var sortedCorners = corners.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();
            var filler = CreateFiller();

            foreach (var rotation in box.BoxDefinition.GetAllowedRotations())
            {
                foreach (var corner in sortedCorners)
                {
                    // שכבתיות: נסה קודם כל פינות בגובה השכבה הנוכחית בלבד
                    if (currentLayerY.HasValue && currentLayerHeight.HasValue)
                    {
                        // אם הפינה לא בשכבה הנוכחית, דלג
                        if (Math.Abs(corner.Y - currentLayerY.Value) > 1e-6)
                            continue;
                        // אם הארגז לא נכנס בגובה השכבה, דלג
                        if (corner.Y + rotation.H > currentLayerY.Value + currentLayerHeight.Value + 1e-6)
                            continue;
                    }
                    if (filler.TryPlaceBox(currentState, box, corner, rotation,
                                          fragilePhase, out var placed))
                    {
                        currentState.AddBox(placed);
                        placement = placed;
                        return true;
                    }
                }
            }
            placement = null;
            return false;
        }

        private void UndoAssignToBin(
            BoxInstance        box,
            int                binIdx,
            List<PackingState> openBins,
            bool               fragilePhase)
        {
            // Backtrack: הסר את הארגז שהוסף אחרון (TryAssignToBin תמיד מוסיף בסוף)
            openBins[binIdx].RemoveLastBox();
        }

        // ─────────────────────────────────────────────────────────────────
        // מיזוג תוצאות שני השלבים
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// בודק אם שני ארגזים זהים מבחינת הגדרה (לצורך symmetry breaking).
        /// שני ארגזים נחשבים זהים אם ממדיהם, משקלם, שבירותם וכיווניהם זהים.
        /// </summary>
        private static bool SameBoxType(BoxInstance a, BoxInstance b)
        {
            var da = a.BoxDefinition;
            var db = b.BoxDefinition;
            return Math.Abs(da.Width  - db.Width)  < 1e-9 &&
                   Math.Abs(da.Height - db.Height) < 1e-9 &&
                   Math.Abs(da.Depth  - db.Depth)  < 1e-9 &&
                   Math.Abs(da.WeightKg - db.WeightKg) < 1e-9 &&
                   da.IsFragile     == db.IsFragile &&
                   da.AllowRotation == db.AllowRotation;
        }

        /// <summary>
        /// בונה מחדש רשימת PackingState מרשימת מיקומים – משמש לשחזור bins אחרי backtracking.
        /// </summary>
        private static List<PackingState> RebuildBinsFromPlacements(
            List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements)
        {
            if (placements.Count == 0) return new List<PackingState>();

            int maxBin = placements.Max(p => p.bin);
            var bins   = Enumerable.Range(0, maxBin + 1)
                                   .Select(_ => new PackingState())
                                   .ToList();

            foreach (var (instance, bin, pos, rot) in placements)
                bins[bin].AddBox(new PlacedBox(instance, pos, rot) { BinIndex = bin });

            return bins;
        }

        /// <summary>
        /// מחזיר רשימת מיקומים מאוחדת של שלב 1 + שלב 2.
        /// שלב 2 (phase2OpenBins) כולל את ארגזי שלב 1 + ארגזי שלב 2 ביחד.
        /// </summary>
        private static List<(BoxInstance, int, Position3D, Rotation)> MergePlacements(
            List<PackingState> phase1OpenBins,
            List<PackingState> phase2OpenBins,
            bool               hasFragile)
        {
            // אם יש שבירים – תוצאת שלב 2 כוללת הכל
            if (hasFragile)
                return ExtractPlacements(phase2OpenBins);

            return ExtractPlacements(phase1OpenBins);
        }

        // ─────────────────────────────────────────────────────────────────
        // בניית רשימת מיקומים
        // ─────────────────────────────────────────────────────────────────

        private static List<(BoxInstance, int, Position3D, Rotation)> ExtractPlacements(
            List<PackingState> openBins)
        {
            var result = new List<(BoxInstance, int, Position3D, Rotation)>();

            for (int b = 0; b < openBins.Count; b++)
            {
                foreach (var placed in openBins[b].PlacedBoxes)
                {
                    result.Add((placed.Instance, b, placed.Position, placed.Rotation));
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // עזר: יצירת SingleBinFiller עם הגדרות אחידות
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// יוצר SingleBinFiller ומעביר אליו את MaxFillHeightRatio הנוכחי.
        /// כל יצירת filler בקובץ זה תעבור דרך כאן – שינוי אחד משפיע על הכל.
        /// </summary>
        private SingleBinFiller CreateFiller() =>
            new SingleBinFiller(_container)
            {
                MaxFillHeightRatio = MaxFillHeightRatio
            };

        // ─────────────────────────────────────────────────────────────────
        // בניית PackingResult
        // ─────────────────────────────────────────────────────────────────

        private PackingResult BuildResult(
            List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements,
            List<BoxInstance>                                                    allBoxes,
            int                                                                  binsUsed,
            bool                                                                 isOptimal)
        {
            var placedIds = placements.Select(p => p.instance.InstanceId).ToHashSet();

            // ארגזים שלא שובצו = לא ב-placements ולא טופלו ע"י החמדני
            var greedyUnplacedIds = _greedyUnplaced
                .Select(b => b.InstanceId)
                .ToHashSet();

            var unplaced = allBoxes
                .Where(b => !placedIds.Contains(b.InstanceId))
                .Where(b =>  greedyUnplacedIds.Contains(b.InstanceId)
                          || !_greedyFallbackUsed)
                .ToList();

            // בנה רשימה ראשונית
            var rawPlaced = placements
                .Select(p => new PlacedBox(p.instance, p.pos, p.rot) { BinIndex = p.bin })
                .ToList();

            // הפעל כוח משיכה: הורד כל ארגז לנקודה הנמוכה ביותר האפשרית (מכולה בכולה)
            var placedBoxes = rawPlaced
                .GroupBy(pb => pb.BinIndex)
                .SelectMany(g => GravitySettler.Settle(g.ToList(), _container))
                .ToList();

            double totalVolume = placedBoxes.Sum(pb => pb.Volume);
            double binVolume   = _container.Volume * Math.Max(1, binsUsed);

            // חישוב ניצולת לכל מכולה בנפרד
            var perBinStats = Enumerable.Range(0, Math.Max(1, binsUsed)).Select(b =>
            {
                double used = placedBoxes.Where(pb => pb.BinIndex == b).Sum(pb => pb.Volume);
                return new BinStats
                {
                    BinIndex    = b,
                    UsedVolume  = used,
                    TotalVolume = _container.Volume
                };
            }).ToList();

            // בניית הודעת סטטוס
            string statusMessage;
            if (isOptimal)
                statusMessage = $"פתרון אופטימלי נמצא: {binsUsed} מכולות";
            else if (_greedyFallbackUsed && _greedyUnplaced.Count == 0)
                statusMessage = $"פתרון מלא (B&B + חמדני): {binsUsed} מכולות – לא מוכח כאופטימלי";
            else if (_greedyFallbackUsed && _greedyUnplaced.Count > 0)
                statusMessage = $"פתרון חלקי (B&B + חמדני): {binsUsed} מכולות, {_greedyUnplaced.Count} ארגזים לא שובצו";
            else
                statusMessage = $"פתרון טוב (לא בהכרח אופטימלי): {binsUsed} מכולות";

            return new PackingResult
            {
                PlacedBoxes       = placedBoxes,
                UnplacedBoxes     = unplaced,
                BinsUsed          = binsUsed,
                VolumeUtilization = binVolume > 0 ? totalVolume / binVolume : 0,
                SolveTime         = _stopwatch.Elapsed,
                IsOptimal         = isOptimal,
                StatusMessage     = statusMessage,
                PerBinStats       = perBinStats
            };
        }
    }
}


