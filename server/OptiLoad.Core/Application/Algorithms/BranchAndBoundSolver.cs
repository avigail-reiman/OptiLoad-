using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public class BranchAndBoundSolver
    {
        
        private const int MaxGlobalNodes = 10_000_000;

        public double TimeLimitSeconds { get; set; } = 3600.0;

private readonly ContainerDimensions _container;
        private readonly Stopwatch           _stopwatch = new();

        private int  _globalNodeCount;
        private int  _bestBins;
        private bool _isOptimal;
        private bool _greedyFallbackUsed;
        private List<BoxInstance> _greedyUnplaced = new();

        public double MaxFillHeightRatio { get; set; } = 1.0;

private List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)>
            _bestPlacements = new();

        public BranchAndBoundSolver(ContainerDimensions container)
        {
            _container = container;
        }

public PackingResult Solve(IEnumerable<BoxInstance> instances)
        {
            _stopwatch.Restart();
            _greedyFallbackUsed = false;
            _greedyUnplaced     = new List<BoxInstance>();
            var allBoxes = instances.ToList(); 
            if (allBoxes.Count == 0)
                return BuildResult(new(), allBoxes, 0, true);

var nonFragile = allBoxes.Where(b => !b.BoxDefinition.IsFragile).ToList();
            var fragile    = allBoxes.Where(b =>  b.BoxDefinition.IsFragile).ToList();

int lowerBound = LowerBoundCalculator.ComputeBestLowerBound(nonFragile, _container);

            var h1           = new HeuristicH1(_container);
            var h1Phase1     = h1.Solve(nonFragile);
            _bestBins        = h1Phase1.binsUsed;
            _bestPlacements  = h1Phase1.placements;

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

_globalNodeCount = 0;
            var sortedNonFragile = nonFragile
                .OrderByDescending(b => b.BoxDefinition.Volume).ToList();

            var phase1Assignment = new List<(BoxInstance instance, int bin)>();
            var phase1OpenBins   = new List<PackingState>();

            BranchMain(sortedNonFragile, phase1Assignment,
                       phase1OpenBins, lowerBound, fragilePhase: false);

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

                BranchMain(sortedFragile, phase2Assignment,
                           phase2OpenBins, lbFragile, fragilePhase: true,
                           binOffset: bestPhase1Bins.Count);

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
            bool moved;
            do
            {
                moved = false;
                
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

var repackedPlacements = ExtractPlacements(binsForRepack);
            var repackedPlacedIds = repackedPlacements.Select(p => p.Item1.InstanceId).ToHashSet();
            var bruteUnplaced = allBoxes.Where(b => !repackedPlacedIds.Contains(b.InstanceId)).ToList();

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

private void BranchMain(
            List<BoxInstance>                     allBoxes,
            List<(BoxInstance instance, int bin)> currentAssignment,
            List<PackingState>                    openBins,
            int                                   lowerBound,
            bool                                  fragilePhase,
            int                                   binOffset = 0)
        {
            _globalNodeCount++;

if (_globalNodeCount > MaxGlobalNodes) return;

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

_bestBins       = bestBins.Count;
                    _bestPlacements = ExtractPlacements(bestBins);
                }
                return;
            }

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
                (int)Math.Ceiling(Math.Max(0.0, remainingVol - freeCapacityInOpenBins)
                                  / _container.Volume));

            int newBinsUsed = openBins.Count - binOffset;  
            int bestNewBins = _bestBins - binOffset;         
            if (newBinsUsed + lb >= bestNewBins) return;

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

        private static List<(BoxInstance, int, Position3D, Rotation)> MergePlacements(
            List<PackingState> phase1OpenBins,
            List<PackingState> phase2OpenBins,
            bool               hasFragile)
        {
            
            if (hasFragile)
                return ExtractPlacements(phase2OpenBins);

            return ExtractPlacements(phase1OpenBins);
        }

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

private PackingResult BuildResult(
            List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements,
            List<BoxInstance>                                                    allBoxes,
            int                                                                  binsUsed,
            bool                                                                 isOptimal)
        {
            var placedIds = placements.Select(p => p.instance.InstanceId).ToHashSet();

var greedyUnplacedIds = _greedyUnplaced
                .Select(b => b.InstanceId)
                .ToHashSet();

            var unplaced = allBoxes
                .Where(b => !placedIds.Contains(b.InstanceId))
                .Where(b =>  greedyUnplacedIds.Contains(b.InstanceId)
                          || !_greedyFallbackUsed)
                .ToList();

var rawPlaced = placements
                .Select(p => new PlacedBox(p.instance, p.pos, p.rot) { BinIndex = p.bin })
                .ToList();

var placedBoxes = rawPlaced
                .GroupBy(pb => pb.BinIndex)
                .SelectMany(g => GravitySettler.Settle(g.ToList(), _container))
                .ToList();

            double totalVolume = placedBoxes.Sum(pb => pb.Volume);
            double binVolume   = _container.Volume * Math.Max(1, binsUsed);

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

