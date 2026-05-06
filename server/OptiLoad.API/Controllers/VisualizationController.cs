using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OptiLoad.API.DTOs;
using OptiLoad.Core.Application.Algorithms;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;
using OptiLoad.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreLoadingFace = OptiLoad.Core.Application.Algorithms.LoadingFace;

namespace OptiLoad.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VisualizationController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly PackingService   _packing;
    private readonly IMemoryCache     _cache;

    public VisualizationController(DatabaseService db, PackingService packing, IMemoryCache cache)
    {
        _db      = db;
        _packing = packing;
        _cache   = cache;
    }

[HttpGet("3d/{jobId:int}")]
    [Produces("text/html")]
    public async Task<ContentResult> ViewJobFromDb(int jobId)
    {
        var job        = await _db.GetJobById(jobId);
        var placements = await _db.GetPlacementResults(jobId);

        if (job == null || placements.Count == 0)
            return Content("<h2>משימה לא נמצאה או ריקה</h2>", "text/html; charset=utf-8");

        var json = JsonSerializer.Serialize(new
        {
            placedBoxes = placements.Select(pr => new
            {
                name      = pr.BoxName,
                id        = pr.PlacementId,
                binIndex  = pr.BinIndex,
                x1 = pr.PosX,         y1 = pr.PosY,          z1 = pr.PosZ,
                x2 = pr.PosX + pr.PlacedWidth,
                y2 = pr.PosY + pr.PlacedHeight,
                z2 = pr.PosZ + pr.PlacedDepth,
                isFragile = pr.IsFragile
            }),
            binsUsed          = job.BinsUsed ?? 0,
            volumeUtilization = job.VolumeUtilization ?? 0.0,
            wastedSpacePercent = (1.0 - (job.VolumeUtilization ?? 0.0)) * 100.0,
            perBinStats       = BuildPerBinStatsFromPlacements(placements, job.BinsUsed ?? 1, job.VolumeUtilization ?? 0),
            placedCount       = placements.Count,
            unplacedCount     = 0,
            solveTime         = $"{(job.SolveTimeSeconds ?? 0) * 1000:F0}ms",
            isOptimal         = job.IsOptimal ?? false,
            jobId             = jobId,
            source            = "DB"
        });

        return Content(HtmlTemplate.Replace("", json), "text/html; charset=utf-8");
    }

[HttpGet("run")]
    [Produces("text/html")]
    public async Task<ContentResult> RunAndVisualize()
    {

var boxDefs = new[]
        {
            
            ("PALLET-LG",    120.0, 144.0,  80.0, 500.0, false, false,  2),  
            ("PALLET-SM",     80.0, 120.0,  60.0, 250.0, false, false,  3),  
            ("CRATE-LG",     100.0, 100.0, 100.0, 400.0, false, true,   2),  
            ("CRATE-SM",      60.0,  80.0,  60.0, 150.0, false, true,   4),  
            ("CARTON-LG",     80.0,  70.0,  60.0,  40.0, false, true,   5),  
            ("CARTON-MED",    50.0,  50.0,  50.0,  20.0, false, true,   6),  
            ("CARTON-SM",     40.0,  30.0,  30.0,   8.0, false, true,   8),  
            ("DRUM",          60.0,  90.0,  60.0, 200.0, false, false,  3),  
            ("ELEC-BOX",      60.0,  50.0,  60.0,  20.0, true,  false,  3),  
            ("GLASS-PANEL",  180.0,  10.0, 120.0,  30.0, true,  false,  1),  
        };

var boxIds = new List<(int id, int qty)>();
        foreach (var (name, w, h, d, kg, fragile, rot, qty) in boxDefs)
        {
            int bid = await _db.CreateBox(new Box
            {
                BoxName       = name,
                Width         = w, Height = h, Depth = d,
                WeightKg      = kg,
                IsFragile     = fragile,
                AllowRotation = rot,
                CreatedAt     = DateTime.UtcNow
            });
            boxIds.Add((bid, qty));
        }

int containerId = await _db.CreateContainer(1, $"VIS-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}");

int jobId = await _db.CreatePackingJob(containerId);
        foreach (var (bid, qty) in boxIds)
            await _db.AddBoxToJob(jobId, bid, qty);

var result = await _packing.RunPackingJobWithTimeLimit(jobId, 8.0);

return await ViewJobFromDb(jobId);
    }

[HttpGet("3d")]
    [Produces("text/html")]
    public ContentResult View3D()
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

        var quantities = new Dictionary<int, int>
        {
            {1,10},{2,8},{3,7},{4,6},{5,5},
            {6,4},{7,3},{8,2},{9,3},
            {10,2},{11,2},{12,1},
            {13,3},{14,2}
        };

        var instances = new List<BoxInstance>();
        foreach (var box in boxes)
            for (int i = 1; i <= quantities[box.BoxId]; i++)
                instances.Add(new BoxInstance { BoxDefinition = box, InstanceIndex = i });

        var service = new PackingService();
        var result  = service.RunPackingJobInMemory(container, instances);

var json = JsonSerializer.Serialize(new
        {
            placedBoxes = LoadingSequencer.Sequence(result.PlacedBoxes)
                .Select(t => new
                {
                    name         = t.Box.Instance.BoxDefinition.BoxName,
                    id           = t.Box.Instance.InstanceId,
                    binIndex     = t.Box.BinIndex,
                    x1 = t.Box.X1, y1 = t.Box.Y1, z1 = t.Box.Z1,
                    x2 = t.Box.X2, y2 = t.Box.Y2, z2 = t.Box.Z2,
                    isFragile    = t.Box.Instance.BoxDefinition.IsFragile,
                    loadingStep  = t.LoadingStep
                }),
            binsUsed          = result.BinsUsed,
            volumeUtilization = result.VolumeUtilization,
            wastedSpacePercent = (1.0 - result.VolumeUtilization) * 100.0,
            perBinStats       = result.PerBinStats.Select(s => new
            {
                binIndex          = s.BinIndex,
                usedVolumePercent = s.UtilizationPercent,
                wastedPercent     = s.WastedPercent
            }).ToList(),
            placedCount       = result.PlacedBoxes.Count,
            unplacedCount     = result.UnplacedBoxes.Count,
            solveTime         = result.SolveTime.TotalMilliseconds.ToString("F0") + "ms",
            isOptimal         = result.IsOptimal
        });

        var html = HtmlTemplate.Replace("", json);
        return Content(html, "text/html; charset=utf-8");
    }

[HttpPost("run")]
    public async Task<ActionResult> RunPacking([FromBody] VisualizationRunRequest request)
    {
        
        var cacheKey = "packing_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Ok(cached);

        var container = new ContainerDimensions
        {
            Width       = request.Container.Width,
            Height      = request.MaxFillHeight.HasValue
                              ? Math.Min(request.Container.Height, request.MaxFillHeight.Value)
                              : request.Container.Height,
            Depth       = request.Container.Depth,
            MaxWeightKg = request.Container.MaxWeightKg
        };

var boxIdMap = new Dictionary<string, int>(); 
        foreach (var bc in request.Boxes)
        {
            var box = new Box
            {
                BoxName       = string.IsNullOrWhiteSpace(bc.Name) ? $"Box_{bc.W}x{bc.H}x{bc.D}" : bc.Name,
                Width         = bc.W,
                Height        = bc.H,
                Depth         = bc.D,
                WeightKg      = bc.Weight,
                IsFragile     = bc.Fragile,
                AllowRotation = bc.AllowRotation
            };
            boxIdMap[box.BoxName] = await _db.UpsertBox(box);
        }

int templateId = await _db.UpsertContainerTemplate(container);

int containerId = await _db.CreateContainer(templateId,
            $"VIS-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}");
        int jobId = await _db.CreatePackingJob(containerId);

var boxQtyByBoxId = new Dictionary<int, int>();
        foreach (var bc in request.Boxes)
        {
            var name = string.IsNullOrWhiteSpace(bc.Name) ? $"Box_{bc.W}x{bc.H}x{bc.D}" : bc.Name;
            int bid = boxIdMap[name];
            if (boxQtyByBoxId.ContainsKey(bid))
                boxQtyByBoxId[bid] += bc.Qty;
            else
                boxQtyByBoxId[bid] = bc.Qty;
        }
        foreach (var (bid, qty) in boxQtyByBoxId)
            await _db.AddBoxToJob(jobId, bid, qty);

double timeLimit = Math.Clamp(request.TimeLimitSeconds > 0 ? request.TimeLimitSeconds : 10.0, 1.0, 300.0);
        var result = await _packing.RunPackingJobWithTimeLimit(jobId, timeLimit);

        var loadingFace = Enum.TryParse<CoreLoadingFace>(request.LoadingFace, true, out var lf)
            ? lf : CoreLoadingFace.Front;

        var response = new
        {
            jobId             = jobId,
            containerW        = container.Width,
            containerH        = container.Height,
            containerD        = container.Depth,
            loadingFace       = loadingFace.ToString(),
            placedBoxes = LoadingSequencer.Sequence(result.PlacedBoxes, loadingFace)
                .Select(t => new
                {
                    name         = t.Box.Instance.BoxDefinition.BoxName,
                    id           = t.Box.Instance.InstanceId,
                    binIndex     = t.Box.BinIndex,
                    x1 = t.Box.X1, y1 = t.Box.Y1, z1 = t.Box.Z1,
                    x2 = t.Box.X2, y2 = t.Box.Y2, z2 = t.Box.Z2,
                    isFragile    = t.Box.Instance.BoxDefinition.IsFragile,
                    loadingStep  = t.LoadingStep
                }),
            unplacedBoxes     = result.UnplacedBoxes
                .GroupBy(b => b.BoxDefinition.BoxName)
                .Select(g => new { name = g.Key, count = g.Count() })
                .ToList(),
            binsUsed          = result.BinsUsed,
            volumeUtilization = result.VolumeUtilization,
            wastedSpacePercent = (1.0 - result.VolumeUtilization) * 100.0,
            perBinStats       = result.PerBinStats.Select(s => new
            {
                binIndex          = s.BinIndex,
                usedVolumePercent = s.UtilizationPercent,
                wastedPercent     = s.WastedPercent
            }).ToList(),
            placedCount       = result.PlacedBoxes.Count,
            unplacedCount     = result.UnplacedBoxes.Count,
            solveTime         = result.SolveTime.TotalMilliseconds.ToString("F0") + "ms",
            isOptimal         = result.IsOptimal
        };
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
        return Ok(response);
    }

private static object BuildPerBinStatsFromPlacements(
        IEnumerable<PlacementResult> placements, int binsUsed, double utilization)
    {
        var list = placements.ToList();
        double totalBoxVol = list.Sum(p => p.PlacedWidth * p.PlacedHeight * p.PlacedDepth);
        
        double containerVol = (binsUsed > 0 && utilization > 0)
            ? totalBoxVol / (binsUsed * utilization)
            : totalBoxVol;
        return Enumerable.Range(0, Math.Max(1, binsUsed)).Select(b =>
        {
            double binBoxVol = list.Where(p => p.BinIndex == b)
                                   .Sum(p => p.PlacedWidth * p.PlacedHeight * p.PlacedDepth);
            double usedPct   = containerVol > 0 ? binBoxVol / containerVol * 100.0 : 0;
            return new { binIndex = b, usedVolumePercent = usedPct, wastedPercent = 100.0 - usedPct };
        }).ToList();
    }

private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="he">
        <head>
        <meta charset="UTF-8">
        <title>OptiLoad – תצוגת 3D</title>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{background:#0d1117;color:#e6edf3;font-family:'Segoe UI',Tahoma,sans-serif;overflow:hidden}
        #panel{position:fixed;top:14px;left:14px;background:rgba(13,17,23,.94);border:1px solid #30363d;
               border-radius:12px;padding:18px;min-width:200px;backdrop-filter:blur(10px);z-index:10;
               box-shadow:0 4px 24px rgba(0,0,0,.5)}
        #panel h2{font-size:16px;margin-bottom:14px;color:#58a6ff;letter-spacing:.5px}
        .stat{display:flex;justify-content:space-between;font-size:12px;margin:6px 0;gap:16px}
        .val{color:#7ee787;font-weight:600}
        .wasted{color:#f78166!important}
        .bin-row{font-size:11px;margin:4px 0;color:#c9d1d9}
        .bin-bar{height:6px;border-radius:3px;margin-top:2px;background:#21262d;overflow:hidden}
        .bin-bar-fill{height:100%;border-radius:3px;transition:width .4s}
        .bin-bar-used{background:#7ee787}
        .bin-bar-wasted{background:#f78166}
        #legend{position:fixed;top:14px;right:14px;background:rgba(13,17,23,.94);border:1px solid #30363d;
                border-radius:12px;padding:16px;max-height:90vh;overflow-y:auto;min-width:160px;
                backdrop-filter:blur(10px);z-index:10;box-shadow:0 4px 24px rgba(0,0,0,.5)}
        #legend h3{font-size:13px;margin-bottom:10px;color:#58a6ff}
        .li{display:flex;align-items:center;font-size:12px;margin:5px 0;gap:9px}
        .lc{width:15px;height:15px;border-radius:3px;flex-shrink:0;border:1px solid rgba(255,255,255,.15)}
        #tip{position:fixed;bottom:16px;left:50%;transform:translateX(-50%);font-size:11px;color:#8b949e;
             background:rgba(13,17,23,.85);padding:7px 18px;border-radius:20px;pointer-events:none;z-index:10}
        </style>
        </head>
        <body>
        <div id="panel">
          <h2>&#x1F69A; OptiLoad 3D</h2>
          <div class="stat"><span>&#x1F4E6; מכולות</span><span class="val" id="sb"></span></div>
          <div class="stat"><span>&#x1F4CA; ניצול נפח</span><span class="val" id="sv"></span></div>
          <div class="stat"><span>&#x1F6AB; מרחב מבוזבז</span><span class="val wasted" id="sw"></span></div>
          <div class="stat"><span>&#x2705; שובצו</span><span class="val" id="sp"></span></div>
          <div class="stat"><span>&#x274C; לא שובצו</span><span class="val" id="su"></span></div>
          <div class="stat"><span>&#x23F1; זמן חישוב</span><span class="val" id="st"></span></div>
          <div class="stat"><span>&#x1F3AF; אופטימלי</span><span class="val" id="so"></span></div>
          <div id="binBreakdown" style="margin-top:10px;border-top:1px solid #30363d;padding-top:10px;display:none">
            <div style="font-size:11px;color:#8b949e;margin-bottom:6px">פירוט לכל מכולה:</div>
            <div id="binRows"></div>
          </div>
        </div>
        <div id="legend"><h3>&#x1F3A8; סוגי ארגזים</h3><div id="li"></div></div>
        <div id="tip">&#x1F5B1; גרור לסיבוב &nbsp;&middot;&nbsp; גלגלת לזום &nbsp;&middot;&nbsp; Shift+גרור להזזה</div>

        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
        <script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
        <script>
        const R = ;
        const CW=589, CH=239, CD=235, GAP=120;
        const PAL=[0x4c9be8,0x56d364,0xe3b341,0xd2a8ff,0x79c0ff,0xffa657,
                   0xf78166,0xadd7f6,0xb8bb26,0x83a598,0xfe8019,0x8ec07c,0xa9b665];
        const nameColor={};let ci=0;
        function gc(n,f){ if(f) return 0xff8c00; if(!nameColor[n]) nameColor[n]=PAL[ci++%PAL.length]; return nameColor[n]; }

        const renderer=new THREE.WebGLRenderer({antialias:true});
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.setSize(window.innerWidth,window.innerHeight);
        renderer.shadowMap.enabled=true;
        renderer.shadowMap.type=THREE.PCFSoftShadowMap;
        document.body.appendChild(renderer.domElement);

        const scene=new THREE.Scene();
        scene.background=new THREE.Color(0x0d1117);
        scene.fog=new THREE.FogExp2(0x0d1117,0.00028);

        const camera=new THREE.PerspectiveCamera(45,window.innerWidth/window.innerHeight,1,30000);
        const controls=new THREE.OrbitControls(camera,renderer.domElement);
        controls.enableDamping=true; controls.dampingFactor=0.07;
        controls.minDistance=200; controls.maxDistance=9000;

        scene.add(new THREE.AmbientLight(0xffffff,0.6));
        const dl=new THREE.DirectionalLight(0xffffff,0.75);
        dl.position.set(1200,1800,900); dl.castShadow=true;
        dl.shadow.camera.left=-2500; dl.shadow.camera.right=2500;
        dl.shadow.camera.top=1200; dl.shadow.camera.bottom=-600;
        dl.shadow.camera.near=50; dl.shadow.camera.far=7000;
        dl.shadow.mapSize.set(2048,2048); scene.add(dl);
        scene.add(new THREE.HemisphereLight(0x3a6fcc,0x001a0d,0.35));

        const totalW=R.binsUsed*(CW+GAP)-GAP;
        const grid=new THREE.GridHelper(Math.max(totalW*1.6,2000),60,0x161b22,0x21262d);
        grid.position.set(totalW/2,-0.5,CD/2); scene.add(grid);

        function addBin(ox,bi){
          const col=new THREE.Color(bi===0?0x58a6ff:0x3fb950);
          const geo=new THREE.BoxGeometry(CW,CH,CD);
          const ls=new THREE.LineSegments(
            new THREE.EdgesGeometry(geo),
            new THREE.LineBasicMaterial({color:col,transparent:true,opacity:.75})
          );
          ls.position.set(ox+CW/2,CH/2,CD/2); scene.add(ls);
          
          const fp=new THREE.Mesh(
            new THREE.PlaneGeometry(CW,CD),
            new THREE.MeshStandardMaterial({color:bi===0?0x0c1f35:0x0c2512,transparent:true,opacity:.35,roughness:1})
          );
          fp.rotation.x=-Math.PI/2; fp.position.set(ox+CW/2,0.3,CD/2);
          fp.receiveShadow=true; scene.add(fp);
        }

        const legSet={};
        R.placedBoxes.forEach(pb=>{
          const ox=(pb.binIndex||0)*(CW+GAP);
          const bw=pb.x2-pb.x1, bh=pb.y2-pb.y1, bd=pb.z2-pb.z1;
          const col=gc(pb.name,pb.isFragile);
          const mesh=new THREE.Mesh(
            new THREE.BoxGeometry(bw-.8,bh-.8,bd-.8),
            new THREE.MeshLambertMaterial({color:col,transparent:true,opacity:.84})
          );
          mesh.position.set(ox+pb.x1+bw/2, pb.y1+bh/2, pb.z1+bd/2);
          mesh.castShadow=true; mesh.receiveShadow=true; scene.add(mesh);
          
          const el=new THREE.LineSegments(
            new THREE.EdgesGeometry(mesh.geometry),
            new THREE.LineBasicMaterial({color:0x000000,transparent:true,opacity:.38})
          );
          el.position.copy(mesh.position); scene.add(el);
          if(!legSet[pb.name]) legSet[pb.name]={c:col,f:pb.isFragile};
        });

        for(let b=0;b<R.binsUsed;b++) addBin(b*(CW+GAP),b);

        controls.target.set(totalW/2,CH/2,CD/2);
        camera.position.set(totalW/2+200, CH*1.9, CD*3.4);
        controls.update();

        Object.entries(legSet).forEach(([n,{c,f}])=>{
          const h='#'+c.toString(16).padStart(6,'0');
          document.getElementById('li').insertAdjacentHTML('beforeend',
            '<div class="li"><div class="lc" style="background:'+h+'"></div>'+n+(f?' &#x1F538;':'')+'</div>');
        });

        document.getElementById('sb').textContent = R.binsUsed;
        document.getElementById('sv').textContent = (R.volumeUtilization*100).toFixed(1)+'%';
        document.getElementById('sw').textContent = (R.wastedSpacePercent||((1-R.volumeUtilization)*100)).toFixed(1)+'%';
        document.getElementById('sp').textContent = R.placedCount;
        document.getElementById('su').textContent = R.unplacedCount;
        document.getElementById('st').textContent = R.solveTime;
        document.getElementById('so').textContent = R.isOptimal ? 'כן ✓' : 'לא (היוריסטי)';

        if(R.perBinStats && R.perBinStats.length > 0){
          document.getElementById('binBreakdown').style.display='block';
          const rows=document.getElementById('binRows');
          R.perBinStats.forEach(s=>{
            const used=s.usedVolumePercent.toFixed(1);
            const wasted=s.wastedPercent.toFixed(1);
            rows.insertAdjacentHTML('beforeend',
              '<div class="bin-row">מכולה '+(s.binIndex+1)+': '+used+'% מנוצל / '+wasted+'% בזבוז'+
              '<div class="bin-bar">'+
                '<div class="bin-bar-fill bin-bar-used" style="width:'+used+'%;display:inline-block"></div>'+
                '<div class="bin-bar-fill bin-bar-wasted" style="width:'+wasted+'%;display:inline-block"></div>'+
              '</div></div>');
          });
        }

        function animate(){ requestAnimationFrame(animate); controls.update(); renderer.render(scene,camera); }
        animate();
        window.addEventListener('resize',()=>{
          camera.aspect=window.innerWidth/window.innerHeight;
          camera.updateProjectionMatrix();
          renderer.setSize(window.innerWidth,window.innerHeight);
        });
        </script>
        </body>
        </html>
        """;
}
