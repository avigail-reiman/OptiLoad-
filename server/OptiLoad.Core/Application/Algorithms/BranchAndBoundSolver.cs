using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{
    public class BranchAndBoundSolver
    {
        private const int MaxGlobalNodes = 10_000_000;//כמות הצמתים המקסימלית שתיבדק לפני שהאלגוריתם יוותר ויעבור לפתרון חמדני

        public double TimeLimitSeconds { get; set; } = 3600.0;//כמות הזמן המקסימלית שהאלגוריתם ירוץ לפני שיוותר ויעבור לפתרון חמדני

        private readonly ContainerDimensions _container; //מידות המכולה שבה יש לארוז את הארגזים
        private readonly Stopwatch _stopwatch = new(); //שעון עצר למדידת זמן הריצה של האלגוריתם

        private int  _globalNodeCount;
        private int  _bestBins;//מספר המכולות הטוב ביותר שנמצא עד כה
        private bool _isOptimal;//דגל שמציין האם הפתרון הטוב ביותר שנמצא הוא אופטימלי או לא
        private bool _greedyFallbackUsed;//דגל שמציין האם נעשה שימוש בפתרון חמדני
        private List<BoxInstance> _greedyUnplaced = new();//רשימת הארגזים שלא נכללו בפתרון החמדני

        public double MaxFillHeightRatio { get; set; } = 1.0;//מגדיר עד איזה גובה מותר לארוז בתוך המכולה - ברירת מחדל 100%

        private List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)>//רשימת המיקומים הטובים ביותר שנמצאו עד כה - הארגז, המכולה שבה הוא ארוז, המיקום בתוך המכולה והסיבוב שלו
            _bestPlacements = new();//התחלה כריקה, תתמלא במהלך הריצה

        public BranchAndBoundSolver(ContainerDimensions container)//בנאי שמקבל את מידות המכולה שבה יש לארוז את הארגזים
        {
            _container = container;//שמירת המידות המכולה לשימוש במהלך הריצה
        }

public PackingResult Solve(IEnumerable<BoxInstance> instances)
        {
            _stopwatch.Restart();//מתחיל למדוד את זמן הריצה של האלגוריתם
            _greedyFallbackUsed = false;//לא נעשה שימוש בפתרון חמדני עדיין
            _greedyUnplaced = new List<BoxInstance>();//רשימת הארגזים שלא נכללו בפתרון החמדני מתחילה כריקה
            var allBoxes = instances.ToList();//רשימת כל הארגזים שיש לארוז, ממירה את הארגזים שהתקבלו לרשימה כדי לאפשר גישה לפי אינדקס
            if (allBoxes.Count == 0)//אם אין ארגזים לארוז
                return BuildResult(new(), allBoxes, 0, true);//בונה תוצאה עם רשימת מיקומים ריקה, רשימת כל הארגזים, 0 מכולות וסטטוס אופטימלי true

            var nonFragile = allBoxes.Where(b => !b.BoxDefinition.IsFragile).ToList();//מכניסה לרשימה רק ארגזים שאינם שבירים
            var fragile = allBoxes.Where(b =>  b.BoxDefinition.IsFragile).ToList();//מכניסה לרשימה רק ארגזים שבירים

            int lowerBound = LowerBoundCalculator.ComputeBestLowerBound(nonFragile, _container);//מחשב את החסם החסום ביותר מבין השלושה

            var h1 = new HeuristicH1(_container);//יוצר מופע של המכולה הנוכחית עם H1 החמדני
            var h1Phase1 = h1.Solve(nonFragile);//ריץ את הפותר החמדני על הארגזים הלא שבירים ומקבל את התוצאה של הפתרון
            _bestBins = h1Phase1.binsUsed;//מספר המכולות שהשתמש הפותר החמדני עד כה הוא הפתרון הטוב ביותר שנמצא
            _bestPlacements = h1Phase1.placements;//רשימת המיקומים שהפותר החמדני מצא עד כה היא הפתרון הטוב ביותר שנמצא

{
                var h1PlacedIds = h1Phase1.placements//מכניס את מזהי הארגזים שכבר שובצו על ידי הפותר החמדני לרשימה כדי לבדוק אילו ארגזים לא שובצו
                    .Select(p => p.instance.InstanceId)
                    .ToHashSet();
                var h1Unplaced = nonFragile//מכניס לרשימה את הארגזים הלא שבירים שלא שובצו על ידי הפותר החמדני על ידי סינון כל הארגזים הלא שבירים לפי אלו שכבר שובצו
                    .Where(b => !h1PlacedIds.Contains(b.InstanceId))
                    .ToList();
                if (h1Unplaced.Count > 0)//אם יש ארגזים לא שבירים שלא שובצו על ידי הפותר החמדני
                {
                    var h1Bins = RebuildBinsFromPlacements(h1Phase1.placements);//בונה את המכולות עם הארגזים שכבר שובצו על ידי הפותר החמדני כדי להשתמש בהן כמצב התחלתי לפתרון החמדני שלב 2
                    GreedySolver.FillRemaining(h1Unplaced, h1Bins, _container, MaxFillHeightRatio);
                    _bestBins       = h1Bins.Count;
                    _bestPlacements = ExtractPlacements(h1Bins);
                }
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

{
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
                }

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
            
            int overallLowerBound = Math.Max(lowerBound, lbFragile);
            _isOptimal = (_bestBins == overallLowerBound);

var binsForRepack = RebuildBinsFromPlacements(_bestPlacements);
            // repack רץ רק אם לוקח פחות ממגבלת הזמן + 10% (מינימום 2 שניות בונוס)
            double repackBudget = TimeLimitSeconds + Math.Max(2.0, TimeLimitSeconds * 0.1);
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
            }

var repackedPlacements = ExtractPlacements(binsForRepack);
            var repackedPlacedIds = repackedPlacements.Select(p => p.Item1.InstanceId).ToHashSet();
            var bruteUnplaced = allBoxes.Where(b => !repackedPlacedIds.Contains(b.InstanceId)).ToList();

            foreach (var bin in binsForRepack.Select((b, idx) => (b, idx)))
            {
                var toPlace = bruteUnplaced.ToList();
                foreach (var box in toPlace)
                {
                    bool placed = false;
                    var corners = CornerPointsFinder.Find3DCorners(bin.b.PlacedBoxes.ToList(), _container, new[] { box });
                    foreach (var corner in corners)
                    {
                        foreach (var rot in box.BoxDefinition.GetAllowedRotations())
                        {
                            var filler = CreateFiller();
                            if (filler.TryPlaceBox(bin.b, box, corner, rot, false, out var placedBox))
                            {
                                bin.b.AddBox(placedBox);
                                bruteUnplaced.Remove(box);
                                placed = true;
                                break;
                            }
                        }
                        if (placed) break;
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
            if (_globalNodeCount > MaxGlobalNodes) return;
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
                bool isEmpty = openBins[binIdx].UsedVolume < 1e-9;
                if (isEmpty)
                {
                    if (triedEmptyBin) continue;  
                    triedEmptyBin = true;
                }

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
                        
                        if (Math.Abs(corner.Y - currentLayerY.Value) > 1e-6)
                            continue;
                        
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
            
            openBins[binIdx].RemoveLastBox();
        }

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
        //הפונקציה יוצרת את כמות המכולות הנדרשות לפי המיקומים שנשלחים לה ומכניסה לתוכם את הארגזים לפי המיקומים
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

        // ⚠️ DEAD CODE — פונקציה זו לא נקראת מאף מקום בקוד
        private static List<(BoxInstance, int, Position3D, Rotation)> MergePlacements(
            List<PackingState> phase1OpenBins,
            List<PackingState> phase2OpenBins,
            bool               hasFragile)
        {
            
            if (hasFragile)
                return ExtractPlacements(phase2OpenBins);

            return ExtractPlacements(phase1OpenBins);
        }
        // ⚠️ END DEAD CODE
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

