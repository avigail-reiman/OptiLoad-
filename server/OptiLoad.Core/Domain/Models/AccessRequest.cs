using System;

namespace OptiLoad.Core.Models
{
    public class AccessRequest
    {
        public int       RequestId     { get; set; }
        public int       SessionId     { get; set; }
        public int       SessionUserId { get; set; }
        public string    Status        { get; set; } = "Pending";  // Pending / Approved / Denied
        public DateTime  RequestedAt   { get; set; }
        public DateTime? RespondedAt   { get; set; }

        // Joined from SessionUser
        public string?   DisplayName   { get; set; }
        public string?   Email         { get; set; }
    }
}
