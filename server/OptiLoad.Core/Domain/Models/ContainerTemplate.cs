using System;

namespace OptiLoad.Core.Models
{

    public class ContainerTemplate//מודל שמייצג תבנית של מכולה, כולל מזהה ייחודי, שם התבנית, מידות המכולה, משקל מקסימלי ותאריך יצירה
    {
        public int      TemplateId   { get; set; }//מזהה ייחודי של תבנית המכולה במסד הנתונים, משמש לזיהוי התבנית ולקשר אותה לטבלאות אחרות במסד הנתונים
        public string   TemplateName { get; set; } = string.Empty;//שם התבנית של המכולה, משמש לזיהוי התבנית בממשק המשתמש ובתיעוד
        public double   Width        { get; set; }//רוחב המכולה במטרים
        public double   Height       { get; set; }//גובה המכולה במטרים
        public double   Depth        { get; set; }//עומק המכולה במטרים
        public double   MaxWeightKg  { get; set; }//משקל מקסימלי שהמכולה יכולה לשאת בקילוגרמים
        public DateTime CreatedAt    { get; set; }//תאריך יצירת תבנית המכולה, משמש לתיעוד ולממשק המשתמש כדי לדעת מתי נוצרה התבנית או מתי עודכנה לאחרונה

        public double Volume => Width * Height * Depth;//נפח המכולה בסמ"ק מחושב על ידי כפל המידות

        public override string ToString() =>//הפונקציה מחזירה את תבנית המכולה כמחרוזת עם השם, המידות והמשקל המקסימלי שלה, לשימוש בממשק המשתמש ובתיעוד
            $"{TemplateName} ({Width}×{Height}×{Depth}, maxWeight={MaxWeightKg}kg)";
    }
}
