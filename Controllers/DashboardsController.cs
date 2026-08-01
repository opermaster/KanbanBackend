using KanbanBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KanbanBackend.Controllers
{
    public record DashboardCreate(string dashboard_name);
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        public DashboardsController(DatabaseContext context) {
            _context = context;
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<ActionResult<DashboardDto>> CreateDashboard(DashboardCreate db, CancellationToken ct) {

            var userId = User.GetUserId();

            if (userId is null) 
                return Unauthorized();

            if (await _context.Dashboards.AnyAsync(_db => _db.UserId == userId && _db.Name == db.dashboard_name, ct))
                return Conflict("Dashboard with this name already exist!");

            Dashboard dashboard = new Dashboard() { Name = db.dashboard_name, UserId = (int)userId };
            _context.Dashboards.Add(dashboard);
            await _context.SaveChangesAsync(ct);

            return StatusCode(201, new DashboardDto() { Name = db.dashboard_name, Id = dashboard.Id });
        }

        [Authorize]
        [HttpGet("get_all")]
        public async Task<ActionResult<List<DashboardDto>>> GetAllDashboards(CancellationToken ct) {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            return await _context.Dashboards
                .AsNoTracking()
                .Where(_db => _db.UserId == userId)
                .Select(_db => new DashboardDto() { Name = _db.Name, Id = _db.Id })
                .ToListAsync(ct); 
        }
    }
}
