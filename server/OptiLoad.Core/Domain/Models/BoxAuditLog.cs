using System;

namespace OptiLoad.Core.Models
{
    public class BoxAuditLog
    {
        public int      LogId         { get; set; }
        public int      SessionId     { get; set; }
        public string   Action        { get; set; } = string.Empty;  // Added / Deleted
        public int?     BoxId         { get; set; }
        public string   BoxName       { get; set; } = string.Empty;
        public int?     Quantity      { get; set; }
        public string   ChangedBy     { get; set; } = string.Empty;
        public string   ChangedByType { get; set; } = string.Empty;  // Admin / User
        public DateTime ChangedAt     { get; set; }
    }
}
