using System;

namespace OptiLoad.Core.Models
{

    public class ErrorLog
    {
        public int      ErrorId    { get; set; }
        public int?     JobId      { get; set; }
        public string   Context    { get; set; } = string.Empty;
        public string   Message    { get; set; } = string.Empty;
        public string?  StackTrace { get; set; }
        public DateTime CreatedAt  { get; set; }
    }
}
