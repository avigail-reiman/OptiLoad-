using System;

namespace OptiLoad.Core.Models
{
    /// <summary>
    /// יומן שגיאות (טבלה: ErrorLog)
    /// </summary>
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
