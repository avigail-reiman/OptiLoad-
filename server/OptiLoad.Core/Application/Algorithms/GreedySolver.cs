using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public static class GreedySolver
    {
        private const double Epsilon = 1e-9;

public static List<BoxInstance> FillRemaining(
            List<BoxInstance>   remainingBoxes,
            List<PackingState>  openBins,
            ContainerDimensions container,
            double              maxFillHeightRatio = 1.0)
        {
            var unplaced = new List<BoxInstance>();

var sorted = remainingBoxes
                .OrderByDescending(b => b.BoxDefinition.Volume)
                .ToList();

            foreach (var instance in sorted)
            {
                bool placed = false;

foreach (var bin in openBins.OrderByDescending(b => b.UsedVolume))
                {
                    if (TryPlaceGreedy(instance, bin, container, maxFillHeightRatio))
                    {
                        placed = true;
                        break;
                    }
                }

if (!placed)
                {
                    var newBin = new PackingState();
                    if (TryPlaceGreedy(instance, newBin, container, maxFillHeightRatio))
                    {
                        openBins.Add(newBin);
                        placed = true;
                    }
                }

if (!placed)
                    unplaced.Add(instance);
            }

            return unplaced;
        }

private static bool TryPlaceGreedy(
            BoxInstance         instance,
            PackingState        bin,
            ContainerDimensions container,
            double              maxFillHeightRatio)
        {
            
            var corners = CornerPointsFinder.Find3DCorners(
                bin.PlacedBoxes,
                container);

            bool isFragile = instance.BoxDefinition.IsFragile;

var sortedCorners = corners.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();

            foreach (var rotation in instance.BoxDefinition.GetAllowedRotations())
            {
                foreach (var corner in sortedCorners)
                {
                    if (IsValidPlacement(instance, corner, rotation,
                                         bin, container, maxFillHeightRatio, isFragile))
                    {
                        var placed = new PlacedBox(instance, corner, rotation);
                        bin.AddBox(placed);
                        return true;
                    }
                }
            }

            return false;
        }

private static bool IsValidPlacement(
            BoxInstance         instance,
            Position3D          corner,
            Rotation            rotation,
            PackingState        bin,
            ContainerDimensions container,
            double              maxFillHeightRatio,
            bool                isFragile)
        {
            double maxHeight = container.Height * maxFillHeightRatio;

if (corner.X + rotation.W > container.Width  + Epsilon ||
                corner.Y + rotation.H > maxHeight        + Epsilon ||
                corner.Z + rotation.D > container.Depth  + Epsilon)
                return false;

if (bin.UsedWeightKg + instance.BoxDefinition.WeightKg >
                container.MaxWeightKg + Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

foreach (var existing in bin.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }

if (isFragile)
            {
                double candX1 = corner.X, candX2 = corner.X + rotation.W;
                double candY1 = corner.Y, candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z, candZ2 = corner.Z + rotation.D;

                foreach (var existing in bin.PlacedBoxes)
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

                        if (overlapZ) return false;
                    }
                }
            }

            return true;
        }
    }
}
