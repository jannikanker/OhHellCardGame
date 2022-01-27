using CardGames.Shared.Models;
using CardGamesHub.Server.Services;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CardGamesHub.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GameHub : Hub
    {
        private readonly ILogger _logger;
        private IGameService _gameService;
        private IStringLocalizer<GameHubStrings> _localizer;
        private GameSettings _settings;
        private TelemetryClient _telemetry;
        private string _viewUser = "viewer";
        public GameHub(IGameService gameService,
                       IStringLocalizer<GameHubStrings> localizer,
                       ILogger<GameHub> logger,
                       IOptions<GameSettings> settings,
                       TelemetryClient telemetry)
        {
            _gameService = gameService;
            _localizer = localizer;
            _logger = logger;
            _settings = settings.Value;
            _telemetry = telemetry;
        }

        public async Task CleanTable(string gameId)
        {
            var game = _gameService.GetGame(gameId);

            var winningPlayer = game
                .Rounds[game.CurrentRound]
                .PlayedCards.FirstOrDefault(c => c.Winner == true)
                .PlayerId;
            game.Rounds[game.CurrentRound].Wins[Player.GetPlayerId(winningPlayer)]++;
            game.ChooseWinner = false;

            game.CurrentPlayer = Player.GetPlayerId(winningPlayer);
            game.PlayerToStart = Player.GetPlayerId(winningPlayer);

            if (!game.Players[0].Cards.Any())
            {
                game.RoundReady = true;
            }
            game.CleanTable = false;
            game.ChooseWinner = false;
            game.Rounds[game.CurrentRound].PlayHistory.Add(game.Rounds[game.CurrentRound].PlayedCards);
            game.Rounds[game.CurrentRound].Trump = game.PlayingCard;
            game.SetNewPlayingCards();
            if (game.RoundReady)
            {
                game.Status = _localizer["WaitNextRound"];
            }
            else
            {
                game.Status = _localizer["WaitingPlayCard", game.CurrentPlayerObj.FirstName];
            }

            await _gameService.SaveGame(game);
            await Clients.Group(gameId).SendAsync("CleanedTable", game);
        }

        public async Task GetAvailablePlayers(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var availablePlayers = game.Players.Where(p => p.SignedIn == false).Select(p => p.Id).ToArray();

            await Clients.Caller.SendAsync("ReturnAvailablePlayers", availablePlayers);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task GetGameSettings(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var json = JsonConvert.SerializeObject(game);
            await Clients.Caller.SendAsync("GameSettings", json);
            //await Clients.Group(gameId).SendAsync("GameResetted", game);
        }

        public async Task GetRunningGames()
        {
            _logger.LogInformation($"Getting running Games.");
            var gameRegistries = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("ReturnRunningGames", gameRegistries);
        }

        public async Task JoinGame(string gameId, string selectedPlayer, string name)
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                if (!string.IsNullOrEmpty(selectedPlayer))
                {
                    if (GetUser() == game.Players.First(p => p.Id == selectedPlayer).Email)
                    {
                        game.Players.First(p => p.Id == selectedPlayer).SignedIn = true;
                        game.Players.First(p => p.Id == selectedPlayer).Name = name;
                        if (!string.IsNullOrEmpty(game.PlayerConnections[Player.GetPlayerId(selectedPlayer)]))
                        {
                            await Groups.RemoveFromGroupAsync(game.PlayerConnections[Player.GetPlayerId(selectedPlayer)], gameId);
                        }
                        game.PlayerConnections[Player.GetPlayerId(selectedPlayer)] = Context.ConnectionId;
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

                        var gameScores = await _gameService.GetTopScores();
                        await _gameService.SaveGame(game);
                        await Clients.Group(gameId).SendAsync("JoinedGame", game, gameScores);
                        _logger.LogInformation($"Player {name} joined Game {gameId}.");
                    }
                    else
                    {
                        _logger.LogInformation($"Player {name} wants to join game {gameId} as {selectedPlayer}. Not Allowed");
                        await Clients.Caller.SendAsync("NotAllowToJoinGame");
                    }
                }
            }
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task NewGame(string gameRegistryId)
        {
            _telemetry.TrackEvent($"New Game for {gameRegistryId}");
            var gameRegistry = await _gameService.GetGameRegistryById(gameRegistryId);
            var game = _gameService.NewGame(gameRegistry);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("NewGameCreated", games);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task NewGameRegistry(string name, string gameAdmin, int nrPlayers)
        {
            var game = _gameService.NewGameRegistry(name, nrPlayers);
            var gameRegistries = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("NewGameRegistryCreated", gameRegistries);
            _logger.LogInformation($"New GameRegistry created with name: {name}.");
        }


        public async Task NextRound(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.NextRound();
            if (game.GameOver)
            {
                game.Status = string.Format("GameOver !!!");
                await _gameService.SaveGamePersistent(game);
            }
            else
            {
                game.Status = _localizer["WaitToShuffle", game.PlayerToStartObj.FirstName];
            }

            await _gameService.SaveGame(game);

            await Clients.Group(gameId).SendAsync("StartNextRound", game);
        }

        public async Task PlaceBet(string gameId, string selectedPlayer, string placedBet)
        {
            var game = _gameService.GetGame(gameId);
            game.Rounds[game.CurrentRound].Bets[Player.GetPlayerId(selectedPlayer)] = Convert.ToInt32(placedBet);
            if (game.Rounds[game.CurrentRound].AllBetsPlaced)
            {
                game.Playing = true;
                game.Betted = true;
            }

            if (game.CurrentPlayer < game.NrPlayers - 1)
                game.CurrentPlayer++;
            else
                game.CurrentPlayer = 0;

            game.Status = _localizer["UserBetted", game.CurrentPlayerObj.FirstName, game.Betted ? _localizer["Play"] : _localizer["Bet"]];

            await _gameService.SaveGame(game);
            await Clients.Group(gameId).SendAsync("BetPlaced", game);
        }

        public async Task PlayCard(string gameId, string player, Card card)
        {
            var game = _gameService.GetGame(gameId);
            var selectedplayer = Player.GetPlayerId(player);
            game.ArchivePlayCard(player, card, selectedplayer);
            game.SetNextPlayer();
            game.RemovePlayedCardFromPlayer(card, selectedplayer);

            if (game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.Card == null).Count() == 0)
            {
                game.FindWinningCard();
                game.CleanTable = true;
                game.ChooseWinner = true;
            }

            game.Status = _localizer["WaitingPlayCard", game.CurrentPlayerObj.FirstName];
            if (game.ChooseWinner)
            {
                game.Status = _localizer["WaitingChooseWinner"];
            }

            await _gameService.SaveGame(game);

            foreach (var pl in game.Players)
            {
                var pId = Player.GetPlayerId(pl.Id);
                await Clients.Client(game.PlayerConnections[pId]).SendAsync("PlayedCard", game.Players[pId].Cards, game);
            }
            foreach (var connection in game.ViewerConnections)
            {
                await Clients.Client(connection).SendAsync("PlayedCard", new System.Collections.Generic.List<Card>(), game);
            }
        }

        public async Task PlayRandomCard(string gameId, string player)
        {
            var game = _gameService.GetGame(gameId);
            var _selectedplayer = Player.GetPlayerId(player);
            var card = game.Players[_selectedplayer].Cards.First();
            await PlayCard(gameId, player, card);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task RemoveGame(string gameRegistryId)
        {
            var gameRegistry = await _gameService.GetGameRegistryById(gameRegistryId);
            _gameService.RemoveGame(gameRegistry);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("GameRemoved", games);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task ShufflePlayers(string gameRegistryId)
        {
            var gameRegistry = await _gameService.GetGameRegistryById(gameRegistryId);
            _gameService.ShufflePlayers(gameRegistry);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("PlayersShuffled", games);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task RemoveGameRegistry(string gameId)
        {
            //_gameService.RemoveGame(gameId);
            await _gameService.RemoveGameRegistryPersitent(gameId);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("GameRegistryRemoved", games);
        }



        [Authorize(Policy = "IsAdmin")]
        public async Task ResetCurrentRound(string gameId)
        {
            var game = _gameService.ResetCurrentRound(gameId);
            var games = _gameService.GetPlayerGames();
            game.Status = _localizer["CurrentRoundResetted", game.CurrentPlayerObj.FirstName];
            await _gameService.SaveGame(game);

            await Clients.Caller.SendAsync("GameResetted", games);
            await Clients.Group(gameId).SendAsync("GameResetted", game);
        }

        public async Task ResetGame(string gameId)
        {
            if (_gameService.IsUserGameController(gameId) || _gameService.IsUserSystemAdmin())
            {
                var game = _gameService.ResetGame(gameId);
                var games = await _gameService.GetGameRegistries();
                game.Status = _localizer["GameResetted", game.CurrentPlayerObj.FirstName];
                await _gameService.SaveGame(game);

                await Clients.Caller.SendAsync("GameResetted", games);
                await Clients.Group(gameId).SendAsync("GameResetted", game);
            }
        }
        public async Task RoundWinner(string gameId, string winningPlayer)
        {
            var game = _gameService.GetGame(gameId);
            foreach (var card in game.Rounds[game.CurrentRound].PlayedCards)
            {
                card.Winner = false;
            }

            game.Rounds[game.CurrentRound].PlayedCards.FirstOrDefault(c => c.PlayerId == winningPlayer).Winner = true;
            game.CleanTable = true;
            game.Status = _localizer["PlayerWon", game.Players.First(p => p.Id == winningPlayer).FirstName];

            await _gameService.SaveGame(game);

            await Clients.Group(gameId).SendAsync("WinnerRegistered", game);
        }

        public async Task SaveGamePlayer(GamePlayer gamePlayer)
        {
            _logger.LogInformation($"Saving Gameplayer {gamePlayer.Email}.");
            if (_gameService.IsUserGameController(gamePlayer.GameId) || _gameService.IsUserSystemAdmin())
            {
                var gameRegistry = await _gameService.GetGameRegistryByName(gamePlayer.GameId);
                gameRegistry.Players[Player.GetPlayerId(gamePlayer.Player)].Email = gamePlayer.Email;
                gameRegistry.Players[Player.GetPlayerId(gamePlayer.Player)].IsGameAdmin = gamePlayer.IsGameAdmin;
                await _gameService.SaveGameRegistryPersistent(gameRegistry, true);

                var gameRegistries = _gameService.GetGameRegistries();
                await Clients.Caller.SendAsync("SavedGamePlayer", gameRegistries);
            }
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task SaveGameSettings(string gameId, string gameSettings)
        {
            var game = JsonConvert.DeserializeObject<Game>(gameSettings);
            await _gameService.SaveGame(game);
            await Clients.Group(gameId).SendAsync("GameSettingsResetted", game);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task SaveGameToDb(string gameId)
        {
            if (gameId != "TestGame")
            {
                var game = _gameService.GetGame(gameId);
                await _gameService.SaveGamePersistent(game, false);
                await Clients.Caller.SendAsync("GameSavedToDB");
            }
        }
        public async Task Shuffle(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            game.Shuffle();
            game.Status = _localizer["WaitingToBet", game.CurrentPlayerObj.FirstName];
            game.ChooseWinner = false;

            await _gameService.SaveGame(game);

            foreach (var pl in game.Players)
            {
                var pId = Player.GetPlayerId(pl.Id);
                await Clients.Client(game.PlayerConnections[pId]).SendAsync("Shuffled", game.Players[pId].Cards, game);
            }

            foreach (var connection in game.ViewerConnections)
            {
                await Clients.Client(connection).SendAsync("Shuffled", new System.Collections.Generic.List<Card>(), game);
            }
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            game.Status = _localizer["WaitToShuffle", game.CurrentPlayerObj.FirstName];
            await _gameService.SaveGame(game);

            await Clients.Group(gameId).SendAsync("GameStarted", game);
            _logger.LogInformation($"Game {gameId} started.");
        }

        public async Task ViewGame(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

                var gameScores = await _gameService.GetTopScores();
                gameScores = gameScores.OrderByDescending(g => g.Score).Take(10).ToList();

                game.ViewerConnections.Add(Context.ConnectionId);
                await _gameService.SaveGame(game);

                //Remove all card from game that is sent back to viewer so they cannot see cards of players
                Array.ForEach(game.Players, player => player.Cards.Clear());

                await Clients.Caller.SendAsync("ViewedGame", game, gameScores);
                _logger.LogInformation($"{GetUser()} joined Game for viewing {gameId}.");
            }
        }
        private string GetUser()
        {
            return ((System.Security.Claims.ClaimsIdentity)Context.User.Identity).Claims.FirstOrDefault(c => c.Type == "emails")?.Value.ToString();
        }
    }
}