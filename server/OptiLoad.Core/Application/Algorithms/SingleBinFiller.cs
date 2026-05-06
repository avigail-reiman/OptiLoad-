using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public class SingleBinFiller
    {
        
        private const int    MaxNodes = 10_000_000;  
        private const double Epsilon  = 1e-9;    

        public double MaxFillHeightRatio { get; set; } = 1.0;

private readonly ContainerDimensions _container;
        private int _nodeCount;

        public SingleBinFiller(ContainerDimensions container)
        {
            _container = container;
        }

public PackingState FillBin(
            IEnumerable<BoxInstance> instances,
            bool                     fragilePhase  = false,
            PackingState?            existingState = null)
        {
            
            var filtered = instances
                .Where(b => b.BoxDefinition.IsFragile == fragilePhase)
                .OrderByDescending(b => b.BoxDefinition.Volume)
                .ToList();

var currentState = existingState?.Clone() ?? new PackingState();
            var bestState    = currentState.Clone();
            _nodeCount       = 0;

            SearchRecursive(filtered, currentState, ref bestState, fragilePhase);
            return bestState;
        }

        public bool CanFitAll(IEnumerable<BoxInstance> instances)
        {
            var all = instances.ToList();

var stateAfterPhase1 = FillBin(all, fragilePhase: false);

            var nonFragile = all.Where(b => !b.BoxDefinition.IsFragile).ToList();
            double requiredNonFragile = nonFragile.Sum(b => b.BoxDefinition.Volume);
            if (stateAfterPhase1.UsedVolume < requiredNonFragile - Epsilon)
                return false;  

var stateAfterPhase2 = FillBin(all, fragilePhase: true,
                                           existingState: stateAfterPhase1);

            var fragile = all.Where(b => b.BoxDefinition.IsFragile).ToList();
            double requiredFragile  = fragile.Sum(b => b.BoxDefinition.Volume);
            double addedFragileVol  = stateAfterPhase2.UsedVolume
                                    - stateAfterPhase1.UsedVolume;

            return addedFragileVol >= requiredFragile - Epsilon;
        }

private void SearchRecursive(
            List<BoxInstance> allBoxes,
            PackingState      current,
            ref PackingState  best,
            bool              fragilePhase)
        {
            _nodeCount++;
            if (_nodeCount > MaxNodes) return;

if (current.UsedVolume > best.UsedVolume)
                best = current.Clone();

var remaining = GetRemainingBoxes(allBoxes, current);
            double upperBound = current.UsedVolume +
                                remaining.Sum(b => b.BoxDefinition.Volume);

if (upperBound <= best.UsedVolume + Epsilon) return;

var corners = CornerPointsFinder.Find3DCorners(
                current.PlacedBoxes,
                _container,
                remaining);

            if (corners.Count == 0) return;

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
                break; 
            }
        }

public bool TryPlaceBox(
            PackingState   state,
            BoxInstance    instance,
            Position3D     corner,
            Rotation       rotation,
            bool           fragilePhase,
            out PlacedBox? placed) =>
            TryPlace(state, instance, corner, rotation, fragilePhase, out placed);

        private bool TryPlace(
            PackingState   state,
            BoxInstance    instance,
            Position3D     corner,
            Rotation       rotation,
            bool           fragilePhase,
            out PlacedBox? placed)
        {
            placed = null;

double maxAllowedHeight = _container.Height * MaxFillHeightRatio;
            if (corner.X + rotation.W > _container.Width  + Epsilon ||
                corner.Y + rotation.H > maxAllowedHeight  + Epsilon ||
                corner.Z + rotation.D > _container.Depth  + Epsilon)
                return false;

if (state.UsedWeightKg + instance.BoxDefinition.WeightKg >
                _container.MaxWeightKg + Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

foreach (var existing in state.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }

if (fragilePhase)
            {
                double candX1 = corner.X,         candX2 = corner.X + rotation.W;
                double candY1 = corner.Y,         candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z,         candZ2 = corner.Z + rotation.D;

                foreach (var existing in state.PlacedBoxes)
                {
                    if (existing.Instance.BoxDefinition.IsFragile) continue;

if (existing.Y1 < candY2 - Epsilon &&
                        existing.Y2 > candY1 + Epsilon &&
                        existing.Y1 >= candY1 - Epsilon)
                    {
                        
                        bool overlapX = existing.X1 < candX2 - Epsilon &&
                                        existing.X2 > candX1 + Epsilon;
                        if (!overlapX) continue;

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
