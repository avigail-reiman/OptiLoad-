using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Algorithms
{

    public static class LowerBoundCalculator//חסמים לקיצוץ ענפים
    {
//חסם תחתון L0
//הפונקציה מקבלת את רשימת הארגזים שיש לארוז וכן את מידות המכולה ומחזירה את החסם.
public static int ComputeL0(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            double totalVolume = boxes.Sum(b => b.BoxDefinition.Volume);//מחשבת את סך הנפח של כל הארגזים
            return (int)Math.Ceiling(totalVolume / container.Volume);//מחלקת את נפח הארגזים בנפח מכולה ומעגלת כלפי מעלה לקבלת כמות המכולות המינימלית
        }
//חסם תחתון L1
//הפונקציה מקבלת את רשימת הארגזים שיש לארוז וכן את מידות המכולה ומחזירה את החסם.
public static int ComputeL1(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();//ממיר את רשימת הארגזים לרשימה כדי לאפשר גישה לפי אינדקס
            //פונקציה לבדיקת כמות הארגזים ששתיים ממימדיהם הם יותר מחצי המכולה, לפי שילוב כל מימדים
            int l1WH = ComputeL1OneDim(boxList,
                b => b.Width, b => b.Height, b => b.Depth,
                container.Width, container.Height, container.Depth);

            int l1WD = ComputeL1OneDim(boxList,
                b => b.Width, b => b.Depth, b => b.Height,
                container.Width, container.Depth, container.Height);

            int l1HD = ComputeL1OneDim(boxList,
                b => b.Height, b => b.Depth, b => b.Width,
                container.Height, container.Depth, container.Width);

            return Math.Max(l1WH, Math.Max(l1WD, l1HD));
        }

private static int ComputeL1OneDim(
    List<BoxInstance> boxes,
    Func<Box, double> getDim1,
    Func<Box, double> getDim2,
    Func<Box, double> getDim3,
    double W, double H, double D)
    {
            //קופסאות ש2 המימדים הראשונים גדולים מחצי
        var jWH = boxes
            .Where(b =>
            {
                var box = b.BoxDefinition;
                return getDim1(box) > W / 2.0 && getDim2(box) > H / 2.0;
            })
            .ToList();

        if (jWH.Count == 0) return 0;

        //מתוך הקופסאות הנ"ל, קופסאות שגם המימד השלישי גדול מחצי מהמכולה  
        int largeDepthCount = jWH.Count(b => getDim3(b.BoxDefinition) > D / 2.0);

        int best = largeDepthCount;

        var pValues = jWH
                .Select(b => getDim3(b.BoxDefinition))
                .Where(p => p >= 1 && p <= D / 2.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            foreach (double p in pValues)
            {
                
                var jL = jWH.Where(b =>
                {
                    double d = getDim3(b.BoxDefinition);
                    return d >= D - p && d <= D / 2.0;
                }).ToList();

                var jS = jWH.Where(b =>
                {
                    double d = getDim3(b.BoxDefinition);
                    return d >= D / 2.0 && d <= p;
                }).ToList();

                if (jL.Count == 0 && jS.Count == 0) continue;

                double sumDs   = jS.Sum(b => getDim3(b.BoxDefinition));
                double sumDl   = jL.Sum(b => getDim3(b.BoxDefinition));
                int    countLarge = jWH.Count(b => getDim3(b.BoxDefinition) > D / 2.0);

                double numerator1 = sumDs - (jL.Count * D - sumDl);
                int    bound1     = countLarge + (int)Math.Ceiling(Math.Max(0, numerator1) / D);

                double floorDP    = Math.Floor(D / p);
                double sumFloors  = jL.Sum(b => Math.Floor(getDim3(b.BoxDefinition) / p));
                double numerator2 = jS.Count - sumFloors;
                int    bound2     = countLarge + (int)Math.Ceiling(Math.Max(0, numerator2) / floorDP);

                best = Math.Max(best, Math.Max(bound1, bound2));
            }

            return best;
        }

public static int ComputeL2(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();
            if (boxList.Count == 0) return 0;

            int l2WH = ComputeL2OneDim(boxList,
                b => b.Width, b => b.Height, b => b.Depth,
                container.Width, container.Height, container.Depth);

            int l2WD = ComputeL2OneDim(boxList,
                b => b.Width, b => b.Depth, b => b.Height,
                container.Width, container.Depth, container.Height);

            int l2HD = ComputeL2OneDim(boxList,
                b => b.Height, b => b.Depth, b => b.Width,
                container.Height, container.Depth, container.Width);

            return Math.Max(l2WH, Math.Max(l2WD, l2HD));
        }

        private static int ComputeL2OneDim(
            List<BoxInstance> boxes,
            Func<Box, double> getDim1,
            Func<Box, double> getDim2,
            Func<Box, double> getDim3,
            double W, double H, double D)
        {
            
            int l1Base = ComputeL1OneDim(boxes,
                getDim1, getDim2, getDim3,
                W, H, D);

            double binVolume = W * H * D;
            int best = l1Base;

var pValues = boxes
                .Select(b => getDim1(b.BoxDefinition))
                .Where(p => p >= 1 && p <= W / 2.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

var qValues = boxes
                .Select(b => getDim2(b.BoxDefinition))
                .Where(q => q >= 1 && q <= H / 2.0)
                .Distinct()
                .OrderBy(q => q)
                .ToList();

            foreach (double p in pValues)
            {
                foreach (double q in qValues)
                {
                    
                    var kv = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return getDim1(box) > W - p && getDim2(box) > H - q;
                    }).ToList();

var kl = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return !kv.Contains(b) &&
                               getDim1(box) > W / 2.0 &&
                               getDim2(box) > H / 2.0;
                    }).ToList();

var ks = boxes.Where(b =>
                    {
                        var box = b.BoxDefinition;
                        return !kv.Contains(b) && !kl.Contains(b) &&
                               getDim1(box) >= p && getDim2(box) >= q;
                    }).ToList();

                    if (ks.Count == 0 && kl.Count == 0) continue;

double sumKvDepth  = kv.Sum(b => getDim3(b.BoxDefinition));
                    double sumKlKsVol  = kl.Sum(b => b.BoxDefinition.Volume)
                                       + ks.Sum(b => b.BoxDefinition.Volume);

                    double availableVolume = binVolume * l1Base - W * H * sumKvDepth;
                    double numerator       = sumKlKsVol - availableVolume;
                    int    extraBins       = (int)Math.Ceiling(Math.Max(0, numerator) / binVolume);

                    int bound = l1Base + extraBins;
                    if (bound > best) best = bound;
                }
            }

            return best;
        }

//פונקציה שמקבלת מערך ארגזים ומכולה מסוימת ומחזירה את החסם התחתון ההדוק ביותר שמצאה מבין שלושת החסמים
public static int ComputeBestLowerBound(
            IEnumerable<BoxInstance> boxes,
            ContainerDimensions      container)
        {
            var boxList = boxes.ToList();
            if (boxList.Count == 0) return 0;

            int l0 = ComputeL0(boxList, container);
            int l1 = ComputeL1(boxList, container);
            int l2 = ComputeL2(boxList, container);

            return Math.Max(l0, Math.Max(l1, l2));
        }
    }
}
