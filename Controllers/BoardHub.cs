using KanbanBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KanbanBackend.Controllers
{
    [Authorize]
    public class BoardHub : Hub
    {
        private readonly DatabaseContext _context;

        public BoardHub(DatabaseContext context) {
            _context = context;
        }

        private int? GetUserId() => Context.User?.GetUserId();

        public override async Task OnConnectedAsync() {
            var userId = GetUserId();
            if (userId is not null) {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            }
            await base.OnConnectedAsync();
        }

        public async Task Enter(int dashboardId) {
            var userId = GetUserId();
            if (userId is null) {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }
            bool hasAccess = await _context.Dashboards.AnyAsync(d => d.Id == dashboardId && d.UserId == userId)
                || await _context.BoardMembers.AnyAsync(m => m.BoardId == dashboardId && m.UserId == userId);

            if (!hasAccess) {
                await Clients.Caller.SendAsync("Error", "No access to this board");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"board-{dashboardId}");

            // var columns = await _context.Columns.Where(c => c.DashboardId == dashboardId)
            // await Clients.Caller.SendAsync("BoardState", columns);

            await Clients.Group($"board-{dashboardId}").SendAsync("UserJoined", userId);
        }

        public async Task Exit(int dashboardId) {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"board-{dashboardId}");
        }

        public async Task InviteUser(int dashboardId, string login) {
            var userId = GetUserId();
            if (userId is null) return;

            bool requesterHasAccess = await _context.Dashboards.AnyAsync(d => d.Id == dashboardId && d.UserId == userId)
                || await _context.BoardMembers.AnyAsync(m => m.BoardId == dashboardId && m.UserId == userId);

            if (!requesterHasAccess) {
                await Clients.Caller.SendAsync("Error", "You have no access to this board");
                return;
            }

            var invitedUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == login);
            if (invitedUser is null) {
                await Clients.Caller.SendAsync("Error", $"User '{login}' not found");
                return;
            }

            bool alreadyMember = await _context.BoardMembers
                .AnyAsync(m => m.BoardId == dashboardId && m.UserId == invitedUser.Id);
            if (alreadyMember) {
                await Clients.Caller.SendAsync("Error", "User is already a member of this board");
                return;
            }

            bool alreadyInvited = await _context.BoardInvites
                .AnyAsync(i => i.BoardId == dashboardId && i.InvitedUserId == invitedUser.Id && i.Status == InviteStatus.Pending);
            if (alreadyInvited) {
                await Clients.Caller.SendAsync("Error", "Invite already sent");
                return;
            }

            var invite = new BoardInvite {
                BoardId = dashboardId,
                InvitedUserId = invitedUser.Id,
                InvitedByUserId = userId.Value,
            };
            _context.BoardInvites.Add(invite);
            await _context.SaveChangesAsync();
            var board = await _context.Dashboards.FindAsync(dashboardId);
            await Clients.Group($"user-{invitedUser.Id}").SendAsync("InviteReceived", new {
                inviteId = invite.Id,
                boardId = dashboardId,
                boardName = board?.Name,
            });

            await Clients.Caller.SendAsync("InviteSent", invitedUser.Login);
        }

        public async Task RespondToInvite(int inviteId, bool accept) {
            var userId = GetUserId();
            if (userId is null) return;

            var invite = await _context.BoardInvites.FirstOrDefaultAsync(i => i.Id == inviteId);
            if (invite is null || invite.InvitedUserId != userId || invite.Status != InviteStatus.Pending) {
                await Clients.Caller.SendAsync("Error", "Invite not found or already handled");
                return;
            }

            invite.Status = accept ? InviteStatus.Accepted : InviteStatus.Declined;

            if (accept) {
                _context.BoardMembers.Add(new BoardMember {
                    BoardId = invite.BoardId,
                    UserId = userId.Value,
                    Role = BoardRole.Editor,
                });
            }

            await _context.SaveChangesAsync();

            await Clients.Caller.SendAsync("InviteResolved", new { inviteId, accepted = accept });
            // Enter(invite.BoardId), accept == true?
        }
    }
}
