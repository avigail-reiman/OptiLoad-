using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public class HeuristicH1
    {
        private readonly ContainerDimensions _container;

        public HeuristicH1(ContainerDimensions container)
        {
            _container = container;
        }

public (int binsUsed, List<(BoxInstance instance, int bin, Position3D pos, Rotation rot)> placements)
            Solve(IEnumerable<BoxInstance> instances)
        {
            var boxList = instances.ToList();
            if (boxList.Count == 0)
                return (0, new());

var results = new[]
            {
                RunWithDimension(boxList, AxisChoice.Depth),
                RunWithDimension(boxList, AxisChoice.Width),
                RunWithDimension(boxList, AxisChoice.Height),
            };

return results
                .OrderBy(r => r.binsUsed)
                .ThenByDescending(r => r.placements.Count)
                .First();
        }

private enum AxisChoice { Depth, Width, Height }

        private (int binsUsed, List<(BoxInstance, int, Position3D, Rotation)> placements)
            RunWithDimension(List<BoxInstance> boxes, AxisChoice depthAxis)
        {
            
            double W, H, D;
            switch (depthAxis)
            {
                case AxisChoice.Depth:  W = _container.Width; H = _container.Height; D = _container.Depth;  break;
                case AxisChoice.Width:  W = _container.Depth; H = _container.Height; D = _container.Width;  break;
                case AxisChoice.Height: W = _container.Width; H = _container.Depth;  D = _container.Height; break;
                default: throw new ArgumentOutOfRangeException();
            }

var withRotations = new List<(BoxInstance inst, double bd, double bw, double bh)>();
            foreach (var inst in boxes)
            {
                var box = inst.BoxDefinition;
                var candidates = box.GetAllowedRotations()
                    .Select(r =>
                    {
                        double bd = depthAxis == AxisChoice.Depth  ? r.D :
                                    depthAxis == AxisChoice.Width  ? r.W : r.H;
                        double bw = depthAxis == AxisChoice.Depth  ? r.W :
                                    depthAxis == AxisChoice.Width  ? r.H : r.W;
                        double bh = depthAxis == AxisChoice.Depth  ? r.H :
                                    depthAxis == AxisChoice.Width  ? r.D : r.D;
                        return (bd, bw, bh);
                    })
                    
                    .Where(r => r.bw <= W + 1e-9 && r.bh <= H + 1e-9 && r.bd <= D + 1e-9)
                    
                    .OrderByDescending(r => r.bd)
                    .ToList();

                if (candidates.Count == 0) continue; 

                var best = candidates.First();
                withRotations.Add((inst, best.bd, best.bw, best.bh));
            }

withRotations = withRotations.OrderByDescending(b => b.bd).ToList();

var slices     = new List<Slice>();
            var remaining  = new Queue<(BoxInstance inst, double bd, double bw, double bh)>(withRotations);

            while (remaining.Count > 0)
            {
                var slice     = new Slice(remaining.Peek().bd, W, H);
                var toProcess = new List<(BoxInstance inst, double bd, double bw, double bh)>();

double sliceDepth = remaining.Peek().bd;
                var    temp       = remaining.ToList();
                remaining.Clear();
                foreach (var item in temp)
                {
                    if (item.bd <= sliceDepth + 1e-9)
                        toProcess.Add(item);
                    else
                        remaining.Enqueue(item);
                }

toProcess = toProcess.OrderByDescending(b => b.bh).ToList();

                foreach (var item in toProcess)
                {
                    if (!slice.TryAddBox(item.inst, item.bw, item.bh, item.bd))
                    {
                        
                        remaining.Enqueue(item);
                    }
                }

if (slice.PlacedItems.Count == 0) break;

                slices.Add(slice);
            }

var bins = new List<double>();  
            var sliceToBin = new Dictionary<int, int>();

            for (int s = 0; s < slices.Count; s++)
            {
                double sliceD = slices[s].Depth;
                bool   placed = false;

                for (int b = 0; b < bins.Count; b++)
                {
                    if (bins[b] + sliceD <= D + 1e-9)
                    {
                        sliceToBin[s] = b;
                        bins[b]       += sliceD;
                        placed        = true;
                        break;
                    }
                }

                if (!placed)
                {
                    sliceToBin[s] = bins.Count;
                    bins.Add(sliceD);
                }
            }

var placements = new List<(BoxInstance, int, Position3D, Rotation)>();
            var binDepthOffset = new double[bins.Count];

            for (int s = 0; s < slices.Count; s++)
            {
                int    binIndex   = sliceToBin[s];
                double zOffset    = binDepthOffset[binIndex];
                double sliceDepth = slices[s].Depth;

                foreach (var (inst, px, py, bW, bH, bD) in slices[s].PlacedItems)
                {
                    
                    double finalX, finalY, finalZ;
                    double rW, rH, rD;

                    switch (depthAxis)
                    {
                        case AxisChoice.Depth:
                            
                            finalX = px; finalY = py; finalZ = zOffset;
                            rW = bW; rH = bH; rD = bD;
                            break;
                        case AxisChoice.Width:
                            
                            finalX = zOffset; finalY = py; finalZ = px;
                            rW = bD; rH = bH; rD = bW;
                            break;
                        default: 
                            
                            finalX = px; finalY = zOffset; finalZ = py;
                            rW = bW; rH = bD; rD = bH;
                            break;
                    }

                    var rotation = new Rotation(rW, rH, rD, 0);
                    placements.Add((inst, binIndex, new Position3D(finalX, finalY, finalZ), rotation));
                }

                binDepthOffset[binIndex] += sliceDepth;
            }

            return (bins.Count, placements);
        }

private class Slice
        {
            public double Depth { get; }
            public double W     { get; }
            public double H     { get; }

            private readonly List<Shelf>                                              _shelves = new();
            public           List<(BoxInstance inst, double x, double y, double bW, double bH, double bD)> PlacedItems = new();

            public Slice(double depth, double w, double h)
            {
                Depth = depth;
                W     = w;
                H     = h;
            }

            public bool TryAddBox(BoxInstance inst, double boxW, double boxH, double boxD)
            {
                
                foreach (var shelf in _shelves)
                {
                    if (shelf.TryAddBox(inst, boxW, boxH, out double px, out double py))
                    {
                        PlacedItems.Add((inst, px, py, boxW, boxH, boxD));
                        return true;
                    }
                }

double usedHeight = _shelves.Sum(s => s.Height);
                if (usedHeight + boxH <= H + 1e-9)
                {
                    var newShelf = new Shelf(usedHeight, boxH, W);
                    if (newShelf.TryAddBox(inst, boxW, boxH, out double px, out double py))
                    {
                        _shelves.Add(newShelf);
                        PlacedItems.Add((inst, px, py, boxW, boxH, boxD));
                        return true;
                    }
                }

                return false;
            }
        }

private class Shelf
        {
            public double BaseY  { get; }
            public double Height { get; }
            public double Width  { get; }
            private double _usedX = 0;

            public Shelf(double baseY, double height, double width)
            {
                BaseY  = baseY;
                Height = height;
                Width  = width;
            }

            public bool TryAddBox(BoxInstance inst, double boxW, double boxH,
                                  out double px, out double py)
            {
                px = 0; py = 0;
                if (boxH > Height + 1e-9)    return false;
                if (_usedX + boxW > Width + 1e-9) return false;

                px    = _usedX;
                py    = BaseY;
                _usedX += boxW;
                return true;
            }
        }
    }
}
