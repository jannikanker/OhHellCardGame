using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorSignalRApp.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalRApp.Server.Hubs
{
    public class ChatHub : Hub
    {
        private TeamService _teamService;
        public ChatHub(TeamService groupService)
        {
            _teamService = groupService;
        }

        public async Task GetGroups()
        {
            var groups = _teamService.GetAllTeams();
            await Clients.All.SendAsync("ReceiveGroups", groups);
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        public async Task SendEmoji(string teamId, string user, string emoji)
        {
            _teamService.SetMember(teamId, user, emoji);
            await Clients.Groups(teamId).SendAsync("ReceiveEmoji", _teamService.GetTeam(teamId).Members);
        }

        public async Task AddToGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            await Clients.All.SendAsync("ReceiveMessage", "System", $"{Context.ConnectionId} has joined the group {groupName}.");
        }
    }
}