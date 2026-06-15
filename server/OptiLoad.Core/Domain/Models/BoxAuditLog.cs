using System;

namespace OptiLoad.Core.Models
{
    public class BoxAuditLog//מודל שמייצג רשומת יומן שינויים של קופסה בסשן, כולל סוג הפעולה, פרטי הקופסה שהשתנתה ומידע על המשתמש שביצע את השינוי
    {
        public int      LogId         { get; set; }//מזהה ייחודי לרשומת היומן, משמש כמפתח ראשי במסד הנתונים
        public int      SessionId     { get; set; }//מזהה הסשן שבו התבצע השינוי, משמש לקישור בין רשומות היומן לסשן הרלוונטי
        public string   Action        { get; set; } = string.Empty;  // Added / Deleted
        public int?     BoxId         { get; set; }//מזהה הקופסה שהשתנתה, יכול להיות null במקרה של הוספת קופסה חדשה שאין לה עדיין מזהה
        public string   BoxName       { get; set; } = string.Empty;//שם הקופסה שהשתנתה, לשימוש בממשק המשתמש ולתיעוד השינוי
        public double?  Width         { get; set; }//רוחב הקופסה שהשתנתה, יכול להיות null במקרה של הוספת קופסה חדשה
        public double?  Height        { get; set; }//גובה הקופסה שהשתנתה, יכול להיות null במקרה של הוספת קופסה חדשה
        public double?  Depth         { get; set; }//עומק הקופסה   שהשתנה, יכול להיות null במקרה של הוספת קופסה חדשה
        public double?  WeightKg      { get; set; }//משקל הקופסה שהשתנה, יכול להיות null במקרה של הוספת קופסה חדשה
        public bool?    IsFragile     { get; set; }//האם הקופסה שהשתנתה מכילה פריטים שבירים, יכול להיות null במקרה של הוספת קופסה חדשה
        public bool?    AllowRotation { get; set; }//האם מותר לסובב את הקופסה שהשתנתה, יכול להיות null במקרה של הוספת קופסה חדשה
        public int?     Quantity      { get; set; }//כמות הקופסאות שהשתנתה, יכול להיות null במקרה של הוספת קופסה חדשה או מחיקת קופסה קיימת
        public string   ChangedBy     { get; set; } = string.Empty;//שם המשתמש שביצע את השינוי, לשימוש בממשק המשתמש ולתיעוד השינוי
        public string   ChangedByType { get; set; } = string.Empty;  // Admin / User
        public DateTime ChangedAt     { get; set; }//תאריך ושעה שבהם התבצע השינוי, לשימוש בממשק המשתמש ולתיעוד השינוי
    }
}
