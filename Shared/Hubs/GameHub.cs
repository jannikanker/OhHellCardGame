using CardGames.Server.Services;
using CardGames.Shared;
using CardGames.Shared.Hubs;
using CardGames.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;


namespace CardGames.Server.Hubs
{

    public class GameHub : Hub
    {
        private GameService _gameService;
        private IStringLocalizer<GameHubStrings> _localizer;
        private readonly ILogger _logger;

        public GameHub(GameService gameService, 
                       IStringLocalizer<GameHubStrings> localizer,
                       ILogger<GameHub> logger)
        {
            _gameService = gameService;
            _localizer = localizer;
            _logger = logger;
        }

        public async Task GetAvailablePlayers(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var availablePlayers = game.Players.Where(p => p.SignedIn == false).Select(p => p.Id).ToArray();

            await Clients.Caller.SendAsync("ReturnAvailablePlayers", availablePlayers);
        }

        public async Task NewGame(string name, string gameAdmin, string userEmail, int nrPlayers)
        {
            var game = _gameService.NewGame(name, nrPlayers);
            game.GameAdmin = gameAdmin;
            var games = _gameService.GetPlayerGames(userEmail);
            await Clients.Caller.SendAsync("NewGameCreated", games);
            _logger.LogInformation($"New Game created with name: {name}.");
        }

        public async Task JoinGame(string gameId, string selectedPlayer, string name)
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                if (!string.IsNullOrEmpty(selectedPlayer))
                {
                    game.Players.Where(p => p.Id == selectedPlayer).First().SignedIn = true;
                    game.Players.Where(p => p.Id == selectedPlayer).First().Name = name;
                    if (!string.IsNullOrEmpty(game.Connections[GetPlayerId(selectedPlayer)]))
                    {
                        await Groups.RemoveFromGroupAsync(game.Connections[GetPlayerId(selectedPlayer)], gameId);
                    }
                    game.Connections[GetPlayerId(selectedPlayer)] = Context.ConnectionId;
                    await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

                    var notsignedinplayer = "";
                    foreach (var player in game.Players.Where(p => p.SignedIn == false))
                    {
                        notsignedinplayer += player.Id + " ";
                    }

                    if (game.GameStarted != true)
                    {
                        game.Status = game.AllPlayersSignedIn ? _localizer["GameAdminStart"] : _localizer["WaitingSignIn", notsignedinplayer];
                    }

                    await Clients.Group(gameId).SendAsync("JoinedGame", game);
                    _logger.LogInformation($"Player {name} joined Game {gameId}.");
                }
            }
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            game.Status = _localizer["WaitToShuffle", game.CurrentPlayerObj.FirstName];
            await Clients.Group(gameId).SendAsync("GameStarted", game);
            _logger.LogInformation($"Game {gameId} started.");
        }

        public async Task ResetGame(string gameId, string userEmail)
        {

            var game = _gameService.ResetGame(gameId);
            var games = _gameService.GetPlayerGames(userEmail);
            game.Status = _localizer["GameResetted", game.CurrentPlayerObj.FirstName];
            await Clients.Caller.SendAsync("GameResetted", games);
            //await Clients.Group(gameId).SendAsync("GameResetted", game);
        }

        public async Task RemoveGame(string gameId, string userEmail)
        {
            _gameService.RemoveGame(gameId, userEmail);
            var games = _gameService.GetPlayerGames(userEmail);
            await Clients.Caller.SendAsync("GameRemoved", games);
        }

        public async Task NewGameSet(string gameId, string userEmail)
        {
            var game = _gameService.NewGameSet(gameId);
            var games = _gameService.GetPlayerGames(userEmail);
            game.Status = _localizer["NewGameSet"];
            //await Clients.Caller.SendAsync("NewGameSet", games);
            await Clients.Group(gameId).SendAsync("NewGameSet", game);
        }

        public async Task GetRunningGames(string userEmail)
        {
            var games = _gameService.GetPlayerGames(userEmail);
            await Clients.Caller.SendAsync("ReturnRunningGames", games);
        }

        public async Task SaveGamePlayer(GamePlayer gamePlayer, string userEmail)
        {
            var game = _gameService.GetGame(gamePlayer.GameId);
            game.Players[GetPlayerId(gamePlayer.Player)].Email = gamePlayer.Email;
            game.Players[GetPlayerId(gamePlayer.Player)].IsGameController = gamePlayer.IsGameAdmin;
            var games = _gameService.GetPlayerGames(userEmail);
            await Clients.Caller.SendAsync("SavedGamePlayer", games);
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

            if (game.CurrentPlayer < game.NrPlayers-1)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 0;

            game.Status = _localizer["UserBetted", game.CurrentPlayerObj.FirstName, game.Betted ? _localizer["Play"] : _localizer["Bet"]];
            await Clients.Group(gameId).SendAsync("BetPlaced", game);
        }

        public async Task PlayCard(string gameId, string player, Card card)
        {
            var game = _gameService.GetGame(gameId);

            var _selectedplayer = GetPlayerId(player);

            #region getwinner
            //var playColor = game.Rounds[game.CurrentRound].PlayedCards[game.PlayerToStart].Card.Colour;
            //var playValue = game.Rounds[game.CurrentRound].PlayedCards[game.PlayerToStart].Card.Value;

            //for (int i = 1; i < 4; i++)
            //{
            //    if(game.Rounds[game.CurrentRound].PlayedCards[i] != null)
            //    {
            //        if(game.Rounds[game.CurrentRound].PlayedCards[i].Card.Colour == playColor)
            //        {
            //            if(game.Rounds[game.CurrentRound].PlayedCards[i].Card.Value > playValue)
            //            {

            //            }
            //        }
            //        else
            //        {
            //            if(game.Rounds[game.CurrentRound].PlayedCards[i].Card.Colour == game.PlayingCard.Colour)
            //            {

            //            }
            //        }
            //    }
            //}
            #endregion

            game.Rounds[game.CurrentRound].PlayedCards[_selectedplayer] = new PlayedCard { PlayerId = player, Card = card };

            if (game.CurrentPlayer < game.NrPlayers-1)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 0;

            var card2Remove = game.Players[_selectedplayer].Cards.Where(c => c.Colour == card.Colour && c.Value == card.Value).FirstOrDefault();
            if (card2Remove != null)
            {
                game.Players[_selectedplayer].Cards.Remove(card2Remove);
            }

            if (game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.Card == null).Count() == 0)
            {
                game.ChooseWinner = true;
            }

            game.Status = _localizer["WaitingPlayCard", game.CurrentPlayerObj.FirstName];
            if (game.ChooseWinner)
            {
                game.Status = _localizer["WaitingChooseWinner"];
            }
            foreach (var pl in game.Players)
            {
                var pId = GetPlayerId(pl.Id);
                await Clients.Client(game.Connections[pId]).SendAsync("PlayedCard", game.Players[pId].Cards, game);
            }
        }

        private static int GetPlayerId(string player)
        {
            var pId = 0;
            pId = Convert.ToInt32(player.Substring(1, 1))-1;
            return pId;
        }

        public async Task Shuffle(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.Shuffle();
            game.Status = _localizer["WaitingToBet", game.CurrentPlayerObj.FirstName];
            game.ChooseWinner = false;
            foreach (var pl in game.Players)
            {
                var pId = GetPlayerId(pl.Id);
                await Clients.Client(game.Connections[pId]).SendAsync("Shuffled", game.Players[pId].Cards, game);
            }
        }

        public async Task NextRound(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.NextRound();
            if (game.GameOver)
            {
                game.Status = string.Format("GameOver !!!");
            }
            else
            {
                game.Status = _localizer["WaitToShuffle", game.PlayerToStartObj.FirstName];
            }
            await Clients.Group(gameId).SendAsync("StartNextRound", game);
        }

        public async Task CleanTable(string gameId)
        {
            var game = _gameService.GetGame(gameId);

            var winningPlayer = game
                .Rounds[game.CurrentRound]
                .PlayedCards.Where(c => c.Winner == true)
                .FirstOrDefault()
                .PlayerId;
            game.Rounds[game.CurrentRound].Wins[GetPlayerId(winningPlayer)]++;
            game.ChooseWinner = false;

            game.CurrentPlayer = GetPlayerId(winningPlayer);
            game.PlayerToStart = GetPlayerId(winningPlayer);

            if (game.Players[0].Cards.Count() == 0)
            {
                game.RoundReady = true;
            }
            game.CleanTable = false;
            game.ChooseWinner = false;
            game.SetNewPlayingCards();
            if (game.RoundReady)
            {
                game.Status = _localizer["WaitNextRound"];
            }
            else
            {
                game.Status = _localizer["WaitingPlayCard", game.CurrentPlayerObj.FirstName];
            }
            await Clients.Group(gameId).SendAsync("CleanedTable", game);
        }

        public async Task RoundWinner(string gameId, PlayedCard winningCard)
        {
            var game = _gameService.GetGame(gameId);
            foreach(var card in game.Rounds[game.CurrentRound].PlayedCards)
            {
                card.Winner = false;
            }

            game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.PlayerId == winningCard.PlayerId).FirstOrDefault().Winner = true;
            game.CleanTable = true;
            game.Status = _localizer["PlayerWon", game.Players.Where(p => p.Id == winningCard.PlayerId).First().FirstName];
            await Clients.Group(gameId).SendAsync("WinnerRegistered", game);
        }
    }
}
