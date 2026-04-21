using System;

namespace OptiLoad.Core.Models
{
    /// <summary>
    /// תוצאת שיבוץ – מיקום מדויק לכל ארגז (טבלה: PlacementResult)
    /// </summary>
    public class PlacementResult
    {
        public int      PlacementId   { get; set; }
        public int      JobId         { get; set; }
        public int      BoxId         { get; set; }
        public int      InstanceIndex { get; set; } = 1;
        public int      BinIndex      { get; set; } = 0;
        public double   PosX          { get; set; }
        public double   PosY          { get; set; }
        public double   PosZ          { get; set; }
        public double   PlacedWidth   { get; set; }
        public double   PlacedHeight  { get; set; }
        public double   PlacedDepth   { get; set; }
        public int      RotationIndex { get; set; }
        public DateTime CreatedAt     { get; set; }

        // שדות מועשרים (מ-JOIN עם Box)
        public string BoxName  { get; set; } = string.Empty;
        public bool   IsFragile { get; set; }

        // ניווט
        public PackingJob? Job { get; set; }
        public Box?        Box { get; set; }

        public override string ToString() =>
            $"Placement: Box[{BoxId}]#{InstanceIndex} @ ({PosX},{PosY},{PosZ}) Bin={BinIndex}";
    }
}
