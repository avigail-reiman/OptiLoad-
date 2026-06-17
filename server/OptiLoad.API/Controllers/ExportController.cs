using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Application.Algorithms;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;
using OptiLoad.Data;
using System.Security.Claims;
using System.Text.Json;

namespace OptiLoad.API.Controllers;

[Authorize]
[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly DatabaseService     _db;
    private readonly ISnapshotRepository _snapshots;

    public ExportController(DatabaseService db, ISnapshotRepository snapshots)
    {
        _db        = db;
        _snapshots = snapshots;
    }

    private int GetCurrentAdminId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    
    [HttpGet("{jobId:int}")]
    public async Task<IActionResult> ExportJob(int jobId, [FromQuery] string face = "Front")
    {
        var job        = await _db.GetJobById(jobId);
        var placements = await _db.GetPlacementResults(jobId);

        if (job == null || placements.Count == 0)
            return NotFound(new { error = $"Job {jobId} not found or has no placements." });

        if (job.AdminId != GetCurrentAdminId())
            return Forbid();

        // Convert PlacementResult → PlacedBox so LoadingSequencer can compute order
        var placedBoxes = placements.Select(ToPlacedBox).ToList();

        var loadingFace = Enum.TryParse<LoadingFace>(face, ignoreCase: true, out var lf)
            ? lf : LoadingFace.Front;

        var sequenced = LoadingSequencer.Sequence(placedBoxes, loadingFace);

        // Sequencer returns 0-based steps; build lookup by position key → 1-based step
        var stepByKey = sequenced.ToDictionary(
            t => PosKey(t.Box),
            t => t.LoadingStep + 1
        );

        // Build loading-sequence rows (sorted ascending by step)
        var loadingSequence = placements
            .Select(pr => new
            {
                loadingStep = stepByKey.GetValueOrDefault(PosKey(pr), 0),
                boxName     = pr.BoxName,
                x           = pr.PosX,
                y           = pr.PosY,
                z           = pr.PosZ,
                width       = pr.PlacedWidth,
                height      = pr.PlacedHeight,
                depth       = pr.PlacedDepth,
                binIndex    = pr.BinIndex,
                isFragile   = pr.IsFragile
            })
            .OrderBy(r => r.loadingStep)
            .ToList();

        // Get stored snapshots (images already as base64 JPEG strings)

        var snapshots = await _snapshots.GetSnapshotsAsync(jobId);
        var imagesDir = @"C:\Users\1\Desktop\תכנות\משובצות\תוצאות פרויקט שיבוץ לדאטאבייס\images";
        if (!Directory.Exists(imagesDir))
            Directory.CreateDirectory(imagesDir);

        var snapshotRows = new List<object>();
        foreach (var s in snapshots.OrderBy(s => s.LoadingStep))
        {
            // שמירת קובץ תמונה
            var fileName = $"job{jobId}_step{s.LoadingStep}.jpg";
            var filePath = Path.Combine(imagesDir, fileName);
            if (!string.IsNullOrEmpty(s.ImageData))
            {
                try
                {
                    byte[] imageBytes = Convert.FromBase64String(s.ImageData);
                    System.IO.File.WriteAllBytes(filePath, imageBytes);
                }
                catch (Exception ex)
                {
                    // אפשר להוסיף לוג או טיפול בשגיאה
                }
            }
            snapshotRows.Add(new
            {
                loadingStep = s.LoadingStep,
                boxName     = s.BoxName,
                imagePath   = filePath // שמור רק את הנתיב לקובץ
            });
        }

        var export = new
        {
            jobId           = jobId,
            exportedAt      = DateTime.UtcNow,
            loadingFace     = face,
            totalBoxes      = placements.Count,
            loadingSequence,
            snapshots       = snapshotRows
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented        = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // שמירה אוטומטית לנתיב במחשב
        try
        {
            var outputDir = @"C:\Users\1\Desktop\תכנות\משובצות\תוצאות פרויקט שיבוץ לדאטאבייס";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            var filePath = Path.Combine(outputDir, $"optiload-export-job{jobId}.json");
            System.IO.File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            // אפשר להוסיף לוג או טיפול בשגיאה במידת הצורך
        }

        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            $"optiload-export-job{jobId}.json"
        );
    }

    private static PlacedBox ToPlacedBox(PlacementResult pr)
    {
        var box      = new Box { BoxId = pr.BoxId, BoxName = pr.BoxName };
        var instance = new BoxInstance { BoxDefinition = box, InstanceIndex = pr.InstanceIndex };
        var position = new Position3D(pr.PosX, pr.PosY, pr.PosZ);
        var rotation = new Rotation(pr.PlacedWidth, pr.PlacedHeight, pr.PlacedDepth, pr.RotationIndex);
        return new PlacedBox(instance, position, rotation) { BinIndex = pr.BinIndex };
    }

    private static string PosKey(PlacedBox b)       => $"{b.X1:F4}|{b.Y1:F4}|{b.Z1:F4}|{b.BinIndex}";
    private static string PosKey(PlacementResult pr) => $"{pr.PosX:F4}|{pr.PosY:F4}|{pr.PosZ:F4}|{pr.BinIndex}";
}
