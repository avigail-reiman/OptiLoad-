using System;

namespace OptiLoad.Core.Models
{
    public class SessionBox//מייצג קופסה שנוספה לעבודה מסוימת
    {
        public int      SessionBoxId { get; set; }//מזהה ייחודי של הקופסה בתוך הסשן
        public int      SessionId    { get; set; }//מזהה הסשן שאליו שייכת הקופסה
        public int      BoxId        { get; set; }//מזהה הקופסה
        public int      Quantity     { get; set; }//כמות הקופסאות מסוג זה שיש לשבץ
        public string   AddedBy      { get; set; } = string.Empty;//שם המשתמש שהוסיף את הקופסה לסשן
        public DateTime AddedAt      { get; set; }//תאריך שבו נוספה הקופסה לסשן

        public string?  BoxName      { get; set; }//שם הקופסה
        public double   Width        { get; set; }//רוחב הקופסה במטרים
        public double   Height       { get; set; }//גובה הקופסה במטרים
        public double   Depth        { get; set; }//עומק הקופסה במטרים
        public double   WeightKg     { get; set; }//משקל הקופסה בקילוגרמים
        public bool     IsFragile    { get; set; }//האם הקופסה שבירה
        public bool     AllowRotation { get; set; } = true;//האם מותר לסובב את הקופסה
    }
}
