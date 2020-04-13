using BlazorSignalRApp.Server.Services;
using BlazorSignalRApp.Shared;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSignalRApp.Server.Hubs
{
    public class GameHub : Hub
    {
        private GameService _gameService;
        public GameHub(GameService gameService)
        {
            _gameService = gameService;
        }

        public async Task AddToGroup(string gameId, string playerId)
        {
            var game = _gameService.GetGame(gameId);
            game.Players.Where(p => p.Name == playerId).First().SignIn = true;
            await Groups.AddToGroupAsync(Context.ConnectionId, playerId);

            await Clients.All.SendAsync("PlayerSelected", game, playerId);
        }

        public async Task GetAvailablePlayers(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var availablePlayers = game.Players.Where(p => p.SignIn == false).Select(p => p.Name).ToArray();

            await Clients.Caller.SendAsync("ReturnAvailablePlayers", availablePlayers);
        }

        public async Task NewGame(string name)
        {
            var game = _gameService.NewGame(name);
            await Clients.Caller.SendAsync("NewGameCreated", game);
        }

        public async Task JoinGame(string gameId, string selectedPlayer)
        {
            var game = _gameService.GetGame(gameId);
            if(!string.IsNullOrEmpty(selectedPlayer))
            {
                game.Players.Where(p => p.Name == selectedPlayer).First().SignIn = true;
                await Groups.AddToGroupAsync(Context.ConnectionId, selectedPlayer);
            }
            await Clients.All.SendAsync("JoinedGame", game);
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            await Clients.All.SendAsync("GameStarted", game);
        }

        public async Task ResetGame(string gameId)
        {
            var game = _gameService.ResetGame(gameId);
            await Clients.All.SendAsync("GameResetted", game);
        }

        public async Task GetRunningGames()
        {
            var games = _gameService.GetGames();
            await Clients.All.SendAsync("ReturnRunningGames", games);
        }

        public async Task RoundWinner(string gameId, PlayedCard winningCard)
        {
            var game = _gameService.GetGame(gameId);
            game.ChooseWinner = false;
            game.CleanTable = true;
            game.CurrentPlayer = GetPlayerId(winningCard.PlayerName)+1;
            game.PlayerToStart = GetPlayerId(winningCard.PlayerName) + 1;
            await Clients.All.SendAsync("WinnerRegistered", game);
        }

        public async Task PlayCard(string gameId, string player, Card card)
        {
            var game = _gameService.GetGame(gameId);
            var _selectedplayer = 0;
            _selectedplayer = GetPlayerId(player);
            game.PlayedCards[_selectedplayer] = new PlayedCard { PlayerName = player, Card = card };

            if (game.CurrentPlayer < 4)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 1;

            var card2Remove = game.Players[_selectedplayer].Cards.Where(c => c.Colour == card.Colour && c.Value == card.Value).FirstOrDefault();
            if (card2Remove != null)
            {
                game.Players[_selectedplayer].Cards.Remove(card2Remove);
            }

            if (game.PlayedCards.Where(c => c.Card == null).Count() == 0)
            {
                game.ChooseWinner = true;
            }

            await Clients.Group("P1").SendAsync("PlayedCard", game.Players[0].Cards, game);
            await Clients.Group("P2").SendAsync("PlayedCard", game.Players[1].Cards, game);
            await Clients.Group("P3").SendAsync("PlayedCard", game.Players[2].Cards, game);
            await Clients.Group("P4").SendAsync("PlayedCard", game.Players[3].Cards, game);
        }

        private static int GetPlayerId(string player)
        {
            var pId = 0;
            switch (player)
            {
                case "P1":
                    pId = 0;
                    break;
                case "P2":
                    pId = 1;
                    break;
                case "P3":
                    pId = 2;
                    break;
                case "P4":
                    pId = 3;
                    break;
            }
            return pId;
        }

        public async Task Shuffle(string gameId, int nrCards)
        {
            var game = _gameService.GetGame(gameId);
            game.Shuffle(nrCards);
            game.ChooseWinner = false;
            await Clients.Groups("P1").SendAsync("Shuffled", game.Players[0].Cards, game);
            await Clients.Groups("P2").SendAsync("Shuffled", game.Players[1].Cards, game);
            await Clients.Groups("P3").SendAsync("Shuffled", game.Players[2].Cards, game);
            await Clients.Groups("P4").SendAsync("Shuffled", game.Players[3].Cards, game);
        }

        public async Task NextRound(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.NextRound();
            await Clients.All.SendAsync("StartNextRound", game);
        }

        public async Task CleanTable(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            if (game.Players[0].Cards.Count() == 0)
            {
                game.RoundReady = true;
            }
            game.CleanTable = false;
            game.SetNewPlayingCards();
            await Clients.All.SendAsync("CleanedTable", game);
        }
    }
}
