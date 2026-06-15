using System;
using System.Collections.Generic;
using System.Linq;

namespace OptiLoad.Core.Models
{

public class ContainerDimensions//מודל שמייצג את מידות המכולה שבה נארזות הקופסאות, כולל רוחב, גובה, עומק ומשקל מקסימלי
    {
        public double Width       { get; set; }//רוחב המכולה במטרים
        public double Height      { get; set; }//גובה המכולה במטרים
        public double Depth       { get; set; }//עומק המכולה במטרים
        public double MaxWeightKg { get; set; }//משקל מקסימלי שהמכולה יכולה לשאת בקילוגרמים

        public double Volume => Width * Height * Depth;//נפח המכולה בסמ"ק מחושב על ידי כפל המידות

        public override string ToString() =>//הפונקציה מחזירה את המכולה כמחרוזת עם כל הנתונים
            $"Container({Width}×{Height}×{Depth}, maxWeight={MaxWeightKg}kg)";
    }

    public class BoxInstance//מודל שמייצג מופע של קופסה מסוימת עם מזהה ייחודי בתוך הסשן, כולל הפניה להגדרת הקופסה והאינדקס של המופע
    {
        public Box BoxDefinition  { get; set; } = null!;//הפניה להגדרת הקופסה שמכילה את המידות והמשקל של הקופסה
        public int InstanceIndex  { get; set; }//האינדקס של המופע בתוך הסשן, משמש להבחין בין מופעים שונים של אותה הגדרת קופסה
        public string InstanceId  => $"{BoxDefinition.BoxName}#{InstanceIndex}";//מזהה ייחודי למופע הקופסה בתוך הסשן, מורכב משם הקופסה ואינדקס המופע כדי להבטיח ייחודיות גם כאשר יש כמה מופעים של אותה הגדרת קופסה

        public override string ToString() => InstanceId;//הפונקציה מחזירה את מזהה המופע של הקופסה כמחרוזת, לשימוש בממשק המשתמש ובתיעוד
    }

    public readonly record struct Rotation(double W, double H, double D, int Index)//מודל שמייצג סיבוב של קופסה עם מידות רוחב, גובה ועומק בסדר מסוים, כולל אינדקס ייחודי לסיבוב כדי להבחין בין סיבובים שונים של אותה הקופסה
    {
        public override string ToString() => $"R{Index}({W}×{H}×{D})";//הפונקציה מחזירה את הסיבוב כמחרוזת עם האינדקס והמידות, לשימוש בממשק המשתמש ובתיעוד
    }

    public readonly record struct Position3D(double X, double Y, double Z)//מודל שמייצג מיקום תלת-ממדי של קופסה בתוך המכולה, כולל קואורדינטות X, Y ו-Z שמציינות את הפינה התחתונה הקדמית השמאלית של הקופסה
    {
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";//הפונקציה מחזירה את המיקום כמחרוזת עם הקואורדינטות מעוגלות לשתי ספרות אחרי הנקודה, לשימוש בממשק המשתמש ובתיעוד
    }

    public class PlacedBox//מייצג קופסה שכבר שובצה במכולה
    {
        public BoxInstance Instance { get; }//אומר איזה קופסה זה, ומה המספר הסידורי שלהל 
        public Position3D  Position { get; }//המיקום של פינת "ההתחלה" בתוך המכולה
        public Rotation    Rotation { get; }//איזה סיבוב של הקופסה נבחר
        public int         BinIndex { get; set; }//באיזו מכולה הונח

        public double X1 => Position.X;//קואורדינטת X של הפינה התחתונה הקדמית השמאלית של הקופסה
        public double Y1 => Position.Y;//קואורדינטת Y של הפינה התחתונה הקדמית השמאלית של הקופסה
        public double Z1 => Position.Z;//קואורדינטת Z של הפינה התחתונה הקדמית השמאלית של הקופסה
        public double X2 => Position.X + Rotation.W;//קואורדינטת X של הפינה העליונה האחורית הימנית של הקופסה
        public double Y2 => Position.Y + Rotation.H;//קואורדינטת Y של הפינה העליונה האחורית הימנית של הקופסה
        public double Z2 => Position.Z + Rotation.D;//קואורדינטת Z של הפינה העליונה האחורית הימנית של הקופסה

        public double Volume => Instance.BoxDefinition.Volume;//נפח הקופסה מחושב על ידי הפניה להגדרת הקופסה שמכילה את המידות שלה

        public PlacedBox(BoxInstance instance, Position3D position, Rotation rotation)//הקונסטרקטור יוצר מופע של PlacedBox עם הקופסה, המיקום והסיבוב שנבחרו
        {
            Instance = instance;//שומר את הפניה למופע הקופסה כדי לדעת איזה קופסה זה ומה המידות שלה
            Position = position;//שומר את המיקום של הקופסה בתוך המכולה כדי לדעת איפה היא נמצאת
            Rotation = rotation;//שומר את הסיבוב של הקופסה כדי לדעת באיזה כיוון היא שובצה
        }

        public bool OverlapsWith(PlacedBox other)//בודק אם הקופסה הזו חופפת עם קופסה אחרת על ידי השוואת הקואורדינטות של הפינות שלהן. אם יש הפרדה בין הקופסאות בכל אחד מהצירים, הן לא חופפות.
        {
            if (X1 >= other.X2 || other.X1 >= X2) return false;
            if (Y1 >= other.Y2 || other.Y1 >= Y2) return false;
            if (Z1 >= other.Z2 || other.Z1 >= Z2) return false;
            return true;
        }

        public override string ToString() =>
            $"{Instance.InstanceId} @ {Position} rot={Rotation}";
    }

    public class PackingState//מודל שמייצג את מצב האריזה הנוכחי של מכולה אחת, כולל רשימת הקופסאות שכבר שובצו, המשקל הכולל שהשתמשנו בו, הנפח הכולל שהשתמשנו בו ומספר הקופסאות שכבר שובצו
    {
        private readonly List<PlacedBox> _placed = new();//רשימה פנימית של הקופסאות שכבר שובצו במכולה

        public IReadOnlyList<PlacedBox> PlacedBoxes => _placed;//חשיפה של רשימת הקופסאות שכבר שובצו לקריאה בלבד - למנוע שינוי חיצוני
        public double UsedWeightKg { get; private set; }//המשקל הכולל של הקופסאות שכבר שובצו
        public double UsedVolume   => _placed.Sum(pb => pb.Volume);//הנפח הכולל של הקופסאות שכבר שובצו
        public int    Count        => _placed.Count;//מספר הקופסאות שכבר שובצו

        public void AddBox(PlacedBox box)//הפונקציה מוסיפה קופסה חדשה למצב האריזה ומעדכנת את המשקל הכולל בהתאם למשקל של הקופסה שהתווספה
        {
            _placed.Add(box);//מוסיפה את הקופסה לרשימת הקופסאות שכבר שובצו
            UsedWeightKg += box.Instance.BoxDefinition.WeightKg;//מעדכנת את המשקל הכולל על ידי הוספת המשקל של הקופסה שהתווספה, שמתקבל על ידי הפניה להגדרת הקופסה שמכילה את המידות והמשקל שלה
        }

        public void RemoveLastBox()//הפונקציה מסירה את הקופסה האחרונה שהתווספה למצב האריזה ומעדכנת את המשקל הכולל בהתאם למשקל של הקופסה שהוסרה
        {
            if (_placed.Count == 0) return;//אם אין קופסאות במצב האריזה, אין מה להסיר ולכן הפונקציה פשוט מחזירה
            UsedWeightKg -= _placed[^1].Instance.BoxDefinition.WeightKg;//מעדכנת את המשקל הכולל על ידי הפחתת המשקל של הקופסה האחרונה שהתווספה, שמתקבל על ידי הפניה להגדרת הקופסה שמכילה את המידות והמשקל שלה
            _placed.RemoveAt(_placed.Count - 1);//מסירה את הקופסה האחרונה מהרשימה
        }

        public PackingState Clone()//הפונקציה יוצרת עותק חדש של מצב האריזה הנוכחי, כולל רשימת הקופסאות שכבר שובצו והמשקל הכולל שהשתמשנו בו, כדי לאפשר שמירה של מצב מסוים לפני ביצוע שינויים נוספים
        {
            var clone = new PackingState { UsedWeightKg = UsedWeightKg };//יוצר מופע חדש של PackingState ומעתיק את המשקל הכולל שהשתמשנו בו
            clone._placed.AddRange(_placed);//מעביר את כל הקופסאות שכבר שובצו לרשימה של המופע החדש כדי לשמור על אותו מצב אריזה
            return clone;//מחזיר את העותק החדש של מצב האריזה
        }
    }

    public class BinStats//מודל שמייצג סטטיסטיקות של מכולה אחת, כולל האינדקס שלה, הנפח שהשתמשנו בו, הנפח הכולל שלה, אחוז ניצול הנפח ואחוז הפסד הנפח
    {
        public int    BinIndex           { get; set; }//האינדקס של המכולה בתוך התוצאה הכוללת, משמש להבחין בין מכולות שונות בתוצאה
        public double UsedVolume         { get; set; }//הנפח הכולל של הקופסאות שכבר שובצו בתוך המכולה הזו
        public double TotalVolume        { get; set; }//הנפח הכולל של המכולה
        public double UtilizationPercent => TotalVolume > 0 ? UsedVolume / TotalVolume * 100.0 : 0;//אחוז ניצול הנפח של המכולה
        public double WastedPercent      => 100.0 - UtilizationPercent;//אחוז הפסד הנפח של המכולה
    }

    public class PackingResult//מודל שמייצג את התוצאה של תהליך האריזה הכולל, כולל רשימת הקופסאות שכבר שובצו, רשימת הקופסאות שלא שובצו, מספר המכולות שהשתמשנו בהן, אחוז ניצול הנפח הכולל, זמן הפתרון, הודעת סטטוס והאם הפתרון הוא אופטימלי
    {
        public List<PlacedBox>   PlacedBoxes       { get; set; } = new();//רשימה של הקופסאות שכבר שובצו במכולות
        public List<BoxInstance> UnplacedBoxes     { get; set; } = new();//רשימה של הקופסאות שלא הצלחנו לשבץ במכולות
        public int               BinsUsed          { get; set; }//מספר המכולות שהשתמשנו בהן כדי לשבץ את הקופסאות
        public double            VolumeUtilization { get; set; }//אחוז ניצול הנפח הכולל של כל המכולות שהשתמשנו בהן, מחושב על ידי חלוקת הנפח הכולל של הקופסאות שכבר שובצו בנפח הכולל של המכולות שהשתמשנו בהן
        public TimeSpan          SolveTime         { get; set; }//זמן שלקח למחשב למצוא את הפתרון של האריזה
        public string            StatusMessage     { get; set; } = string.Empty;//הודעת סטטוס שמתארת את מצב הפתרון
        public bool              IsOptimal         { get; set; }//מציין אם הפתרון הוא אופטימלי
        public List<BinStats>    PerBinStats       { get; set; } = new();//רשימה של סטטיסטיקות לכל מכולה

        public override string ToString() =>//הפונקציה מחזירה את התוצאה של האריזה כמחרוזת עם מספר המכולות שהשתמשנו בהן, אחוז ניצול הנפח הכולל, מספר הקופסאות שכבר שובצו ומספר הקופסאות שלא שובצו
            $"PackingResult: {BinsUsed} bins, {VolumeUtilization:P1} utilization, " +
            $"{PlacedBoxes.Count} placed, {UnplacedBoxes.Count} unplaced";
    }
}
