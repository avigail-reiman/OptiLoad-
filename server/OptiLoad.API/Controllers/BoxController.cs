using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Data;

namespace OptiLoad.API.Controllers
{
    /// <summary>
    /// ניהול ארגזים – CRUD בסיסי
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BoxController : ControllerBase
    {
        private readonly DatabaseService _db;

        public BoxController(DatabaseService db)
        {
            _db = db;
        }

        /// <summary>שליפת כל הארגזים</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Box>>> GetAll()
        {
            var boxes = await _db.GetAllBoxes();
            return Ok(boxes);
        }

        /// <summary>שליפת ארגז לפי מזהה</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Box>> GetById(int id)
        {
            var box = await _db.GetBoxById(id);
            if (box == null) return NotFound();
            return Ok(box);
        }

        /// <summary>הוספת ארגז חדש</summary>
        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] Box box)
        {
            int newId = await _db.CreateBox(box);
            return CreatedAtAction(nameof(GetById), new { id = newId }, newId);
        }

        /// <summary>עדכון ארגז קיים</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Box box)
        {
            var existing = await _db.GetBoxById(id);
            if (existing == null) return NotFound();
            await _db.UpdateBox(id, box);
            return NoContent();
        }

        /// <summary>מחיקת ארגז</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _db.DeleteBox(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
