using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Data;

namespace OptiLoad.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class BoxController : ControllerBase
    {
        private readonly DatabaseService _db;

        public BoxController(DatabaseService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Box>>> GetAll()
        {
            var boxes = await _db.GetAllBoxes();
            return Ok(boxes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Box>> GetById(int id)
        {
            var box = await _db.GetBoxById(id);
            if (box == null) return NotFound();
            return Ok(box);
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] Box box)
        {
            int newId = await _db.CreateBox(box);
            return CreatedAtAction(nameof(GetById), new { id = newId }, newId);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Box box)
        {
            var existing = await _db.GetBoxById(id);
            if (existing == null) return NotFound();
            await _db.UpdateBox(id, box);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _db.DeleteBox(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
