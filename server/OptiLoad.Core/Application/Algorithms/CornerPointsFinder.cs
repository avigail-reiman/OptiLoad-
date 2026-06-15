using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

//פונקציה שמוצאת את כל נקודות הפינה האפשריות במכולה על סמך מיקום וסיבוב של הקופסאות שכבר שובצו, ומחזירה רשימה של מיקומים תלת-ממדיים
    public static class CornerPointsFinder
    {

//מוצאת נקודות פינה בדו מימדי 
//מקבלת רשימה של נקודות קצה של ארגזים שכבר שובצו, מידות המכולה וכן רשימה של ארגזים 
private static List<(double x, double y)> Find2DCorners(
            List<(double x1, double x2, double y1, double y2)> projectedBoxes,
            double W, double H,
            List<(double minW, double minH)>? remainingBoxes = null)
        {//אם אין ארגזים שכבר שובצו, מחזירה את נקודת הפינה היחידה (0,0)
            if (projectedBoxes.Count == 0)
                return new List<(double, double)> { (0.0, 0.0) };

var candidateX = new SortedSet<double> { 0.0 };
            
            var candidateY = new SortedSet<double> { 0.0 };//יוצרת רשימה מסודרת וממוינת של נקודות הפינה מפינת המכולה והלאה

            foreach (var b in projectedBoxes)//מוסיפה את הפינות של כל הארגזים שכבר שובצו.
            {
                candidateX.Add(b.x2);//מוסיפה את הX של סוף הארגז
                candidateY.Add(b.y2);//מוסיפה את הY של סוף הארגז
            }
//יוצרת רשימה אחת של כל הצירופים האפשריים של נקודות הפינה שנמצאו, כלומר כל נקודת פינה אפשרית במכולה
var corners = new List<(double x, double y)>();
            foreach (double cx in candidateX)//עובר על כל נקודות הX שנמצאו
                foreach (double cy in candidateY)//עובר על כל נקודות הY שנמצאו
                    corners.Add((cx, cy));//מכניסה לרשימה את הצירוף של הX והY הנוכחיים.
//אם יש ארגזים שעדיין לא שובצו בודק אם נקודות הפינה מתאימות לאחד מהם
if (remainingBoxes != null && remainingBoxes.Count > 0)
            {
                double minRemainingW = remainingBoxes.Min(r => r.minW);//מחפש את רוחב הארגז הצר ביותר
                double minRemainingH = remainingBoxes.Min(r => r.minH);//מחפש את גובה הארגז הנמוך ביותר
                //מסנן את נקודות הפינה כך שרק אלו שיכולות להכיל את הארגז הצר והנמוך ביותר יישארו, כלומר רק נקודות פינה שמאפשרות להכניס ארגז לפחות בגודל המינימלי שנותר
                corners = corners
                    .Where(c => c.x + minRemainingW <= W && c.y + minRemainingH <= H)
                    .ToList();
            }

            return corners;//מחזיר את רשימת נקודות הפינה האפשריות לאחר הסינון
        }
//פונקציה שמוצאת נקודות תלת מימדיות על סמך נקודות הפינה הדו מימדיות של הארגזים שכבר שובצו, ומוסיפה את הציר Z כדי לקבל את המיקום התלת מימדי של כל נקודת פינה אפשרית בתוך המכולה
//הפונקציה מקבלת את רשימת הארגזים שכבר שובצו, מידות המכולה וכן רשימה של ארגזים שעדיין לא שובצו
public static List<Position3D> Find3DCorners(
            IReadOnlyList<PlacedBox>  placedBoxes,
            ContainerDimensions       container,
            IEnumerable<BoxInstance>? remainingInstances = null)
        {
            if (placedBoxes.Count == 0)//אם אין ארגזים שכבר שובצו, מחזירה את נקודת הפינה היחידה (0,0,0)
                return new List<Position3D> { new Position3D(0, 0, 0) };
//יוצרת רשימה של כל הארגזים שעדיין לא שובצו עם המידות המינימליות שלהם, כדי שנוכל לסנן נקודות פינה שלא יכולות להכיל אותם
var remaining = remainingInstances?
                .Select(b =>
                {
                    var box = b.BoxDefinition;//מוציא את נתוני הארגז מההגדרה שלו כדי לדעת את המידות שלו
                    double minW, minH, minD;
                    if (box.AllowRotation)//אם הארגז מאפשר סיבוב - יכול להיות עם המידה הקטה ביותר בכל כיוון
                    {
                        var dims = new[] { box.Width, box.Height, box.Depth };
                        minW = dims.Min();
                        minH = dims.Min();
                        minD = dims.Min();
                    }
                    else//אם הארגז לא מאפשר סיבוב - נותרות המידות המקוריות.
                    {
                        minW = box.Width;
                        minH = box.Height;
                        minD = box.Depth;
                    }
                    return (minW, minH, minD);//מחזיר את המידות המינימליות לכל ארגז שלא שובץ כדי שנוכל להשתמש בהן לסינון נקודות הפינה
                })
                .ToList() ?? new List<(double, double, double)>();//אם אין ארגזים שלא שובצו, יוצר רשימה ריקה 
//לוקחת את נקודות הדו מימדיות האפשריות ומוסיפה להם את ציר Z על מנת ליצור את המיקומים התלת מימדיים שלהם.
var zCoords = new SortedSet<double> { 0.0 };//יוצרת רשימה מסודרת של נקודות Z שמתחילה בציר Z של המכולה (0.0)
            foreach (var pb in placedBoxes)//עובר על כל הארגזים שכבר שובצו ומוסיף את הZ שלהם לרשימת המקומות הפנויים
                zCoords.Add(pb.Z2);

            var result = new List<Position3D>();//יוצרת רשימה של מיקומים תלת מימדיים
            List<(double x, double y)>? prevCorners2D = null;//משתנה שמכיל את נקודות הפינה הדו מימדיות מהסיבוב הקודם

            foreach (double z0 in zCoords)//בודק את כל העומקים האפשריים שנוצרו על ידי הארגזים שכבר שובצו
            {
                if (remaining.Count > 0)//במידה ויש ארגזים שלא שובצו בודק אם הם יכולים להיכנס בנקודת הZ הנוכחית. אם נקודת הZ 
                {
                    double minD = remaining.Min(r => r.minD);//מוצא את העומק המינימלי של הארגזים שלא שובצו
                    if (z0 + minD > container.Depth) break;//בודק אם הנקודה הזו מתאימה לארגז הקטן ביותר ולא חורגת. במידה ולא, נסוג מכלל הבדיקות האחרות
                }
                else if (z0 >= container.Depth) break;//אין ארגזים שלא שובצו.

var activeBoxes = placedBoxes//מוצא את כל הארגזים שכבר שובצו שנמצאים מעל נקודת הZ הנוכחית 
                    .Where(pb => pb.Z2 > z0)//הZ של סוף הארגז גבוה יותר מהZ הנוכחי
                    .Select(pb => (x1: pb.X1, x2: pb.X2, y1: pb.Y1, y2: pb.Y2))//יוצר טפל של הארגזים עם הפינות שלהם ברמה הדו מימדית (X ו-Y)
                    .ToList();//מכניס אותם לרשימה כדי שנוכל להשתמש בהם ליצירת נקודות הפינה הדו מימדיות
                
                var remainingForFilter = remaining
                    .Select(r => (r.minW, r.minH))
                    .ToList();

                var corners2D = Find2DCorners(
                    activeBoxes,
                    container.Width,
                    container.Height,
                    remainingForFilter.Count > 0 ? remainingForFilter : null);

foreach (var (cx, cy) in corners2D)
                    result.Add(new Position3D(cx, cy, z0));

                prevCorners2D = corners2D;
            }

            return result;
        }
    }
}
