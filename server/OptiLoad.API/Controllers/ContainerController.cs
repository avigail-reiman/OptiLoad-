using System;
using Microsoft.AspNetCore.Mvc;
using OptiLoad.Core.Models;
using OptiLoad.Data;

namespace OptiLoad.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ContainerController : ControllerBase
    {
        private readonly DatabaseService _db;

        public ContainerController(DatabaseService db)
        {
            _db = db;
        }

        [HttpGet("templates")]
        public async Task<ActionResult<IEnumerable<ContainerTemplate>>> GetTemplates()
        {
            var templates = await _db.GetAllContainerTemplates();
            return Ok(templates);
        }

        [HttpGet("templates/{id}")]
        public async Task<ActionResult<ContainerTemplate>> GetTemplateById(int id)
        {
            var template = await _db.GetContainerTemplateById(id);
            if (template == null) return NotFound();
            return Ok(template);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Container>>> GetAll()
        {
            var containers = await _db.GetAllContainers();
            return Ok(containers);
        }

        [HttpPost("templates")]
        public async Task<ActionResult<int>> CreateTemplate([FromBody] ContainerTemplate template)
        {
            template.CreatedAt = DateTime.UtcNow;
            int newId = await _db.CreateContainerTemplate(template);
            return CreatedAtAction(nameof(GetTemplateById), new { id = newId }, newId);
        }
    }
}
