using System;

namespace OptiLoad.Core.Models
{
    public class AccessRequest//מודל שמייצג בקשת גישה של משתמש לסשן מסוים, כולל סטטוס הבקשה ותזמון הבקשה והתשובה
    {
        public int       RequestId     { get; set; }//מזהה ייחודי לבקשת הגישה
        public int       SessionId     { get; set; }//מזהה הסשן שבו מתבצעת הבקשה
        public int       SessionUserId { get; set; }//מזהה המשתמש שביצע את הבקשה
        public string    Status        { get; set; } = "Pending"; //סטטוס הבקשה - יכול להיות "Pending" (ממתינה), "Approved" (מאושרת) או "Denied" (נדחתה)
        public DateTime  RequestedAt   { get; set; }//תאריך ושעה שבהם נשלחה הבקשה
        public DateTime? RespondedAt   { get; set; }//תאריך ושעה שבהם ניתנה התשובה לבקשה

        public string?   DisplayName   { get; set; }//שם התצוגה של המשתמש שביצע את הבקשה, לשימוש בממשק המשתמש
        public string?   Email         { get; set; }//כתובת האימייל של המשתמש שביצע את הבקשה, לשימוש בממשק המשתמש
    }
}
