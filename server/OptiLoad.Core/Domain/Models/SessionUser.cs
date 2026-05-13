using System;

namespace OptiLoad.Core.Models
{
    public class SessionUser
    {
        public int      SessionUserId { get; set; }
        public int      SessionId     { get; set; }
        public string   DisplayName   { get; set; } = string.Empty;
        public string?  Email         { get; set; }
        public string   Token         { get; set; } = string.Empty;
        public DateTime CreatedAt     { get; set; }
    }
}
