using FactoryApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryApi.Controllers
{
    [ApiController]
    [Route("api/history")]
    public class HistoryController : ControllerBase
    {
        private readonly FactoryDbContext _db;

        public HistoryController(FactoryDbContext db)
        {
            _db = db;
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents(
            [FromQuery] int? cameraId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _db.ProductionEvents.AsQueryable();

            if (cameraId.HasValue)
                query = query.Where(x => x.CameraId == cameraId.Value);

            if (from.HasValue)
                query = query.Where(x => x.EventTime >= from.Value);

            if (to.HasValue)
                query = query.Where(x => x.EventTime <= to.Value);

            var items = await query
                .OrderByDescending(x => x.EventTime)
                .Select(x => new
                {
                    Id = x.EventId,
                    x.CameraId,
                    CameraName = "cam" + x.CameraId,
                    x.ProductName,
                    x.EventTime,
                    x.ProductionCount,
                    ImagePath = x.SnapshotPath,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(items);
        }
    }
}