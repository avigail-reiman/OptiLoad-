using System;

namespace OptiLoad.Core.Models
{

    public class PlacementResult//מייצג מידות ומיקום של קופסה מסוימת בעבודה מסוימת
    {
        public int      PlacementId   { get; set; }//מזהה ייחודי של המיקום
        public int      JobId         { get; set; }//מזהה העבודה
        public int      BoxId         { get; set; }//מזהה הקופסה
        public int      InstanceIndex { get; set; } = 1;//אינדקס המופע של הקופסה בעבודה
        public int      BinIndex      { get; set; } = 0;//אינדקס המיכל שבו הקופסה מונחת
        public double   PosX          { get; set; }//מיקום הקופסה בציר X
        public double   PosY          { get; set; }//מיקום הקופסה בציר Y
        public double   PosZ          { get; set; }//מיקום הקופסה בציר Z
        public double   PlacedWidth   { get; set; }//רוחב הקופסה לאחר מיקום
        public double   PlacedHeight  { get; set; }//גובה הקופסה לאחר מיקום
        public double   PlacedDepth   { get; set; }//עומק הקופסה לאחר מיקום
        public int      RotationIndex { get; set; }//אינדקס הסיבוב של הקופסה
        public DateTime CreatedAt     { get; set; }//תאריך יצירת המיקום

public string BoxName  { get; set; } = string.Empty;//שם הקופסה
        public bool   IsFragile { get; set; }//האם הקופסה שבירה

public PackingJob? Job { get; set; }//הפנייה לעבודה שאליה הקופסה שייכת
        public Box? Box { get; set; }//הפנייה לקופסה עצמה, שמכילה את המידות והמשקל שלה

        public override string ToString() =>//סטרינג לתיאור המיקום של הקופסה בעבודה המסוימת
            $"Placement: Box[{BoxId}]#{InstanceIndex} @ ({PosX},{PosY},{PosZ}) Bin={BinIndex}";
    }
}
