using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;

namespace OptiLoad.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/snapshots")]
    public class SnapshotController : ControllerBase
    {
        private readonly ISnapshotRepository _repo;

        public SnapshotController(ISnapshotRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("{jobId}")]
        public async Task<IActionResult> SaveSnapshots(int jobId, [FromBody] List<SnapshotDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return BadRequest("No snapshots provided.");

            var snapshots = dtos.Select(d => new ContainerSnapshot
            {
                JobId       = jobId,
                LoadingStep = d.LoadingStep,
                BoxName     = d.BoxName ?? string.Empty,
                ImageData   = d.ImageData ?? string.Empty
            });

            await _repo.SaveSnapshotsAsync(jobId, snapshots);
            return Ok(new { saved = dtos.Count });
        }

        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetSnapshots(int jobId)
        {
            var snapshots = await _repo.GetSnapshotsAsync(jobId);
            return Ok(snapshots.Select(s => new
            {
                s.Id,
                s.JobId,
                s.LoadingStep,
                s.BoxName,
                s.ImageData,
                s.CreatedAt
            }));
        }

        [HttpDelete("{jobId}")]
        public async Task<IActionResult> DeleteSnapshots(int jobId)
        {
            await _repo.DeleteSnapshotsAsync(jobId);
            return Ok();
        }
    }

    public class SnapshotDto
    {
        public int    LoadingStep { get; set; }
        public string? BoxName   { get; set; }
        public string? ImageData { get; set; }
    }
}
