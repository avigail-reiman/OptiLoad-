using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;
using OptiLoad.Data;

namespace OptiLoad.API.Controllers
{
    /// <summary>
    /// ניהול משימות שיבוץ – יצירה, הרצה, שליפת תוצאות
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PackingJobController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly PackingService  _packingService;

        public PackingJobController(DatabaseService db, PackingService packingService)
        {
            _db             = db;
            _packingService = packingService;
        }

        /// <summary>שליפת כל המשימות</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PackingJob>>> GetAll()
        {
            var jobs = await _db.GetAllJobs();
            return Ok(jobs);
        }

        /// <summary>שליפת משימה לפי מזהה</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<PackingJob>> GetById(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            return Ok(job);
        }

        /// <summary>יצירת משימה חדשה</summary>
        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] CreateJobRequest request)
        {
            int jobId = await _db.CreatePackingJob(request.ContainerId);
            foreach (var item in request.Boxes)
                await _db.AddBoxToJob(jobId, item.BoxId, item.Quantity);
            return CreatedAtAction(nameof(GetById), new { id = jobId }, jobId);
        }

        /// <summary>הרצת אלגוריתם שיבוץ עבור משימה קיימת</summary>
        [HttpPost("{id}/run")]
        public async Task<ActionResult<PackingResult>> Run(int id)
        {
            var result = await _packingService.RunPackingJob(id);
            return Ok(result);
        }

        /// <summary>שליפת תוצאות שיבוץ עבור משימה</summary>
        [HttpGet("{id}/placements")]
        public async Task<ActionResult<IEnumerable<PlacementResult>>> GetPlacements(int id)
        {
            var placements = await _db.GetPlacementResults(id);
            return Ok(placements);
        }

        /// <summary>מחיקת משימה (כולל תוצאות שיבוץ)</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var job = await _db.GetJobById(id);
            if (job == null) return NotFound();
            await _db.DeleteJob(id);
            return NoContent();
        }

        /// <summary>
        /// הדגמה בלי DB – מריץ אלגוריתם In-Memory ומחזיר מיקומי ארגזים מיידית.
        /// אין צורך במסד נתונים!
        /// </summary>
                        [HttpPost("demo")]
        public ActionResult<PackingResult> RunDemo()
        {
            var container = new ContainerDimensions
            {
                Width = 589.0, Height = 239.0, Depth = 235.0, MaxWeightKg = 21770.0
            };

            var boxes = new List<Box>
            {
                // זעירים (20-40 ס"מ)
                new() { BoxId=1,  BoxName="TINY-A",     Width=20,  Height=20,  Depth=20,  WeightKg=1,  AllowRotation=true  },
                new() { BoxId=2,  BoxName="TINY-B",     Width=30,  Height=20,  Depth=25,  WeightKg=2,  AllowRotation=true  },
                // קטנים (40-80 ס"מ)
                new() { BoxId=3,  BoxName="SMALL-A",    Width=40,  Height=40,  Depth=40,  WeightKg=4,  AllowRotation=true  },
                new() { BoxId=4,  BoxName="SMALL-B",    Width=60,  Height=50,  Depth=55,  WeightKg=7,  AllowRotation=true  },
                new() { BoxId=5,  BoxName="SMALL-C",    Width=70,  Height=40,  Depth=60,  WeightKg=8,  AllowRotation=true  },
                // בינוניים (80-150 ס"מ)
                new() { BoxId=6,  BoxName="MED-A",      Width=80,  Height=80,  Depth=80,  WeightKg=15, AllowRotation=true  },
                new() { BoxId=7,  BoxName="MED-B",      Width=100, Height=90,  Depth=100, WeightKg=20, AllowRotation=true  },
                new() { BoxId=8,  BoxName="MED-TALL",   Width=90,  Height=150, Depth=90,  WeightKg=22, AllowRotation=true  },
                new() { BoxId=9,  BoxName="MED-WIDE",   Width=140, Height=80,  Depth=100, WeightKg=18, AllowRotation=true  },
                // גדולים (150-250 ס"מ)
                new() { BoxId=10, BoxName="LARGE-A",    Width=150, Height=100, Depth=120, WeightKg=35, AllowRotation=true  },
                new() { BoxId=11, BoxName="LARGE-B",    Width=200, Height=120, Depth=150, WeightKg=50, AllowRotation=true  },
                new() { BoxId=12, BoxName="LARGE-FLAT", Width=230, Height=60,  Depth=180, WeightKg=40, AllowRotation=true  },
                // שבירים
                new() { BoxId=13, BoxName="FRAGILE-S",  Width=50,  Height=40,  Depth=50,  WeightKg=3,  IsFragile=true, AllowRotation=false },
                new() { BoxId=14, BoxName="FRAGILE-M",  Width=90,  Height=70,  Depth=90,  WeightKg=10, IsFragile=true, AllowRotation=false },
            };

            var quantities = new Dictionary<int,int>
            {
                {1,10},{2,8},                  // זעירים
                {3,7},{4,6},{5,5},             // קטנים
                {6,4},{7,3},{8,2},{9,3},       // בינוניים
                {10,2},{11,2},{12,1},           // גדולים
                {13,3},{14,2}                   // שבירים
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
