using KanbanBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KanbanBackend.Controllers
{
    public record InviteDto(int InviteId, int BoardId, string BoardName);

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvitesController : ControllerBase
    {
        private readonly DatabaseContext _context;
        public InvitesController(DatabaseContext context) {
            _context = context;
        }

        [HttpGet("pending")]
        public async Task<ActionResult<List<InviteDto>>> GetPendingInvites() {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var invites = await _context.BoardInvites
                .AsNoTracking()
                .Where(i => i.InvitedUserId == userId && i.Status == InviteStatus.Pending)
                .Select(i => new InviteDto(i.Id, i.BoardId, i.Board.Name))
                .ToListAsync();

            return invites;
        }
    }
}
