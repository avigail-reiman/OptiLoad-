using System;
using System.Collections.Generic;

namespace OptiLoad.Core.Models
{

    public class Box//מודל שמייצג קופסה עם מידות, משקל ופרמטרים נוספים
    {
        public int      BoxId         { get; set; }//מזהה ייחודי לקופסה, משמש כמפתח ראשי במסד הנתונים
        public string   BoxName       { get; set; } = string.Empty;//שם הקופסה, לשימוש בממשק המשתמש
        public double   Width         { get; set; }//רוחב הקופסה במטרים
        public double   Height        { get; set; }//גובה הקופסה במטרים
        public double   Depth         { get; set; }//עומק הקופסה במטרים
        public double   WeightKg      { get; set; }//משקל הקופסה בקילוגרמים
        public bool     IsFragile     { get; set; }//האם הקופסה מכילה פריטים שבירים, משפיע על מיקום הקופסה במכולה
        public bool     AllowRotation { get; set; } = true;//האם מותר לסובב את הקופסה במהלך האריזה
        public DateTime CreatedAt     { get; set; }//תאריך ושעה שבהם נוצרה הקופסה

        public double Volume => Width * Height * Depth;//נפח הקופסה בסמ"ק מחושב על ידי כפל המידות

        public IEnumerable<Rotation> GetAllowedRotations()//מחזיר את כל הסיבובים האפשריים של הקופסה בהתאם לפרמטר AllowRotation
        {
            var (w, h, d) = (Width, Height, Depth);//קיצור שמות המידות

            if (!AllowRotation)//אם אסור לסובב את הקופסה, מחזיר רק את הסיבוב המקורי
            {
                yield return new Rotation(w, h, d, 0);//סיבוב עם המידות המקוריות ומזהה סיבוב 0
                yield break;//סיום הפונקציה כי אין סיבובים נוספים
            }

            var rotations = new[]//כל הסיבובים האפשריים של הקופסה, כולל המידות בסדר שונה ומזהה סיבוב ייחודי לכל אחד
            {
                new Rotation(w, h, d, 0),
                new Rotation(w, d, h, 1),
                new Rotation(h, w, d, 2),
                new Rotation(h, d, w, 3),
                new Rotation(d, w, h, 4),
                new Rotation(d, h, w, 5),
            };

            var seen = new HashSet<(double, double, double)>();//סט לשמירת המידות שכבר נראו כדי למנוע החזרת סיבובים עם מידות זהות (למשל אם הקופסה היא קובייה)
            foreach (var r in rotations)
            {
                if (seen.Add((r.W, r.H, r.D)))//אם עוד אין סיבוב עם המידות האלה, מכניס אותו לסט
                    yield return r;//מחזיר את הסיבוב הנוכחי כי הוא ייחודי במידותיו
            }
        }
        //הפונקציה מחזירה את הקופסה כמחרוזת עם כל הנתונים
        public override string ToString() =>
            $"Box[{BoxId}:{BoxName}]({Width}×{Height}×{Depth}, {WeightKg}kg, fragile={IsFragile})";
    }
}
