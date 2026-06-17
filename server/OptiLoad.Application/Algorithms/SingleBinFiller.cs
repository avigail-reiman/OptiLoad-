using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Application.Algorithms
{

    public class SingleBinFiller
    {
        public double MaxFillHeightRatio { get; set; } = AlgorithmConfig.DefaultMaxFillHeightRatio;

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

        // ⚠️ DEAD CODE — פונקציה זו לא נקראת מאף מקום בקוד
        public bool CanFitAll(IEnumerable<BoxInstance> instances)
        {
            var all = instances.ToList();

var stateAfterPhase1 = FillBin(all, fragilePhase: false);

            var nonFragile = all.Where(b => !b.BoxDefinition.IsFragile).ToList();
            double requiredNonFragile = nonFragile.Sum(b => b.BoxDefinition.Volume);
            if (stateAfterPhase1.UsedVolume < requiredNonFragile - AlgorithmConfig.Epsilon)
                return false;  

var stateAfterPhase2 = FillBin(all, fragilePhase: true,
                                           existingState: stateAfterPhase1);

            var fragile = all.Where(b => b.BoxDefinition.IsFragile).ToList();
            double requiredFragile  = fragile.Sum(b => b.BoxDefinition.Volume);
            double addedFragileVol  = stateAfterPhase2.UsedVolume
                                    - stateAfterPhase1.UsedVolume;

            return addedFragileVol >= requiredFragile - AlgorithmConfig.Epsilon;
        }
        // ⚠️ END DEAD CODE

private void SearchRecursive(
            List<BoxInstance> allBoxes,
            PackingState      current,
            ref PackingState  best,
            bool              fragilePhase)
        {
            _nodeCount++;
            if (_nodeCount > AlgorithmConfig.SingleBinMaxNodes) return;

if (current.UsedVolume > best.UsedVolume)
                best = current.Clone();

var remaining = GetRemainingBoxes(allBoxes, current);
            double upperBound = current.UsedVolume +
                                remaining.Sum(b => b.BoxDefinition.Volume);

if (upperBound <= best.UsedVolume + AlgorithmConfig.Epsilon) return;

var corners = CornerPointsFinder.Find3DCorners(
                current.PlacedBoxes,
                _container,
                remaining);

            if (corners.Count == 0) return;

            if (remaining.Count > 0)
                TryAllOrientationsInSearch(remaining[0], corners, current, allBoxes, ref best, fragilePhase);
        }

        //עובר על כל הסיבובים וכל נקודות הפינה לארגז נתון במסגרת הביקוש הרקורסיבי
        private void TryAllOrientationsInSearch(
            BoxInstance       instance,
            List<Position3D>  corners,
            PackingState      current,
            List<BoxInstance> allBoxes,
            ref PackingState  best,
            bool              fragilePhase)
        {
            foreach (var rotation in instance.BoxDefinition.GetAllowedRotations())
            {
                foreach (var corner in corners)
                {
                    if (_nodeCount > AlgorithmConfig.SingleBinMaxNodes) return;
                    if (TryPlace(current, instance, corner, rotation, fragilePhase, out var placed))
                    {
                        current.AddBox(placed!);
                        SearchRecursive(allBoxes, current, ref best, fragilePhase);
                        current.RemoveLastBox();
                    }
                }
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
            if (corner.X + rotation.W > _container.Width  + AlgorithmConfig.Epsilon ||
                corner.Y + rotation.H > maxAllowedHeight  + AlgorithmConfig.Epsilon ||
                corner.Z + rotation.D > _container.Depth  + AlgorithmConfig.Epsilon)
                return false;

if (state.UsedWeightKg + instance.BoxDefinition.WeightKg >
                _container.MaxWeightKg + AlgorithmConfig.Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

foreach (var existing in state.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }
            // אין להניח ארגז על גבי ארגז שביר
            foreach (var existing in state.PlacedBoxes)
            {
                if (existing.Instance.BoxDefinition.IsFragile)
                {
                    if (Math.Abs(corner.Y - existing.Y2) < AlgorithmConfig.Epsilon)
                    {
                        bool overlapX = corner.X < existing.X2 - AlgorithmConfig.Epsilon &&
                                        corner.X + rotation.W > existing.X1 + AlgorithmConfig.Epsilon;
                        bool overlapZ = corner.Z < existing.Z2 - AlgorithmConfig.Epsilon &&
                                        corner.Z + rotation.D > existing.Z1 + AlgorithmConfig.Epsilon;
                        if (overlapX && overlapZ) return false;
                    }
                }
            }
if (fragilePhase)
            {
                double candX1 = corner.X,         candX2 = corner.X + rotation.W;
                double candY1 = corner.Y,         candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z,         candZ2 = corner.Z + rotation.D;

                foreach (var existing in state.PlacedBoxes)
                {
                    if (!existing.Instance.BoxDefinition.IsFragile)
                    {
                        if (existing.Y1 < candY2 - AlgorithmConfig.Epsilon &&
                            existing.Y2 > candY1 + AlgorithmConfig.Epsilon &&
                            existing.Y1 >= candY1 - AlgorithmConfig.Epsilon)
                        {
                            bool overlapX = existing.X1 < candX2 - AlgorithmConfig.Epsilon &&
                                            existing.X2 > candX1 + AlgorithmConfig.Epsilon;
                            bool overlapZ = overlapX &&
                                            existing.Z1 < candZ2 - AlgorithmConfig.Epsilon &&
                                            existing.Z2 > candZ1 + AlgorithmConfig.Epsilon;
                            if (overlapZ)
                                return false;
                        }
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

