using KanbanBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KanbanBackend.Controllers
{
    public record CardDto(int Id, string Name, int Order, bool Done);
    public record ColumnDto(int Id, string Name, int Order, List<CardDto> Cards);

    [Authorize]
    public class BoardHub : Hub
    {
        private readonly DatabaseContext _context;

        public BoardHub(DatabaseContext context) {
            _context = context;
        }

        private async Task<List<ColumnDto>> LoadBoardState(int dashboardId) {
            return await _context.Columns
                .AsNoTracking()
                .Where(c => c.DashboardId == dashboardId)
                .OrderBy(c => c.Order)
                .Select(c => new ColumnDto(
                    c.Id,
                    c.Name,
                    c.Order,
                    c.Cards
                        .OrderBy(card => card.Order)
                        .Select(card => new CardDto(card.Id, card.Name, card.Order, card.Done))
                        .ToList()
                ))
                .ToListAsync();
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

            var state = await LoadBoardState(dashboardId);
            await Clients.Caller.SendAsync("BoardState", state);

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
        public async Task BoardState(int dashboardId, List<ColumnDto> columns) {
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

            var existingColumns = await _context.Columns
                .Include(c => c.Cards)
                .Where(c => c.DashboardId == dashboardId)
                .ToListAsync();

            var incomingColumnIds = columns.Where(c => c.Id > 0).Select(c => c.Id).ToHashSet();
            var columnsToRemove = existingColumns.Where(c => !incomingColumnIds.Contains(c.Id)).ToList();
            _context.Columns.RemoveRange(columnsToRemove);

            int columnOrder = 0;
            foreach (var colDto in columns) {
                Column column;
                if (colDto.Id > 0) {
                    var found = existingColumns.FirstOrDefault(c => c.Id == colDto.Id);
                    if (found is null) { columnOrder++; continue; } 
                    column = found;
                    column.Name = colDto.Name;
                    column.Order = columnOrder;
                }
                else {
                    column = new Column { Name = colDto.Name, Order = columnOrder, DashboardId = dashboardId };
                    _context.Columns.Add(column);
                }

                var incomingCardIds = colDto.Cards.Where(c => c.Id > 0).Select(c => c.Id).ToHashSet();
                var existingCards = column.Cards.ToList();
                var cardsToRemove = existingCards.Where(c => !incomingCardIds.Contains(c.Id)).ToList();
                _context.Cards.RemoveRange(cardsToRemove);

                int cardOrder = 0;
                foreach (var cardDto in colDto.Cards) {
                    if (cardDto.Id > 0) {
                        var found = existingCards.FirstOrDefault(c => c.Id == cardDto.Id);
                        if (found is null) { cardOrder++; continue; }
                        found.Name = cardDto.Name;
                        found.Order = cardOrder;
                        found.Done = cardDto.Done;
                    }
                    else {
                        column.Cards.Add(new Card { Name = cardDto.Name, Order = cardOrder, Done = cardDto.Done });
                    }
                    cardOrder++;
                }

                columnOrder++;
            }

            await _context.SaveChangesAsync();
            await BroadcastBoardState(dashboardId);
        }
        private async Task BroadcastBoardState(int dashboardId) {
            var state = await LoadBoardState(dashboardId);
            await Clients.Group($"board-{dashboardId}").SendAsync("BoardState", state);
        }
    }
}
