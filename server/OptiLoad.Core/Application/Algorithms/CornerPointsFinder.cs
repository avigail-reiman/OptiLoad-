using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public static class CornerPointsFinder
    {

private static List<(double x, double y)> Find2DCorners(
            List<(double x1, double x2, double y1, double y2)> projectedBoxes,
            double W, double H,
            List<(double minW, double minH)>? remainingBoxes = null)
        {
            if (projectedBoxes.Count == 0)
                return new List<(double, double)> { (0.0, 0.0) };

var candidateX = new SortedSet<double> { 0.0 };
            
            var candidateY = new SortedSet<double> { 0.0 };

            foreach (var b in projectedBoxes)
            {
                candidateX.Add(b.x2);
                candidateY.Add(b.y2);
            }

var corners = new List<(double x, double y)>();
            foreach (double cx in candidateX)
                foreach (double cy in candidateY)
                    corners.Add((cx, cy));

if (remainingBoxes != null && remainingBoxes.Count > 0)
            {
                double minRemainingW = remainingBoxes.Min(r => r.minW);
                double minRemainingH = remainingBoxes.Min(r => r.minH);

                corners = corners
                    .Where(c => c.x + minRemainingW <= W && c.y + minRemainingH <= H)
                    .ToList();
            }

            return corners;
        }

public static List<Position3D> Find3DCorners(
            IReadOnlyList<PlacedBox>  placedBoxes,
            ContainerDimensions       container,
            IEnumerable<BoxInstance>? remainingInstances = null)
        {
            if (placedBoxes.Count == 0)
                return new List<Position3D> { new Position3D(0, 0, 0) };

var remaining = remainingInstances?
                .Select(b =>
                {
                    var box = b.BoxDefinition;
                    double minW, minH, minD;
                    if (box.AllowRotation)
                    {
                        var dims = new[] { box.Width, box.Height, box.Depth };
                        minW = dims.Min();
                        minH = dims.Min();
                        minD = dims.Min();
                    }
                    else
                    {
                        minW = box.Width;
                        minH = box.Height;
                        minD = box.Depth;
                    }
                    return (minW, minH, minD);
                })
                .ToList() ?? new List<(double, double, double)>();

var zCoords = new SortedSet<double> { 0.0 };
            foreach (var pb in placedBoxes)
                zCoords.Add(pb.Z2);

            var result          = new List<Position3D>();
            List<(double x, double y)>? prevCorners2D = null;

            foreach (double z0 in zCoords)
            {
                
                if (remaining.Count > 0)
                {
                    double minD = remaining.Min(r => r.minD);
                    if (z0 + minD > container.Depth) break;
                }
                else if (z0 >= container.Depth) break;

var activeBoxes = placedBoxes
                    .Where(pb => pb.Z2 > z0)
                    .Select(pb => (x1: pb.X1, x2: pb.X2, y1: pb.Y1, y2: pb.Y2))
                    .ToList();

                var remainingForFilter = remaining
                    .Select(r => (r.minW, r.minH))
                    .ToList();

                var corners2D = Find2DCorners(
                    activeBoxes,
                    container.Width,
                    container.Height,
                    remainingForFilter.Count > 0 ? remainingForFilter : null);

foreach (var (cx, cy) in corners2D)
                    result.Add(new Position3D(cx, cy, z0));

                prevCorners2D = corners2D;
            }

            return result;
        }
    }
}
