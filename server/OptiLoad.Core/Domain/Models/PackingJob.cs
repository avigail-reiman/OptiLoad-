using System;

namespace OptiLoad.Core.Models
{

    public class PackingJob//מודל שמייצג עבודת אריזה
    {
        public int        JobId             { get; set; }//מזהה ייחודי של העבודה
        public int        AdminId           { get; set; }//מזהה המנהל שהפעיל את העבודה
        public int        ContainerId       { get; set; }//מזהה המכולה שבה מתבצעת האריזה
        public JobStatus  Status            { get; set; } = JobStatus.Pending;//סטטוס העבודה, יכול להיות ממתין (Pending), רץ (Running), הושלם (Completed) או נכשל (Failed)
        public int?       BinsUsed          { get; set; }//מספר המכולות שנדשו לעבודה, יכול להיות NULL אם לא הושלמה עדיין
        public double?    VolumeUtilization { get; set; }//אחוז ניצול הנפח
        public double?    TotalWeightKg     { get; set; }//משקל הכולל של כל הקופסאות
        public double?    SolveTimeSeconds  { get; set; }//זמן הפתרון של העבודה בשניות
        public bool?      IsOptimal         { get; set; }//מציין אם אופטימלי או לא
        public string?    StatusMessage     { get; set; }//סטטוס העבודה
        public DateTime   CreatedAt         { get; set; }//תאריך יצירת העבודה
        public DateTime?  CompletedAt       { get; set; }//תאריך סיום העבודה

public Container? Container { get; set; }//הפנייה למכולה שבה מתבצעת האריזה

        public override string ToString() =>//מחרוזת לתיאור העבודה
            $"Job[{JobId}] Status={Status}, Bins={BinsUsed}";
    }

    public enum JobStatus//enum לתיאור מצב העבודה
    {
        Pending,//העבודה ממתינה להתחלה
        Running,//העבודה רצה כעת
        Completed,//העבודה הושלמה בהצלחה
        Failed//העבודה נכשלה
    }
}
