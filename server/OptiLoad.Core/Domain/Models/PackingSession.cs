using System;

namespace OptiLoad.Core.Models
{
    public class PackingSession//מייצג סשן של שיבוץ
    {
        public int      SessionId   { get; set; }//מזהה הסשן
        public int      AdminId     { get; set; }//מזהה המנהל שיצר את הסשן
        public string   Name        { get; set; } = string.Empty;//שם הסשן
        public string?  Description { get; set; }//תיאור הסשן, יכול להיות null
        public string   LinkToken   { get; set; } = string.Empty;//טוקן ייחודי לשיתוף הסשן עם אחרים, משמש ליצירת קישור ייחודי לסשן שניתן לשתף עם משתמשים אחרים כדי לאפשר להם להצטרף לסשן ולראות את התקדמות השיבוץ
        public string   Status      { get; set; } = "Open";   // Open / Closed
        public DateTime CreatedAt   { get; set; }//תאריך יצירת הסשן
    }
}
