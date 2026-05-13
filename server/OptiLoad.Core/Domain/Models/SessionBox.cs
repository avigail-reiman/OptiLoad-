using System;

namespace OptiLoad.Core.Models
{
    public class SessionBox
    {
        public int      SessionBoxId { get; set; }
        public int      SessionId    { get; set; }
        public int      BoxId        { get; set; }
        public int      Quantity     { get; set; }
        public string   AddedBy      { get; set; } = string.Empty;
        public DateTime AddedAt      { get; set; }

        // Joined from Box
        public string?  BoxName      { get; set; }
        public double   Width        { get; set; }
        public double   Height       { get; set; }
        public double   Depth        { get; set; }
        public double   WeightKg     { get; set; }
        public bool     IsFragile    { get; set; }
        public bool     AllowRotation { get; set; } = true;
    }
}
