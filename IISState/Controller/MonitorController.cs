

using IISState.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IISState.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonitorController : ControllerBase
    {
        private readonly MonitorContext _context;

        public MonitorController(MonitorContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var items = _context.MonitorItem.Include(m => m.History.Where(x=>x.Delfalg==false)).ToList();
            return Ok(items);
        }

        [HttpPost]
        public IActionResult Post(MonitorItem item)
        {
            if (item == null)
            {
                return BadRequest("Item is null");
            }

            //item.Id = _context.MonitorItem.Any() ? _context.MonitorItem.Max(i => i.Id) + 1 : 1;
            item.LastChecked = DateTime.Now;
            item.IsAvailable = false;
            item.History = new List<HistoryEntry>(); // 初始化历史记录
            item.Uptime = 0; // 初始可用性百分比
            _context.MonitorItem.Add(item);
            _context.SaveChanges();

            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        
        // 删除 MonitorItem
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> DeleteMonitorItem(int id)
        {
            var item = await _context.MonitorItem.FindAsync(id);
            if (item == null)
            {
                return NotFound(); // 如果未找到，返回 404
            }

            _context.MonitorItem.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent(); // 删除成功，返回 204
        }
    }
}