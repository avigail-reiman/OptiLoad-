using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;
using OptiLoad.Data;
using System.Security.Claims;

namespace OptiLoad.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PackingJobController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly PackingService  _packingService;

        public PackingJobController(DatabaseService db, PackingService packingService)
        {
            _db = db;
            _packingService = packingService;
        }

        private int GetCurrentAdminId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PackingJob>>> GetAll()
        {
            var jobs = await _db.GetAllJobs(GetCurrentAdminId());
            return Ok(jobs);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PackingJob>> GetById(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            if (job.AdminId != GetCurrentAdminId()) return Forbid();
            return Ok(job);
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] CreateJobRequest request)
        {
            int jobId = await _db.CreatePackingJob(request.ContainerId, GetCurrentAdminId());
            foreach (var item in request.Boxes)
                await _db.AddBoxToJob(jobId, item.BoxId, item.Quantity);
            return CreatedAtAction(nameof(GetById), new { id = jobId }, jobId);
        }

        [HttpPost("{id}/run")]
        public async Task<ActionResult<PackingResult>> Run(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            if (job.AdminId != GetCurrentAdminId()) return Forbid();
            var result = await _packingService.RunPackingJob(id);
            return Ok(result);
        }

        [HttpGet("{id}/placements")]
        public async Task<ActionResult<IEnumerable<PlacementResult>>> GetPlacements(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            if (job.AdminId != GetCurrentAdminId()) return Forbid();
            var placements = await _db.GetPlacementResults(id);
            return Ok(placements);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            if (job.AdminId != GetCurrentAdminId()) return Forbid();
            await _db.DeleteJob(id);
            return NoContent();
        }

        // ⚠️ DEAD CODE — endpoint זה (POST /api/packing/demo) לא נקרא מאף דף client
        [HttpPost("demo")]
        public ActionResult<PackingResult> RunDemo()
        {
            var container = new ContainerDimensions
            {
                Width = 589.0, Height = 239.0, Depth = 235.0, MaxWeightKg = 21770.0
            };

            var boxes = new List<Box>
            {
                
                new() { BoxId=1,  BoxName="TINY-A",     Width=20,  Height=20,  Depth=20,  WeightKg=1,  AllowRotation=true  },
                new() { BoxId=2,  BoxName="TINY-B",     Width=30,  Height=20,  Depth=25,  WeightKg=2,  AllowRotation=true  },
                
                new() { BoxId=3,  BoxName="SMALL-A",    Width=40,  Height=40,  Depth=40,  WeightKg=4,  AllowRotation=true  },
                new() { BoxId=4,  BoxName="SMALL-B",    Width=60,  Height=50,  Depth=55,  WeightKg=7,  AllowRotation=true  },
                new() { BoxId=5,  BoxName="SMALL-C",    Width=70,  Height=40,  Depth=60,  WeightKg=8,  AllowRotation=true  },
                
                new() { BoxId=6,  BoxName="MED-A",      Width=80,  Height=80,  Depth=80,  WeightKg=15, AllowRotation=true  },
                new() { BoxId=7,  BoxName="MED-B",      Width=100, Height=90,  Depth=100, WeightKg=20, AllowRotation=true  },
                new() { BoxId=8,  BoxName="MED-TALL",   Width=90,  Height=150, Depth=90,  WeightKg=22, AllowRotation=true  },
                new() { BoxId=9,  BoxName="MED-WIDE",   Width=140, Height=80,  Depth=100, WeightKg=18, AllowRotation=true  },
                
                new() { BoxId=10, BoxName="LARGE-A",    Width=150, Height=100, Depth=120, WeightKg=35, AllowRotation=true  },
                new() { BoxId=11, BoxName="LARGE-B",    Width=200, Height=120, Depth=150, WeightKg=50, AllowRotation=true  },
                new() { BoxId=12, BoxName="LARGE-FLAT", Width=230, Height=60,  Depth=180, WeightKg=40, AllowRotation=true  },
                
                new() { BoxId=13, BoxName="FRAGILE-S",  Width=50,  Height=40,  Depth=50,  WeightKg=3,  IsFragile=true, AllowRotation=false },
                new() { BoxId=14, BoxName="FRAGILE-M",  Width=90,  Height=70,  Depth=90,  WeightKg=10, IsFragile=true, AllowRotation=false },
            };

            var quantities = new Dictionary<int,int>
            {
                {1,10},{2,8},                  
                {3,7},{4,6},{5,5},             
                {6,4},{7,3},{8,2},{9,3},       
                {10,2},{11,2},{12,1},           
                {13,3},{14,2}                   
            };

            var instances = new List<BoxInstance>();
            foreach (var box in boxes)
            {
                int qty = quantities[box.BoxId];
                for (int i = 1; i <= qty; i++)
                    instances.Add(new BoxInstance { BoxDefinition = box, InstanceIndex = i });
            }

            var service = new PackingService();
            var result  = service.RunPackingJobInMemory(container, instances);
            return Ok(result);
        }
        // ⚠️ END DEAD CODE
    }

    public class CreateJobRequest
    {
        public int                    ContainerId { get; set; }
        public List<BoxQuantityItem>  Boxes       { get; set; } = new();
    }
    public class BoxQuantityItem
    {
        public int BoxId    { get; set; }
        public int Quantity { get; set; }
    }
}
