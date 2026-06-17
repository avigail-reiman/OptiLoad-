using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Application.Algorithms
{
    public class BranchAndBoundSolver
    {
        public double TimeLimitSeconds { get; set; } = AlgorithmConfig.DefaultTimeLimitSeconds;//כמות הזמן המקסימלית שהאלגוריתם ירוץ לפני שיוותר ויעבור לפתרון חמדני

        private readonly ContainerDimensions _container; //מידות המכולה שבה יש לארוז את הארגזים
        private readonly Stopwatch _stopwatch = new(); 

        private int  _globalNodeCount;
        private int  _bestBins;//מספר המכולות הטוב ביותר שנמצא עד כה
        private bool _isOptimal;
        private bool _greedyFallbackUsed;
        private List<BoxInstance> _greedyUnplaced = new();//רשימת הארגזים שלא נכללו בפתרון החמדני

        public double MaxFillHeightRatio { get; set; } = AlgorithmConfig.DefaultMaxFillHeightRatio;//מגדיר עד איזה גובה מותר לארוז בתוך המכולה - ברירת מחדל 100%

        private List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)>//רשימת המיקומים הטובים ביותר שנמצאו עד כה - הארגז, המכולה שבה הוא ארוז, המיקום בתוך המכולה והסיבוב שלו
            _bestPlacements = new();//התחלה כריקה, תתמלא במהלך הריצה

        public BranchAndBoundSolver(ContainerDimensions container)//בנאי שמקבל את מידות המכולה שבה יש לארוז את הארגזים
        {
            _container = container;//שמירת המידות המכולה לשימוש במהלך הריצה
        }

public PackingResult Solve(IEnumerable<BoxInstance> instances)
        {
            _stopwatch.Restart();
            _greedyFallbackUsed = false;
            _greedyUnplaced = new List<BoxInstance>();
            var allBoxes = instances.ToList();
            if (allBoxes.Count == 0)
                return BuildResult(new(), allBoxes, 0, true);
            var nonFragile = allBoxes.Where(b => !b.BoxDefinition.IsFragile).ToList();
            var fragile = allBoxes.Where(b =>  b.BoxDefinition.IsFragile).ToList();
            int lowerBound = LowerBoundCalculator.ComputeBestLowerBound(nonFragile, _container);
            var h1 = new HeuristicH1(_container);
            var h1Phase1 = h1.Solve(nonFragile);
            _bestBins = h1Phase1.binsUsed;
            _bestPlacements = h1Phase1.placements;

                var h1PlacedIds = h1Phase1.placements                   
                    .Select(p => p.instance.InstanceId)
                    .ToHashSet();
                var h1Unplaced = nonFragile//מכניס לרשימה את הארגזים הלא שבירים שלא שובצו על ידי הפותר החמדני על ידי סינון כל הארגזים הלא שבירים לפי אלו שכבר שובצו
                    .Where(b => !h1PlacedIds.Contains(b.InstanceId))
                    .ToList();
                if (h1Unplaced.Count > 0)//אם יש ארגזים לא שבירים שלא שובצו על ידי הפותר החמדני
                {
                    var h1Bins = RebuildBinsFromPlacements(h1Phase1.placements);
                    GreedySolver.FillRemaining(h1Unplaced, h1Bins, _container, MaxFillHeightRatio);
                    _bestBins = h1Bins.Count;
                    _bestPlacements = ExtractPlacements(h1Bins);
                }

                _globalNodeCount = 0;
                var sortedNonFragile = nonFragile
                    .OrderByDescending(b => b.BoxDefinition.Volume).ToList();
                var phase1Assignment = new List<(BoxInstance instance, int bin)>();
                var phase1OpenBins   = new List<PackingState>();

                if (_bestBins > lowerBound)
                {
                    BranchMain(sortedNonFragile, phase1Assignment,
                            phase1OpenBins, lowerBound, fragilePhase: false);
                }

                var bestPhase1Bins = RebuildBinsFromPlacements(_bestPlacements);

                int lbFragile = 0;
                if (fragile.Count > 0)
                {
                    lbFragile = LowerBoundCalculator
                        .ComputeBestLowerBound(fragile, _container);

                    var sortedFragile = fragile
                        .OrderByDescending(b => b.BoxDefinition.Volume).ToList();

                    var phase2Assignment = new List<(BoxInstance instance, int bin)>();

                    var phase2OpenBins = bestPhase1Bins;
                    var h1Fragile       = new HeuristicH1(_container);
                    var h1FragileResult = h1Fragile.Solve(sortedFragile);

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

                    int binOffset = phase2OpenBins.Count;
                    var offsetFragileH1 = fragileH1Placements
                        .Select(p => (p.Item1, p.Item2 + binOffset, p.Item3, p.Item4))
                        .ToList();

                    var initialSolution = _bestPlacements.Concat(offsetFragileH1).ToList();
                    _bestPlacements = initialSolution;
                    _bestBins = _bestPlacements.Count > 0
                        ? _bestPlacements.Select(p => p.bin).Max() + 1
                        : 0;

                int fragileCurrentBins = _bestPlacements.Count > 0
                    ? _bestPlacements.Select(p => p.bin).Max() + 1 - bestPhase1Bins.Count
                    : 0;
                if (fragileCurrentBins > lbFragile)
                {
                    BranchMain(sortedFragile, phase2Assignment,
                               phase2OpenBins, lbFragile, fragilePhase: true,
                               binOffset: bestPhase1Bins.Count);
                }

                bool allFragilePlaced = fragile.All(f =>
                    _bestPlacements.Any(p => p.instance.InstanceId == f.InstanceId));

                if (allFragilePlaced)
                {
                    
                    _bestBins = _bestPlacements.Select(p => p.bin).Max() + 1;
                }
                else
                {

                    _bestBins = _bestPlacements.Count > 0
                        ? _bestPlacements.Select(p => p.bin).Max() + 1
                        : 0;
                }
                }

            var bins = RebuildBinsFromPlacements(_bestPlacements);
            var unplaced = allBoxes.Where(b => !_bestPlacements.Any(p => p.instance.InstanceId == b.InstanceId)).ToList();
            bool added;
            do
            {
                added = TryFillUnplacedBoxes(unplaced, bins);
            } while (added);

            _stopwatch.Stop();
            
            int overallLowerBound = Math.Max(lowerBound, lbFragile);
            _isOptimal = (_bestBins == overallLowerBound);

            var binsForRepack = RebuildBinsFromPlacements(_bestPlacements);
            
            double repackBudget = TimeLimitSeconds + Math.Max(AlgorithmConfig.RepackMinBonusSeconds, TimeLimitSeconds * AlgorithmConfig.RepackBonusFactor);
            if (_stopwatch.Elapsed.TotalSeconds < repackBudget)
            {
                bool moved;
                do
                {
                    moved = false;
                    if (_stopwatch.Elapsed.TotalSeconds > repackBudget) break;
                    for (int fromBin = 1; fromBin < binsForRepack.Count; fromBin++)
                    {
                        var boxesToTry = binsForRepack[fromBin].PlacedBoxes.ToList();
                        foreach (PlacedBox box in boxesToTry)
                        {
                            if (TryMoveToEarlierBin(box, fromBin, binsForRepack))
                                moved = true;
                        }
                    }
                } while (moved);
            }

            var repackedPlacements = ExtractPlacements(binsForRepack);
            var repackedPlacedIds = repackedPlacements.Select(p => p.Item1.InstanceId).ToHashSet();
            var bruteUnplaced = allBoxes.Where(b => !repackedPlacedIds.Contains(b.InstanceId)).ToList();

            foreach (var bin in binsForRepack.Select((b, idx) => (b, idx)))
            {
                var toPlace = bruteUnplaced.ToList();
                foreach (var box in toPlace)
                {
                    if (TryPlaceBoxAtCorners(box, bin.b, false, out var placed))
                    {
                        bin.b.AddBox(placed!);
                        bruteUnplaced.Remove(box);
                    }
                }
            }

            var brutePlacements = ExtractPlacements(binsForRepack);
            int bruteBinsUsed = binsForRepack.Count(b => b.PlacedBoxes.Count > 0);

            // Recompute _isOptimal after repack — the repack may reduce bin count below the pre-repack _bestBins
            int overallLB = Math.Max(lowerBound, lbFragile);
            bool finalIsOptimal = _isOptimal || (bruteBinsUsed == overallLB);

            return BuildResult(brutePlacements, allBoxes, bruteBinsUsed, finalIsOptimal);
        }

private void BranchMain(
            List<BoxInstance> allBoxes,
            List<(BoxInstance instance, int bin)> currentAssignment,
            List<PackingState> openBins,
            int lowerBound,
            bool fragilePhase,
            int binOffset = 0)
        {
            _globalNodeCount++;
                        if (_globalNodeCount > AlgorithmConfig.MaxGlobalNodes) return;
            if (_bestBins <= lowerBound) return;
            if (_stopwatch.Elapsed.TotalSeconds > TimeLimitSeconds)
            {
                if (!_greedyFallbackUsed)
                {
                    _greedyFallbackUsed = true;
                    var bestBins = RebuildBinsFromPlacements(_bestPlacements);
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

                    _bestBins = bestBins.Count;
                    _bestPlacements = ExtractPlacements(bestBins);
                }
                return;
            }
            //כאשר העץ מלא - תוצאה סופית
            if (currentAssignment.Count == allBoxes.Count)
            {
                int usedBins = openBins.Count;
                if (usedBins < _bestBins)
                {
                    _bestBins = usedBins;
                    _bestPlacements = ExtractPlacements(openBins);
                }
                return;
            }

            var remainingBoxes = allBoxes.Skip(currentAssignment.Count).ToList();
            double? currentLayerY = null;
            double? currentLayerHeight = null;
            if (remainingBoxes.Count > 0)
            {
                var largestBox = remainingBoxes.OrderByDescending(b => b.BoxDefinition.Height).First();
                currentLayerHeight = largestBox.BoxDefinition.Height;
                
                currentLayerY = openBins.SelectMany(bin => bin.PlacedBoxes.Select(pb => pb.Y2)).DefaultIfEmpty(0).Max();
            }

            var nextBox = allBoxes[currentAssignment.Count];

            var remaining = allBoxes.Skip(currentAssignment.Count).ToList();

            double freeCapacityInOpenBins = openBins.Sum(b => _container.Volume - b.UsedVolume);
            double remainingVol = remaining.Sum(b => b.BoxDefinition.Volume);
            int lb = Math.Max(0,
                (int)Math.Ceiling(Math.Max(0.0, remainingVol - freeCapacityInOpenBins) / _container.Volume));

            int newBinsUsed = openBins.Count - binOffset;  
            int bestNewBins = _bestBins - binOffset;         
            if (newBinsUsed + lb >= bestNewBins) return;
            //בודק סימטריות על ארגז קודם זהה
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
                //בודק סימטריות על מכולות זהות - ריקות
                bool isEmpty = openBins[binIdx].UsedVolume < AlgorithmConfig.Epsilon;
                bool skipBin = isEmpty && triedEmptyBin;
                if (!skipBin)
                {
                    if (isEmpty) triedEmptyBin = true;

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
            }

            if (!placedInExisting && openBins.Count - binOffset + 1 < bestNewBins)
            {
                var initFiller = CreateFiller();
                PlacedBox? initPlacement = null;
                double initBestX2 = double.MaxValue;
                double initBestY2 = double.MaxValue;

                foreach (var rot in nextBox.BoxDefinition.GetAllowedRotations())
                {
                    if (initFiller.TryPlaceBox(new PackingState(), nextBox,
                                               new Position3D(0, 0, 0), rot,
                                               fragilePhase, out var p))
                    {
                        if (p!.X2 < initBestX2 - AlgorithmConfig.Epsilon ||
                            (p.X2 <= initBestX2 + AlgorithmConfig.Epsilon && p.Y2 < initBestY2 - AlgorithmConfig.Epsilon))
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

private bool TryAssignToBin_Exhaustive(
            BoxInstance box,
            int binIdx,
            List<PackingState> openBins,
            bool fragilePhase,
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
                    
                    if (currentLayerY.HasValue && currentLayerHeight.HasValue)
                    {
                        bool layerOk =
                            Math.Abs(corner.Y - currentLayerY.Value) <= AlgorithmConfig.LayerEpsilon &&
                            corner.Y + rotation.H <= currentLayerY.Value + currentLayerHeight.Value + AlgorithmConfig.LayerEpsilon;

                        if (layerOk && filler.TryPlaceBox(currentState, box, corner, rotation,
                                              fragilePhase, out var placed))
                        {
                            currentState.AddBox(placed!);
                            placement = placed;
                            return true;
                        }
                    }
                    else if (filler.TryPlaceBox(currentState, box, corner, rotation,
                                          fragilePhase, out var placed))
                    {
                        currentState.AddBox(placed!);
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
            
            openBins[binIdx].RemoveLastBox();
        }

private static bool SameBoxType(BoxInstance a, BoxInstance b)
        {
            var da = a.BoxDefinition;
            var db = b.BoxDefinition;
            return Math.Abs(da.Width  - db.Width)  < AlgorithmConfig.Epsilon &&
                   Math.Abs(da.Height - db.Height) < AlgorithmConfig.Epsilon &&
                   Math.Abs(da.Depth  - db.Depth)  < AlgorithmConfig.Epsilon &&
                   Math.Abs(da.WeightKg - db.WeightKg) < AlgorithmConfig.Epsilon &&
                   da.IsFragile     == db.IsFragile &&
                   da.AllowRotation == db.AllowRotation;
        }
        //הפונקציה יוצרת את כמות המכולות הנדרשות לפי המיקומים שנשלחים לה ומכניסה לתוכם את הארגזים לפי המיקומים
        private static List<PackingState> RebuildBinsFromPlacements(
            List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements)
        {
            if (placements.Count == 0) return new List<PackingState>();

            int maxBin = placements.Max(p => p.bin);
            var bins = Enumerable.Range(0, maxBin + 1)
                                   .Select(_ => new PackingState())
                                   .ToList();

            foreach (var (instance, bin, pos, rot) in placements)
                bins[bin].AddBox(new PlacedBox(instance, pos, rot) { BinIndex = bin });

            return bins;
        }

        // קוד מת!!!!
        private static List<(BoxInstance, int, Position3D, Rotation)> MergePlacements(
            List<PackingState> phase1OpenBins,
            List<PackingState> phase2OpenBins,
            bool               hasFragile)
        {
            
            if (hasFragile)
                return ExtractPlacements(phase2OpenBins);

            return ExtractPlacements(phase1OpenBins);
        }
        // סיום קוד מת!
//המרה של נתוני הארגזים שנארזו במכולות השונות לרשימה שטוחה
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

        //מנסה למקם את הארגזים שלא שובצו במכולות קיימות — מחזיר true אם שובץ אחד לפחות
        private bool TryFillUnplacedBoxes(List<BoxInstance> unplaced, List<PackingState> bins)
        {
            foreach (var box in unplaced.ToList())
            {
                for (int binIdx = 0; binIdx < bins.Count; binIdx++)
                {
                    if (TryPlaceBoxAtCorners(box, bins[binIdx], false, out var placed))
                    {
                        bins[binIdx].AddBox(placed!);
                        _bestPlacements = _bestPlacements.Concat(new[] { (box, binIdx, placed!.Position, placed.Rotation) }).ToList();
                        unplaced.Remove(box);
                        return true;
                    }
                }
            }
            return false;
        }

        //מנסה למקם ארגז בתוך מכולה על ידי בדיקת כל נקודות הפינה וכל הסיבובים האפשריים
        private bool TryPlaceBoxAtCorners(
            BoxInstance    box,
            PackingState   bin,
            bool           fragilePhase,
            out PlacedBox? result)
        {
            var corners = CornerPointsFinder.Find3DCorners(
                bin.PlacedBoxes.ToList(), _container, new[] { box });
            var filler = CreateFiller();
            foreach (var corner in corners)
            {
                foreach (var rot in box.BoxDefinition.GetAllowedRotations())
                {
                    if (filler.TryPlaceBox(bin, box, corner, rot, fragilePhase, out var placed))
                    {
                        result = placed;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        //מנסה להעביר ארגז ממכולה אחת למכולה קודמת — חלק משלב ה-repack
        private bool TryMoveBoxToBin(PlacedBox box, PackingState fromBin, PackingState toBin)
        {
            if (!TryPlaceBoxAtCorners(box.Instance, toBin, false, out var placed))
                return false;
            toBin.AddBox(placed!);
            ((List<PlacedBox>)fromBin.PlacedBoxes).Remove(box);
            return true;
        }

        //מנסה להעביר ארגז לכל אחת מהמכולות הקודמות לו
        private bool TryMoveToEarlierBin(PlacedBox box, int fromBinIdx, List<PackingState> bins)
        {
            for (int toBin = 0; toBin < fromBinIdx; toBin++)
            {
                if (TryMoveBoxToBin(box, bins[fromBinIdx], bins[toBin]))
                    return true;
            }
            return false;
        }

private SingleBinFiller CreateFiller() =>
            new SingleBinFiller(_container)
            {
                MaxFillHeightRatio = MaxFillHeightRatio
            };
//פונקציה שמקבלת את נתוני הפתרון ומחזירה אותם לתמשתמש כתוצאה סופית
private PackingResult BuildResult(
            List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements,
            List<BoxInstance> allBoxes,
            int binsUsed,
            bool isOptimal)
        {
            var placedIds = placements.Select(p => p.instance.InstanceId).ToHashSet();//ממירה את רשימת המיקומים לרשימת הארגזים כדי לבדוק אילו ארגזים שובצו בפועל

            var greedyUnplacedIds = _greedyUnplaced//בודק אילו ארגזים לא נארזו גם אחרי החמדני
                .Select(b => b.InstanceId)
                .ToHashSet();

            var unplaced = allBoxes//בדיקה אלו ארגזים לא שובצו כלל לא על ידי החמדני ולא בכלל
                .Where(b => !placedIds.Contains(b.InstanceId))
                .Where(b =>  greedyUnplacedIds.Contains(b.InstanceId)
                          || !_greedyFallbackUsed)
                .ToList();
            //הופך כל שורה מרשימת המיקומים שקיבל למופע של ארגז ממוקם הכולל את מיקומו, אופן הסיבוב, באיזה מכולה נמצא וכו'
            var rawPlaced = placements
                .Select(p => new PlacedBox(p.instance, p.pos, p.rot) { BinIndex = p.bin })
                .ToList();
            //מחלק את הארגזים לפי מכולות ומפעיל על כל מכולה את ה-GravitySettler כדי לסדר את הארגזים בצורה יותר צפופה בתוך המכולה, ומחזיר רשימה חדשה של ארגזים ממוקמים לאחר הסידור
            var placedBoxes = rawPlaced
                .GroupBy(pb => pb.BinIndex)
                .SelectMany(g => GravitySettler.Settle(g.ToList(), _container))
                .ToList();
            //חישוב הנפח הכולל
            double totalVolume = placedBoxes.Sum(pb => pb.Volume);
            //חישוב הנפח שנוצל מתוך המכולות
            double binVolume   = _container.Volume * Math.Max(1, binsUsed);
            //חישוב סטטיסטיקות לכל מכולה בנפרד - כמה נפח נוצל וכמה נפח כולל יש במכולה
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

            string statusMessage;//בניית הודעת סטטוס
            //אם הפתרון אופטימלי, מציג את מספר המכולות שנמצא
            if (isOptimal)
                statusMessage = $"פתרון אופטימלי נמצא: {binsUsed} מכולות";
            //אם לא אופטימלי אבל נעשה שימוש בפתרון חמדני והכל שובץ, מציג את מספר המכולות ומציין שלא מוכח כאופטימלי
            else if (_greedyFallbackUsed && _greedyUnplaced.Count == 0)
                statusMessage = $"פתרון מלא (B&B + חמדני): {binsUsed} מכולות – לא מוכח כאופטימלי";
            //אם לא אופטימלי ונעשה שימוש בפתרון חמדני אבל יש ארגזים שלא שובצו, מציג את מספר המכולות ומספר הארגזים שלא שובצו
            else if (_greedyFallbackUsed && _greedyUnplaced.Count > 0)
                statusMessage = $"פתרון חלקי (B&B + חמדני): {binsUsed} מכולות, {_greedyUnplaced.Count} ארגזים לא שובצו";
            //אם לא אופטימלי ולא נעשה שימוש בפתרון חמדני, מציג את מספר המכולות ומציין שזה פתרון טוב אבל לא מוכח כאופטימלי
            else
                statusMessage = $"פתרון טוב (לא בהכרח אופטימלי): {binsUsed} מכולות";

            //מחזיר את תוצאת האריזה הכוללת את הארגזים שהושמו, הארגזים שלא הושמו, מספר המכולות שנמצאו, ניצול הנפח, זמן הריצה, האם הפתרון אופטימלי והסטטיסטיקות לכל מכולה
            return new PackingResult
            {
                PlacedBoxes       = placedBoxes,//רשימת הארגזים שהושמו כולל מיקומם, סיבובם וכו'
                UnplacedBoxes     = unplaced,//רשימת הארגזים שלא הושמו
                BinsUsed          = binsUsed,//כמות המכולות שנמצאו
                VolumeUtilization = binVolume > 0 ? totalVolume / binVolume : 0,//ניצול הנפח הכולל של המכולות
                SolveTime         = _stopwatch.Elapsed,//זמן הריצה של האלגוריתם
                IsOptimal         = isOptimal,//האם הפתרון אופטימלי
                StatusMessage     = statusMessage,//הודעת סטטוס המתארת את הפתרון
                PerBinStats       = perBinStats//סטטיסטיקות לכל מכולה בנפרד

            };
        }
    }
}

