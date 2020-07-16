using CardGames.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardGames.Shared.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CardGames.Server.Services
{
    public class GameService
    {
        private List<Game> _games;
        private GameSettings _settings;
        private ILogger<GameService> _logger;

        public GameService(IOptions<GameSettings> settings, ILogger<GameService> logger, IRedisCacheClient redisCacheClient)
        {
            _settings = settings.Value;
            _games = new List<Game>();
            _logger = logger;
        }

        public Game NewGame(string gameId, int nrPlayers)
        {
            _logger.LogInformation($"Adding New Game with id: {gameId}.");
            var game = new Game(gameId, nrPlayers);
            _games.Add(game);

            return _games.Where(g => g.Id == gameId).FirstOrDefault();
        }

        public Game GetGame(string gameId)
        {
            return _games.Where(g => g.Id == gameId).FirstOrDefault();
        }

        public Game StartGame(string gameId)
        {
            _logger.LogInformation($"Starting Game with id: {gameId}.");
            GetGame(gameId).GameStarted = true;
            GetGame(gameId).Rounds[0].Current = true;
            return GetGame(gameId);
        }

        public Game ResetGame(string gameId)
        {
            _logger.LogInformation($"Reset Game with id: {gameId}.");
            var nrPlayers = GetGame(gameId).NrPlayers;
            
            _games.Remove(_games.Where(g => g.Id == gameId).FirstOrDefault());
            var game = new Game(gameId, nrPlayers);
            _games.Add(game);

            return _games.Where(g => g.Id == gameId).FirstOrDefault();
        }

        public void RemoveGame(string gameId, string userEmail)
        {
            _logger.LogInformation($"Removing Game with id: {gameId}.");
            if (userEmail == _settings.SystemAdmin)
            {
                _games.Remove(_games.Where(g => g.Id == gameId).FirstOrDefault());
            }
        }

        public Game NewGameSet(string gameId)
        {
            _logger.LogInformation($"New Game Set on Game with id: {gameId}.");
            var g = _games.Where(g => g.Id == gameId).FirstOrDefault();
            g.NewGameSet();
            return _games.Where(g => g.Id == gameId).FirstOrDefault();
        }

        public List<GamePlayer> GetPlayerGames(string userEmail)
        {
            var gameIds = new List<GamePlayer>();
            foreach (var game in _games)
            {
                foreach (var player in game.Players)
                {
                    var userInGame = new GamePlayer();
                    userInGame.GameId = game.Id;
                    if (player.Email == userEmail || userEmail == _settings.SystemAdmin || IsUserGameAdmin(game.Id, userEmail))
                    {
                        userInGame.Player = player.Id;
                        userInGame.Email = player.Email;
                        userInGame.IsGameAdmin = player.IsGameController;
                        gameIds.Add(userInGame);
                    }                  
                }              
            }
            return gameIds;
        }

        private bool IsUserGameAdmin(string gameId, string playerEmail)
        {
            var game = GetGame(gameId);
            var gamesAdmin = game.Players.Where(p => p.Email == playerEmail && p.IsGameController);
            var isAdmin = gamesAdmin.Count() > 0;
            return isAdmin;
        }
    }
}
