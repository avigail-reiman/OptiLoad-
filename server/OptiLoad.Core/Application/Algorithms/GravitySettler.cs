using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public static class GravitySettler
    {

        public static List<PlacedBox> Settle(List<PlacedBox> boxes, ContainerDimensions? container = null)
        {
            if (boxes.Count == 0) return boxes;

            var current = boxes
                .Select(b => new PlacedBox(b.Instance, b.Position, b.Rotation) { BinIndex = b.BinIndex })
                .ToList();

const int MaxIterations = 10;
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                
                var prevPos = current.ToDictionary(
                    b => b.Instance,
                    b => (b.X1, b.Y1, b.Z1));

                current = CompactX(current, container);
                current = CompactZ(current, container);
                current = CompactY(current, container);

bool changed = current.Any(b =>
                    prevPos.TryGetValue(b.Instance, out var p) && (
                        Math.Abs(b.X1 - p.X1) > AlgorithmConfig.Epsilon ||
                        Math.Abs(b.Y1 - p.Y1) > AlgorithmConfig.Epsilon ||
                        Math.Abs(b.Z1 - p.Z1) > AlgorithmConfig.Epsilon));

                if (!changed) break;
            }

            return current;
        }

private static List<PlacedBox> CompactX(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result  = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.X1))
            {
                double newX = 0.0;
                foreach (var placed in result)
                {
                    bool overlapY = box.Y1 < placed.Y2 - AlgorithmConfig.Epsilon && placed.Y1 < box.Y2 - AlgorithmConfig.Epsilon;
                    bool overlapZ = box.Z1 < placed.Z2 - AlgorithmConfig.Epsilon && placed.Z1 < box.Z2 - AlgorithmConfig.Epsilon;
                    if (overlapY && overlapZ)
                        newX = Math.Max(newX, placed.X2);
                }
                
                if (container != null && newX + box.Rotation.W > container.Width + AlgorithmConfig.Epsilon)
                    newX = box.X1;
                result.Add(new PlacedBox(box.Instance, new Position3D(newX, box.Y1, box.Z1), box.Rotation)
                    { BinIndex = box.BinIndex });
            }
            return result;
        }

private static List<PlacedBox> CompactZ(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.Z1))
            {
                double newZ = 0.0;
                foreach (var placed in result)
                {
                    bool overlapX = box.X1 < placed.X2 - AlgorithmConfig.Epsilon && placed.X1 < box.X2 - AlgorithmConfig.Epsilon;
                    bool overlapY = box.Y1 < placed.Y2 - AlgorithmConfig.Epsilon && placed.Y1 < box.Y2 - AlgorithmConfig.Epsilon;
                    if (overlapX && overlapY)
                        newZ = Math.Max(newZ, placed.Z2);
                }
                
                if (container != null && newZ + box.Rotation.D > container.Depth + AlgorithmConfig.Epsilon)
                    newZ = box.Z1;
                result.Add(new PlacedBox(box.Instance, new Position3D(box.X1, box.Y1, newZ), box.Rotation)
                    { BinIndex = box.BinIndex });
            }
            return result;
        }

private static List<PlacedBox> CompactY(List<PlacedBox> boxes, ContainerDimensions? container)
        {
            var result = new List<PlacedBox>(boxes.Count);
            foreach (var box in boxes.OrderBy(b => b.Y1))
            {
                double newY = FindYSupport(box, result);
                
                if (container != null && newY + box.Rotation.H > container.Height + AlgorithmConfig.Epsilon)
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
                bool nonFragileOnFragile = !box.Instance.BoxDefinition.IsFragile &&
                                           other.Instance.BoxDefinition.IsFragile;
                if (!nonFragileOnFragile)
                {
                    bool overlapX = box.X1 < other.X2 - AlgorithmConfig.Epsilon && other.X1 < box.X2 - AlgorithmConfig.Epsilon;
                    bool overlapZ = box.Z1 < other.Z2 - AlgorithmConfig.Epsilon && other.Z1 < box.Z2 - AlgorithmConfig.Epsilon;

                    if (overlapX && overlapZ)
                        maxSupportY = Math.Max(maxSupportY, other.Y2);
                }
            }

            return maxSupportY;
        }
    }
}

