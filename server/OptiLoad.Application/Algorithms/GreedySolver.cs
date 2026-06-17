using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Application.Algorithms
{

    public static class GreedySolver
    {
        //פונקציה שמכניסה בצורה חמדנית את כל הארגזים שהאלגוריתם B&B לא טיפל בהם. 
        //מקבלת את רשימת הארגזים, רשימת המכולות הפתוחות, סוג המכולה, וכן את הגובה המקסימלי המותר לארוז בתוכה
        public static List<BoxInstance> FillRemaining(
            List<BoxInstance>   remainingBoxes,
            List<PackingState>  openBins,
            ContainerDimensions container,
            double maxFillHeightRatio = AlgorithmConfig.DefaultMaxFillHeightRatio)
        {
            var unplaced = new List<BoxInstance>();//יוצר רשימה חדשה וריקה לארגזים שלא שובצו

            var sorted = remainingBoxes//ממיין את כל הארגזים לפי נפח יורד.
                .OrderByDescending(b => b.BoxDefinition.Volume)
                .ToList();

            foreach (var instance in sorted)//עובר על כל הארגזים
            {
                bool placed = false;//מסמן אותם כ"לא משובצים"

                foreach (var bin in openBins.OrderByDescending(b => b.UsedVolume))//עובר על המכולות הפתוחות וממין אותם מהמלאה ביותר לריקה ביותר, נותן עדיפות לסיים מכולות.
                {
                    if (TryPlaceGreedy(instance, bin, container, maxFillHeightRatio))//שולח לפונקציה שמנסה להכניס את הארגז למכולה הכי מלאה הנוכחית, אם הצליח לסמן אותו כ"משובץ" ולצאת מהלולאה כי כבר שובץ
                    {
                        placed = true;
                        break;
                    }
                }

                if (!placed)//אם לא הצליח לשבץ את הארגז באף אחת מהמכולות הפתוחות, לנסות לפתוח מכולה חדשה רק בשבילו
                {
                    var newBin = new PackingState();//פותח מכולה חדשה ריקה
                    if (TryPlaceGreedy(instance, newBin, container, maxFillHeightRatio))//אם מצליח להכניס את הארגז למכולה חדשה
                    {
                        openBins.Add(newBin);//מוסיף את המכולה החדשה לרשימת המכולות הפתוחות
                        placed = true;//מסמן את הארגז כ"משובץ"
                    }
                }

                if (!placed)
                    unplaced.Add(instance);
            }

            return unplaced;
        }

//פונקציה שמנסה להכניס ארגז מסוים לתוך מכולה מסוימת
//הפונקציה מקבלתאת מופע הארגז, מופע המכולה, מידות המכולה, ועד איזה גובה מותר לארוז בתוכה
private static bool TryPlaceGreedy(
            BoxInstance instance,
            PackingState bin,
            ContainerDimensions container,
            double maxFillHeightRatio)
        {
            var corners = CornerPointsFinder.Find3DCorners(
                bin.PlacedBoxes,
                container);

            bool isFragile = instance.BoxDefinition.IsFragile;

            var sortedCorners = corners.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();

            foreach (var rotation in instance.BoxDefinition.GetAllowedRotations())
            {
                foreach (var corner in sortedCorners)
                {
                    if (IsValidPlacement(instance, corner, rotation,
                                         bin, container, maxFillHeightRatio, isFragile))
                    {
                        var placed = new PlacedBox(instance, corner, rotation);
                        bin.AddBox(placed);
                        return true;
                    }
                }
            }

            return false;
        }
//בודק אם אפשר להניח ארגז במקום מסוים - מבחינת חפיפה, קונטיינר, שבירות 
private static bool IsValidPlacement(
            BoxInstance instance,
            Position3D corner,
            Rotation rotation,
            PackingState bin,
            ContainerDimensions container,
            double maxFillHeightRatio,
            bool isFragile)
        {
            double maxHeight = container.Height * maxFillHeightRatio;

            if (corner.X + rotation.W > container.Width  + AlgorithmConfig.Epsilon ||
                corner.Y + rotation.H > maxHeight + AlgorithmConfig.Epsilon ||
                corner.Z + rotation.D > container.Depth  + AlgorithmConfig.Epsilon)
                return false;

            if (bin.UsedWeightKg + instance.BoxDefinition.WeightKg >
                container.MaxWeightKg + AlgorithmConfig.Epsilon)
                return false;

            var candidate = new PlacedBox(instance, corner, rotation);

            foreach (var existing in bin.PlacedBoxes)
            {
                if (candidate.OverlapsWith(existing))
                    return false;
            }
            // אין להניח ארגז על גבי ארגז שביר
            foreach (var existing in bin.PlacedBoxes)
            {
                if (existing.Instance.BoxDefinition.IsFragile)
                {
                    if (Math.Abs(corner.Y - existing.Y2) < AlgorithmConfig.Epsilon)
                    {
                        bool overlapX = corner.X < existing.X2 - AlgorithmConfig.Epsilon &&
                                        corner.X + rotation.W > existing.X1 + AlgorithmConfig.Epsilon;
                        bool overlapZ = corner.Z < existing.Z2 - AlgorithmConfig.Epsilon &&
                                        corner.Z + rotation.D > existing.Z1 + AlgorithmConfig.Epsilon;
                        if (overlapX && overlapZ) return false;
                    }
                }
            }
            if (isFragile)
            {
                double candX1 = corner.X, candX2 = corner.X + rotation.W;
                double candY1 = corner.Y, candY2 = corner.Y + rotation.H;
                double candZ1 = corner.Z, candZ2 = corner.Z + rotation.D;

                foreach (var existing in bin.PlacedBoxes)
                {
                    if (!existing.Instance.BoxDefinition.IsFragile)
                    {
                        if (existing.Y1 < candY2 - AlgorithmConfig.Epsilon &&
                            existing.Y2 > candY1 + AlgorithmConfig.Epsilon &&
                            existing.Y1 >= candY1 - AlgorithmConfig.Epsilon)
                        {
                            bool overlapX = existing.X1 < candX2 - AlgorithmConfig.Epsilon &&
                                            existing.X2 > candX1 + AlgorithmConfig.Epsilon;
                            bool overlapZ = overlapX &&
                                            existing.Z1 < candZ2 - AlgorithmConfig.Epsilon &&
                                            existing.Z2 > candZ1 + AlgorithmConfig.Epsilon;
                            if (overlapZ) return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}

