using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{

    public class PackingService
    {
        private readonly IPackingRepository? _db;

        public PackingService(IPackingRepository? db = null)
        {
            _db = db;
        }

public async Task<PackingResult> RunPackingJob(int jobId)
    {
            if (_db == null)
                throw new InvalidOperationException("DatabaseService לא מוגדר.");

            var container = await _db.GetContainerDimensions(jobId);
            var instances = await _db.GetJobBoxInstances(jobId);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] משימה {jobId}: " +
                              $"{instances.Count} ארגזים, {container}");

            var solver = new BranchAndBoundSolver(container);
            var result = solver.Solve(instances);

            try
            {
                await _db.SavePlacementResults(jobId, result);
                await _db.CompleteJob(jobId, result);
            }
            catch (Exception ex)
            {
                await _db.LogError(jobId, "RunPackingJob", ex);
                throw;
            }

            PrintReport(result, container);

            return result;
            }
        
        
        
public async Task<PackingResult> RunPackingJobWithTimeLimit(int jobId, double timeLimitSeconds)
{
            if (_db == null)//בודק אם שירות הדאטאבייס מוגדר, אם לא, זורק חריגה כי לא ניתן להמשיך בלי גישה לנתונים
                throw new InvalidOperationException("DatabaseService לא מוגדר.");
            //משתמש בשירות הדאטאבייס כדי לקבל את מידות המכולה ואת רשימת מופעי הקופסאות עבור המשימה הנתונה
            var container = await _db.GetContainerDimensions(jobId);
            var instances = await _db.GetJobBoxInstances(jobId);
            //יוצר מופע של הפותר BranchAndBoundSolver עם המכולה הנתונה, ומגדיר את מגבלת הזמן
            var solver = new BranchAndBoundSolver(container)
            {
                TimeLimitSeconds = timeLimitSeconds
            };
            //מריץ את הפותר בצורה אסינכרונית כדי לא לחסום את ה-thread הנוכחי, ומקבל את תוצאות הפתרון
            var result = await Task.Run(() => solver.Solve(instances));

            try//שומר את תוצאות הפתרון במסד
            {
                await _db.SavePlacementResults(jobId, result);//שומר את תוצאות השיבוץ של העבודה במסד הנתונים
                await _db.CompleteJob(jobId, result);//מסיים את העבודה ושומר את תוצאות השיבוץ שלה במסד הנתונים
            }
            catch (Exception ex)//אם יש שגיאה בשמירת התואות, ישמור את השגיאה ויזרוק למשתמש
            {
                await _db.LogError(jobId, "RunPackingJobWithTimeLimit", ex);
                throw;
            }

            return result;//מחזיר את תוצאות הפתרון של משימת האריזה
}



public PackingResult RunPackingJobInMemory(
            ContainerDimensions      container,
            IEnumerable<BoxInstance> instances,
            double                   timeLimitSeconds = 300.0)
{
            var instanceList = instances.ToList();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] In-Memory: " +
                              $"{instanceList.Count} ארגזים, {container}");

            var solver = new BranchAndBoundSolver(container)
            {
                TimeLimitSeconds = timeLimitSeconds
            };
            var result = solver.Solve(instanceList);

            PrintReport(result, container);

            return result;
        }



public static void PrintReport(PackingResult result, ContainerDimensions container)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine("                  OptiLoad – תוצאות                ");
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine($"  מכולות בשימוש:     {result.BinsUsed}");
            Console.WriteLine($"  ניצול נפח:         {result.VolumeUtilization:P1}");
            Console.WriteLine($"  ארגזים שמוקמו:     {result.PlacedBoxes.Count}");
            Console.WriteLine($"  ארגזים שלא מוקמו:  {result.UnplacedBoxes.Count}");
            Console.WriteLine($"  זמן פתרון:         {result.SolveTime.TotalSeconds:F2}s");
            Console.WriteLine($"  אופטימלי:          {(result.IsOptimal ? "כן" : "לא (time/node limit)")}");
            Console.WriteLine($"  סטטוס:             {result.StatusMessage}");
            Console.WriteLine("───────────────────────────────────────────────────");

        var jsonOutput = result.PlacedBoxes.Select(pb => new PlacementJsonDto
            {
                Box      = pb.Instance.InstanceId,
                X        = Math.Round(pb.X1, 4),
                Y        = Math.Round(pb.Y1, 4),
                Z        = Math.Round(pb.Z1, 4),
                W        = Math.Round(pb.Rotation.W, 4),
                H        = Math.Round(pb.Rotation.H, 4),
                D        = Math.Round(pb.Rotation.D, 4),
                Rotation = pb.Rotation.Index,
                Fragile  = pb.Instance.BoxDefinition.IsFragile
            }).ToList();

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented         = true,
                PropertyNamingPolicy  = JsonNamingPolicy.CamelCase
            };

            Console.WriteLine();
            Console.WriteLine("── JSON מיקומים ──");
            Console.WriteLine(JsonSerializer.Serialize(jsonOutput, jsonOptions));

            if (result.UnplacedBoxes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("── ארגזים שלא שובצו ──");
                foreach (var unplaced in result.UnplacedBoxes)
                    Console.WriteLine($"  ✗ {unplaced.InstanceId}");
            }

            Console.WriteLine("═══════════════════════════════════════════════════");
        }
    }



public class PlacementJsonDto
    {
        public string Box     { get; set; } = string.Empty;
        public double X       { get; set; }
        public double Y       { get; set; }
        public double Z       { get; set; }
        public double W       { get; set; }
        public double H       { get; set; }
        public double D       { get; set; }
        public int    Rotation { get; set; }
        public bool   Fragile { get; set; }
    }
}
