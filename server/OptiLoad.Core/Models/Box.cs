using System;
using System.Collections.Generic;

namespace OptiLoad.Core.Models
{
    /// <summary>
    /// ארגז – מידות, משקל ואפשרויות סיבוב (טבלה: Box)
    /// </summary>
    public class Box
    {
        public int      BoxId         { get; set; }
        public string   BoxName       { get; set; } = string.Empty;
        public double   Width         { get; set; }
        public double   Height        { get; set; }
        public double   Depth         { get; set; }
        public double   WeightKg      { get; set; }
        public bool     IsFragile     { get; set; }
        public bool     AllowRotation { get; set; } = true;
        public DateTime CreatedAt     { get; set; }

        public double Volume => Width * Height * Depth;

        /// <summary>
        /// מחזיר את כל כיוני הסיבוב האפשריים.
        /// אם AllowRotation=false – רק הכיוון המקורי.
        /// </summary>
        public IEnumerable<Rotation> GetAllowedRotations()
        {
            var (w, h, d) = (Width, Height, Depth);

            if (!AllowRotation)
            {
                yield return new Rotation(w, h, d, 0);
                yield break;
            }

            var rotations = new[]
            {
                new Rotation(w, h, d, 0),
                new Rotation(w, d, h, 1),
                new Rotation(h, w, d, 2),
                new Rotation(h, d, w, 3),
                new Rotation(d, w, h, 4),
                new Rotation(d, h, w, 5),
            };

            var seen = new HashSet<(double, double, double)>();
            foreach (var r in rotations)
            {
                if (seen.Add((r.W, r.H, r.D)))
                    yield return r;
            }
        }

        public override string ToString() =>
            $"Box[{BoxId}:{BoxName}]({Width}×{Height}×{Depth}, {WeightKg}kg, fragile={IsFragile})";
    }
}
