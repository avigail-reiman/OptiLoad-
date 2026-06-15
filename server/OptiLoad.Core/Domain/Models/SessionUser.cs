using System;

namespace OptiLoad.Core.Models
{
    public class SessionUser//מייצג משתמש בעבודת שיבוץ מסוימת
    {
        public int      SessionUserId { get; set; }//מזהה ייחודי של המשתמש
        public int      SessionId     { get; set; }//מזהה הסשן שאליו שייך המשתמש
        public string   DisplayName   { get; set; } = string.Empty;//שם התצוגה של המשתמש
        public string?  Email         { get; set; }//אימייל של המשתמש
        public string   Token         { get; set; } = string.Empty;//טוקן ייחודי של המשתמש
        public string   RootToken     { get; set; } = string.Empty;//טוקן שורש של המשתמש
        public DateTime CreatedAt     { get; set; }//תאריך יצירת המשתמש
    }
}
