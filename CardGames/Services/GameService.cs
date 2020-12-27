using System.Collections.Generic;
using System.Linq;
using CardGames.Shared.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using System;
using System.Threading.Tasks;

namespace CardGames.Server.Services
{
    public class GameService
    {
        private GameSettings _settings;
        private ILogger<GameService> _logger;
        private readonly IRedisCacheClient _redisCacheClient;
        private CosmosSettings _cosmosSettings;

        public GameService(IOptions<GameSettings> settings, ILogger<GameService> logger, IRedisCacheClient redisCacheClient, IOptions<CosmosSettings> cosmosSettings)
        {
            _settings = settings.Value;
            _logger = logger;
            _redisCacheClient = redisCacheClient;
            _cosmosSettings = cosmosSettings.Value;
        }


        public async Task<List<GameScore>> GetTopScores()
        {
            List<GameScore> gameScores = new List<GameScore>();
            try
            {
                var sqlQueryText = "SELECT g.Id, g.GameOverDateTime, p.Name, p.Score FROM Games g JOIN p in g.Players";
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = client.GetDatabase(_cosmosSettings.DatabaseName);
                    var container = database.GetContainer(_cosmosSettings.DatabaseContainer);
                    QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                    var scores = container.GetItemQueryIterator<GameScore>(queryDefinition);


                    while (scores.HasMoreResults)
                    {
                        foreach (var score in await scores.ReadNextAsync())
                        {
                            gameScores.Add(score);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }

            return gameScores.OrderByDescending(g => g.Score).Take(10).ToList();
        }

        public Game NewGame(string gameId, int nrPlayers)
        {
            _logger.LogInformation($"Adding New Game with id: {gameId}.");
            var game = new Game(gameId, nrPlayers);
            _redisCacheClient.Db0.AddAsync(gameId, game).Wait();
            //SaveGamePersistent(game, false).Wait();
            return game;
        }

        public async Task SaveGame(Game game)
        {
            _logger.LogInformation($"Saving Game with id: {game.Id}.");
            await _redisCacheClient.Db0.RemoveAsync(game.Id);
            await _redisCacheClient.Db0.AddAsync(game.Id, game);
            //await SaveGamePersistent(game, true);
        }

        public async Task SaveGamePersistent(Game game, bool overwrite = false)
        {
            //TODO: findout why id is not correctly deserialize from Redis, causing DBid to be null;
            game.DBid = game.Key;

            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.DatabaseContainer);
                    if (overwrite)
                    {
                        await container.ReplaceItemAsync(game, game.Key);
                    }
                    else
                    {
                        await container.CreateItemAsync<Game>(game); 
                    }
                }
            }
            catch(Exception ex)
            {
                var msg = ex.Message;
            }
        }

        public Game GetGame(string gameId)
        {
            var gameData = _redisCacheClient.Db0.Database.StringGetAsync(gameId).Result;
            string result = System.Text.Encoding.UTF8.GetString(gameData);
            var game = JsonSerializer.Deserialize<Game>(result);
            return game;
        }

        public Game StartGame(string gameId)
        {
            _logger.LogInformation($"Starting Game with id: {gameId}.");

            var game = GetGame(gameId);
            game.GameStarted = true;
            game.Rounds[0].Current = true;
            SaveGame(game).Wait();

            return game;
        }

        public Game ResetGame(string gameId)
        {
            _logger.LogInformation($"Reset Game with id: {gameId}.");
            var game = GetGame(gameId);
            var nrPlayers = GetGame(gameId).NrPlayers;

            _redisCacheClient.Db0.RemoveAsync(gameId).Wait();
            game.StartNewGame(true);
            _redisCacheClient.Db0.AddAsync(gameId,game).Wait();

            return game;
        }

        public Game ResetCurrentRound(string gameId)
        {
            _logger.LogInformation($"Reset Round with id: {gameId}.");
            var game = GetGame(gameId);
            game.ResetCurrentRound();

            return game;
        }

        public void RemoveGame(string gameId, string userEmail)
        {
            _logger.LogInformation($"Removing Game with id: {gameId}.");
            if (userEmail == _settings.SystemAdmin)
            {
                _redisCacheClient.Db0.RemoveAsync(gameId).Wait();
            }
        }

        public Game NewGameSet(string gameId)
        {
            _logger.LogInformation($"New Game Set on Game with id: {gameId}.");
            var game = GetGame(gameId);
            game.NewGameSet();
            SaveGame(game).Wait();
            return game;
        }

        public List<GamePlayer> GetPlayerGames(string userEmail)
        {
            var gameIds = new List<GamePlayer>();
            var listOfKeys = _redisCacheClient.Db0.SearchKeysAsync("*").Result;
            foreach (var key in listOfKeys)
            {
                var game = GetGame(key);
                game.Id = key;
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
