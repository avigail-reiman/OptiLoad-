using System;

namespace OptiLoad.Core.Models
{
    public class PackingSession
    {
        public int      SessionId   { get; set; }
        public int      AdminId     { get; set; }
        public string   Name        { get; set; } = string.Empty;
        public string?  Description { get; set; }
        public string   LinkToken   { get; set; } = string.Empty;
        public string   Status      { get; set; } = "Open";   // Open / Closed
        public DateTime CreatedAt   { get; set; }
    }
}
