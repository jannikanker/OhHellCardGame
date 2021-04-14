using System;
using System.Linq;
using System.Threading.Tasks;
using CardGamesHub.Server.Services;
using CardGames.Shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;


namespace CardGamesHub.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class GameHub : Hub
    {
        private GameService _gameService;
        private IStringLocalizer<GameHubStrings> _localizer;
        private readonly ILogger _logger;
        private GameSettings _settings;
        private string _viewUser = "viewer";

        public GameHub(GameService gameService,
                       IStringLocalizer<GameHubStrings> localizer,
                       ILogger<GameHub> logger,
                       IOptions<GameSettings> settings)
        {
            _gameService = gameService;
            _localizer = localizer;
            _logger = logger;
            _settings = settings.Value;
        }


        //[Authorize(Policy = "IsAdmin")]
        //public async Task NewGame(string name, string gameAdmin, int nrPlayers)
        //{
        //    var game = _gameService.NewGame(name, nrPlayers);
        //    game.GameAdmin = gameAdmin;
        //    var games = await _gameService.GetGameRegistries();
        //    await Clients.Caller.SendAsync("NewGameCreated", games);
        //    _logger.LogInformation($"New Game created with name: {name}.");
        //}

        [Authorize(Policy = "IsAdmin")]
        public async Task NewGameRegistry(string name, string gameAdmin, int nrPlayers)
        {
            var game = _gameService.NewGameRegistry(name, nrPlayers);
            var gameRegistries = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("NewGameRegistryCreated", gameRegistries);
            _logger.LogInformation($"New GameRegistry created with name: {name}.");
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task NewGame(string gameRegistryId)
        {
            var gameRegistry = await _gameService.GetGameRegistryById(gameRegistryId);
            var game = _gameService.NewGame(gameRegistry);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("NewGameCreated", games);
        }

        [Authorize(Policy = "IsAdmin")]
        public async Task RemoveGame(string gameRegistryId)
        {
            var gameRegistry = await _gameService.GetGameRegistryById(gameRegistryId);
            _gameService.RemoveGame(gameRegistry);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("GameRemoved", games);
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

        [Authorize(Policy = "IsAdmin")]
        public async Task GetGameSettings(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var json = JsonConvert.SerializeObject(game);
            await Clients.Caller.SendAsync("GameSettings", json);
            //await Clients.Group(gameId).SendAsync("GameResetted", game);

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

        [Authorize(Policy = "IsAdmin")]
        public async Task RemoveGameRegistry(string gameId)
        {
            //_gameService.RemoveGame(gameId);
            await _gameService.RemoveGameRegistryPersitent(gameId);
            var games = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("GameRegistryRemoved", games);
        }

        public async Task GetRunningGames()
        {
            var gameRegistries = await _gameService.GetGameRegistries();
            await Clients.Caller.SendAsync("ReturnRunningGames", gameRegistries);
        }

        public async Task SaveGamePlayer(GamePlayer gamePlayer)
        {
            if (_gameService.IsUserGameController(gamePlayer.GameId) || _gameService.IsUserSystemAdmin())
            {
                var gameRegistry = await _gameService.GetGameRegistryByName(gamePlayer.GameId);
                gameRegistry.Players[Player.GetPlayerId(gamePlayer.Player)].Email = gamePlayer.Email;
                gameRegistry.Players[Player.GetPlayerId(gamePlayer.Player)].IsGameAdmin = gamePlayer.IsGameAdmin;
                await _gameService.SaveGameRegistryPersistent(gameRegistry,true);

                var gameRegistries = _gameService.GetGameRegistries();
                await Clients.Caller.SendAsync("SavedGamePlayer", gameRegistries);
            }
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

        public async Task JoinGame(string gameId, string selectedPlayer, string name)
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                if (!string.IsNullOrEmpty(selectedPlayer))
                {
                    game.Players.Where(p => p.Id == selectedPlayer).First().SignedIn = true;
                    game.Players.Where(p => p.Id == selectedPlayer).First().Name = name;
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
            }
        }

        private string GetUser()
        {
            return ((System.Security.Claims.ClaimsIdentity)Context.User.Identity).Claims.FirstOrDefault(c => c.Type == "emails")?.Value.ToString();
        }

        public async Task GetAvailablePlayers(string gameId)
        {
            var game = _gameService.GetGame(gameId);
            var availablePlayers = game.Players.Where(p => p.SignedIn == false).Select(p => p.Id).ToArray();

            await Clients.Caller.SendAsync("ReturnAvailablePlayers", availablePlayers);
        }

        public async Task StartGame(string gameId)
        {
            var game = _gameService.StartGame(gameId);
            game.Status = _localizer["WaitToShuffle", game.CurrentPlayerObj.FirstName];
            await _gameService.SaveGame(game);

            await Clients.Group(gameId).SendAsync("GameStarted", game);
            _logger.LogInformation($"Game {gameId} started.");
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

        public async Task PlayRandomCard(string gameId, string player)
        {
            var game = _gameService.GetGame(gameId);
            var _selectedplayer = Player.GetPlayerId(player);
            var card = game.Players[_selectedplayer].Cards.First();
            await PlayCard(gameId, player, card);

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
            foreach(var connection in game.ViewerConnections)
            {
                await Clients.Client(connection).SendAsync("PlayedCard", new System.Collections.Generic.List<Card>(), game);
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

        public async Task CleanTable(string gameId)
        {
            var game = _gameService.GetGame(gameId);

            var winningPlayer = game
                .Rounds[game.CurrentRound]
                .PlayedCards.Where(c => c.Winner == true)
                .FirstOrDefault()
                .PlayerId;
            game.Rounds[game.CurrentRound].Wins[Player.GetPlayerId(winningPlayer)]++;
            game.ChooseWinner = false;

            game.CurrentPlayer = Player.GetPlayerId(winningPlayer);
            game.PlayerToStart = Player.GetPlayerId(winningPlayer);

            if (game.Players[0].Cards.Count() == 0)
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

        public async Task RoundWinner(string gameId, string winningPlayer)
        {
            var game = _gameService.GetGame(gameId);
            foreach (var card in game.Rounds[game.CurrentRound].PlayedCards)
            {
                card.Winner = false;
            }

            game.Rounds[game.CurrentRound].PlayedCards.Where(c => c.PlayerId == winningPlayer).FirstOrDefault().Winner = true;
            game.CleanTable = true;
            game.Status = _localizer["PlayerWon", game.Players.Where(p => p.Id == winningPlayer).First().FirstName];

            await _gameService.SaveGame(game);

            await Clients.Group(gameId).SendAsync("WinnerRegistered", game);
        }
    }
}
