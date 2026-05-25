using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            // ייצוא אוטומטי של תוצאות לאותו נתיב כמו ב-API
            try
            {
                var outputDir = @"C:\Users\1\Desktop\תכנות\משובצות\תוצאות פרויקט שיבוץ לדאטאבייס";
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
                var filePath = Path.Combine(outputDir, $"optiload-export-job{jobId}.json");

                // יצירת אובייקט JSON בפורמט דומה ל-API
                var export = new
                {
                    jobId = jobId,
                    exportedAt = DateTime.UtcNow,
                    totalBoxes = result.PlacedBoxes.Count,
                    loadingSequence = result.PlacedBoxes.Select((pb, idx) => new {
                        loadingStep = idx + 1,
                        boxName = pb.Instance.BoxDefinition.BoxName,
                        x = pb.X1,
                        y = pb.Y1,
                        z = pb.Z1,
                        width = pb.Rotation.W,
                        height = pb.Rotation.H,
                        depth = pb.Rotation.D,
                        binIndex = pb.BinIndex,
                        isFragile = pb.Instance.BoxDefinition.IsFragile
                    }).ToList(),
                    // לא נוס תמונות כאן כי אין Snapshots ב-InMemory
                    snapshots = new object[0]
                };
                var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                System.IO.File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                // אפשר להוסיף לוג או טיפול בשגיאה
            }
            return result;
        }

        public async Task<PackingResult> RunPackingJobWithTimeLimit(int jobId, double timeLimitSeconds)
        {
            if (_db == null)
                throw new InvalidOperationException("DatabaseService לא מוגדר.");

            var container = await _db.GetContainerDimensions(jobId);
            var instances = await _db.GetJobBoxInstances(jobId);

            var solver = new BranchAndBoundSolver(container)
            {
                TimeLimitSeconds = timeLimitSeconds
            };

var result = await Task.Run(() => solver.Solve(instances));

            try
            {
                await _db.SavePlacementResults(jobId, result);
                await _db.CompleteJob(jobId, result);
            }
            catch (Exception ex)
            {
                await _db.LogError(jobId, "RunPackingJobWithTimeLimit", ex);
                throw;
            }

            return result;
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
        [JsonPropertyName("box")]
        public string Box  { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public double X    { get; set; }

        [JsonPropertyName("y")]
        public double Y    { get; set; }

        [JsonPropertyName("z")]
        public double Z    { get; set; }

        [JsonPropertyName("w")]
        public double W    { get; set; }

        [JsonPropertyName("h")]
        public double H    { get; set; }

        [JsonPropertyName("d")]
        public double D    { get; set; }

        [JsonPropertyName("rotation")]
        public int    Rotation { get; set; }

        [JsonPropertyName("fragile")]
        public bool   Fragile { get; set; }
    }
}
