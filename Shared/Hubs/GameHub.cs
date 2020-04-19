using BlazorSignalRApp.Server.Services;
using BlazorSignalRApp.Shared;
using Microsoft.AspNetCore.Builder;
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
            if (game != null)
            {
                if (!string.IsNullOrEmpty(selectedPlayer))
                {
                    game.Players.Where(p => p.Name == selectedPlayer).First().SignIn = true;
                    if(!string.IsNullOrEmpty(game.Connections[GetPlayerId(selectedPlayer)]))
                    {
                        await Groups.RemoveFromGroupAsync(game.Connections[GetPlayerId(selectedPlayer)], gameId);
                    }
                    game.Connections[GetPlayerId(selectedPlayer)] = Context.ConnectionId;
                    await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
                }
            }

            if (game.GameStarted != true)
            {
                game.Status = game.AllPlayersSignedIn ? "Waiting for P1 to start the game" : "Waiting for others to sign in";
            }
            await Clients.Group(gameId).SendAsync("JoinedGame", game);
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            game.Status = string.Format("Waiting for P{0} to shuffle", game.CurrentPlayer);
            await Clients.Group(gameId).SendAsync("GameStarted", game);
        }

        public async Task ResetGame(string gameId)
        {
            var game = _gameService.ResetGame(gameId);
            game.Status = string.Format("Game Resetted. Waiting for P{0} to shuffle", game.CurrentPlayer);
            await Clients.Group(gameId).SendAsync("GameResetted", game);
        }

        public async Task GetRunningGames()
        {
            var games = _gameService.GetGames();
            await Clients.Caller.SendAsync("ReturnRunningGames", games);
        }

        public async Task PlaceBet(string gameId, string selectedPlayer, string placedBet)
        {
            var game = _gameService.GetGame(gameId);
            game.Rounds[game.CurrentRound].Bets[GetPlayerId(selectedPlayer)] = Convert.ToInt32(placedBet);
            if (game.Rounds[game.CurrentRound].AllBetsPlaced)
            {
                game.Playing = true;
                game.Betted = true;
            }

            if (game.CurrentPlayer < 4)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 1;

            game.Status = string.Format("Waiting for P{0} to {1}", game.CurrentPlayer, game.Betted ? "Play" : "Bet");
            await Clients.Group(gameId).SendAsync("BetPlaced", game);
        }

        public async Task PlayCard(string gameId, string player, Card card)
        {
            var game = _gameService.GetGame(gameId);
            var _selectedplayer = 0;
            _selectedplayer = GetPlayerId(player);
            game.Rounds[game.CurrentRound].PlayedCards[_selectedplayer] = new PlayedCard { PlayerName = player, Card = card };

            if (game.CurrentPlayer < 4)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 1;

            var card2Remove = game.Players[_selectedplayer].Cards.Where(c => c.Colour == card.Colour && c.Value == card.Value).FirstOrDefault();
            if (card2Remove != null)
            {
                game.Players[_selectedplayer].Cards.Remove(card2Remove);
            }

            if (game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.Card == null).Count() == 0)
            {
                game.ChooseWinner = true;
            }

            game.Status = string.Format("Waiting for P{0} to play card", game.CurrentPlayer);
            if (game.ChooseWinner)
            {
                game.Status = string.Format("Waiting to choose winner", game.CurrentPlayer);
            }
            await Clients.Client(game.Connections[0]).SendAsync("PlayedCard", game.Players[0].Cards, game);
            await Clients.Client(game.Connections[1]).SendAsync("PlayedCard", game.Players[1].Cards, game);
            await Clients.Client(game.Connections[2]).SendAsync("PlayedCard", game.Players[2].Cards, game);
            await Clients.Client(game.Connections[3]).SendAsync("PlayedCard", game.Players[3].Cards, game);
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

        public async Task Shuffle(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.Shuffle();
            game.Status = string.Format("Waiting for P{0} to Bet", game.CurrentPlayer);
            game.ChooseWinner = false;
            await Clients.Client(game.Connections[0]).SendAsync("Shuffled", game.Players[0].Cards, game);
            await Clients.Client(game.Connections[1]).SendAsync("Shuffled", game.Players[1].Cards, game);
            await Clients.Client(game.Connections[2]).SendAsync("Shuffled", game.Players[2].Cards, game);
            await Clients.Client(game.Connections[3]).SendAsync("Shuffled", game.Players[3].Cards, game);
        }

        public async Task NextRound(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.NextRound();
            game.Status = string.Format("Waiting for P{0} to shuffle", game.PlayerToStart);
            await Clients.All.SendAsync("StartNextRound", game);
        }

        public async Task CleanTable(string gameId)
        {
            var game = _gameService.GetGame(gameId);

            var winningPlayer = game
                .Rounds[game.CurrentRound]
                .PlayedCards.Where(c => c.Winner == true)
                .FirstOrDefault()
                .PlayerName;
            game.Rounds[game.CurrentRound].Wins[GetPlayerId(winningPlayer)]++;
            game.ChooseWinner = false;

            game.CurrentPlayer = GetPlayerId(winningPlayer) + 1;
            game.PlayerToStart = GetPlayerId(winningPlayer) + 1;

            if (game.Players[0].Cards.Count() == 0)
            {
                game.RoundReady = true;
            }
            game.CleanTable = false;
            game.ChooseWinner = false;
            game.SetNewPlayingCards();
            if (game.RoundReady)
            {
                game.Status = "Waiting to start next round";
            }
            else
            {
                game.Status = string.Format("Waiting for P{0} to play card", game.CurrentPlayer);
            }
            await Clients.All.SendAsync("CleanedTable", game);
        }

        public async Task RoundWinner(string gameId, PlayedCard winningCard)
        {
            var game = _gameService.GetGame(gameId);
            foreach(var card in game.Rounds[game.CurrentRound].PlayedCards)
            {
                card.Winner = false;
            }

            game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.PlayerName == winningCard.PlayerName).FirstOrDefault().Winner = true;
            game.CleanTable = true;
            game.Status = string.Format("{0} has won. Clear table", winningCard.PlayerName);
            await Clients.Group(gameId).SendAsync("WinnerRegistered", game);
        }
    }
}
