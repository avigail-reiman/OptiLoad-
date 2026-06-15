namespace OptiLoad.Core.Models
{

    public class PackingJobBox//מייצג קופסה מסוימת בתוך עבודת שיבוץ
    {
        public int  JobBoxId { get; set; }//מזהה ייחודי של הקופסה בתוך העבודה
        public int  JobId    { get; set; }//מזהה העבודה
        public int  BoxId    { get; set; }//מזהה הקופסה
        public int  Quantity { get; set; } = 1;//כמות הקופסאות מסוג זה שיש לשבץ

public PackingJob? Job { get; set; }//הפנייה לעבודה אליה הקופסה צריכה להשתייך
        public Box? Box { get; set; }//הפנייה לקופסה עצמה, שמכילה את המידות והמשקל שלה
    }
}
