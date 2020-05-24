﻿using BlazorSignalRApp.Server.Services;
using BlazorSignalRApp.Shared;
using BlazorSignalRApp.Shared.Hubs;
using BlazorSignalRApp.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;


namespace BlazorSignalRApp.Server.Hubs
{

    public class GameHub : Hub
    {
        private GameService _gameService;
        private IStringLocalizer<GameHubStrings> _localizer;
        public GameHub(GameService gameService, IStringLocalizer<GameHubStrings> localizer)
        {
            _gameService = gameService;
            _localizer = localizer;
        }

        public async Task GetAvailablePlayers(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var availablePlayers = game.Players.Where(p => p.SignedIn == false).Select(p => p.Id).ToArray();

            await Clients.Caller.SendAsync("ReturnAvailablePlayers", availablePlayers);
        }

        public async Task NewGame(string name, string gameAdmin, string userEmail)
        {
            var game = _gameService.NewGame(name);
            game.GameAdmin = gameAdmin;
            var games = _gameService.GetGames(userEmail);
            await Clients.Caller.SendAsync("NewGameCreated", games);
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
                }
            }
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            game.Status = _localizer["WaitToShuffle", game.CurrentPlayerObj.FirstName];
            await Clients.Group(gameId).SendAsync("GameStarted", game);
        }

        public async Task ResetGame(string gameId, string userEmail)
        {

            var game = _gameService.ResetGame(gameId);
            var games = _gameService.GetGames(userEmail);
            game.Status = _localizer["GameResetted", game.CurrentPlayerObj.FirstName];


            await Clients.Group(gameId).SendAsync("GameResetted", games);
        }

        public async Task RemoveGame(string gameId, string userEmail)
        {
            _gameService.RemoveGame(gameId, userEmail);
            var games = _gameService.GetGames(userEmail);
            await Clients.Caller.SendAsync("GameRemoved", games);
        }

        public async Task GetRunningGames(string userEmail)
        {
            var games = _gameService.GetGames(userEmail);
            await Clients.Caller.SendAsync("ReturnRunningGames", games);
        }

        public async Task SaveGamePlayer(GamePlayer gamePlayer, string userEmail)
        {
            var game = _gameService.GetGame(gamePlayer.GameId);
            game.Players[GetPlayerId(gamePlayer.Player)].Email = gamePlayer.Email;
            game.Players[GetPlayerId(gamePlayer.Player)].IsGameController = gamePlayer.IsGameAdmin;
            var games = _gameService.GetGames(userEmail);
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

            if (game.CurrentPlayer < 3)
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

            if (game.CurrentPlayer < 3)
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
            game.Status = _localizer["WaitingToBet", game.CurrentPlayerObj.FirstName];
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