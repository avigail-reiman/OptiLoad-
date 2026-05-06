using System;
using System.Collections.Generic;
using System.Linq;

namespace OptiLoad.Core.Models
{

public class ContainerDimensions
    {
        public double Width       { get; set; }
        public double Height      { get; set; }
        public double Depth       { get; set; }
        public double MaxWeightKg { get; set; }

        public double Volume => Width * Height * Depth;

        public override string ToString() =>
            $"Container({Width}×{Height}×{Depth}, maxWeight={MaxWeightKg}kg)";
    }

    public class BoxInstance
    {
        public Box BoxDefinition  { get; set; } = null!;
        public int InstanceIndex  { get; set; }
        public string InstanceId  => $"{BoxDefinition.BoxName}#{InstanceIndex}";

        public override string ToString() => InstanceId;
    }

    public readonly record struct Rotation(double W, double H, double D, int Index)
    {
        public double Volume => W * H * D;

        public override string ToString() => $"R{Index}({W}×{H}×{D})";
    }

    public readonly record struct Position3D(double X, double Y, double Z)
    {
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    public class PlacedBox
    {
        public BoxInstance Instance { get; }
        public Position3D  Position { get; }
        public Rotation    Rotation { get; }
        public int         BinIndex { get; set; }

        public double X1 => Position.X;
        public double Y1 => Position.Y;
        public double Z1 => Position.Z;
        public double X2 => Position.X + Rotation.W;
        public double Y2 => Position.Y + Rotation.H;
        public double Z2 => Position.Z + Rotation.D;

        public double Volume => Rotation.Volume;

        public PlacedBox(BoxInstance instance, Position3D position, Rotation rotation)
        {
            Instance = instance;
            Position = position;
            Rotation = rotation;
        }

        public bool OverlapsWith(PlacedBox other)
        {
            if (X1 >= other.X2 || other.X1 >= X2) return false;
            if (Y1 >= other.Y2 || other.Y1 >= Y2) return false;
            if (Z1 >= other.Z2 || other.Z1 >= Z2) return false;
            return true;
        }

        public override string ToString() =>
            $"{Instance.InstanceId} @ {Position} rot={Rotation}";
    }

    public class PackingState
    {
        private readonly List<PlacedBox> _placed = new();

        public IReadOnlyList<PlacedBox> PlacedBoxes => _placed;
        public double UsedWeightKg { get; private set; }
        public double UsedVolume   => _placed.Sum(pb => pb.Volume);
        public int    Count        => _placed.Count;

        public void AddBox(PlacedBox box)
        {
            _placed.Add(box);
            UsedWeightKg += box.Instance.BoxDefinition.WeightKg;
        }

        public void RemoveLastBox()
        {
            if (_placed.Count == 0) return;
            UsedWeightKg -= _placed[^1].Instance.BoxDefinition.WeightKg;
            _placed.RemoveAt(_placed.Count - 1);
        }

        public PackingState Clone()
        {
            var clone = new PackingState { UsedWeightKg = UsedWeightKg };
            clone._placed.AddRange(_placed);
            return clone;
        }
    }

    public class BinStats
    {
        public int    BinIndex           { get; set; }
        public double UsedVolume         { get; set; }
        public double TotalVolume        { get; set; }
        public double UtilizationPercent => TotalVolume > 0 ? UsedVolume / TotalVolume * 100.0 : 0;
        public double WastedPercent      => 100.0 - UtilizationPercent;
    }

    public class PackingResult
    {
        public List<PlacedBox>   PlacedBoxes       { get; set; } = new();
        public List<BoxInstance> UnplacedBoxes     { get; set; } = new();
        public int               BinsUsed          { get; set; }
        public double            VolumeUtilization { get; set; }
        public TimeSpan          SolveTime         { get; set; }
        public string            StatusMessage     { get; set; } = string.Empty;
        public bool              IsOptimal         { get; set; }
        public List<BinStats>    PerBinStats       { get; set; } = new();

        public override string ToString() =>
            $"PackingResult: {BinsUsed} bins, {VolumeUtilization:P1} utilization, " +
            $"{PlacedBoxes.Count} placed, {UnplacedBoxes.Count} unplaced";
    }
}
