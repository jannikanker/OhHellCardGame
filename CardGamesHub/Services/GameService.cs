using CardGames.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CardGames.Services;

namespace CardGamesHub.Server.Services
{
    [Authorize]
    public class GameService
    {
        private GameSettings _settings;
        private ILogger<GameService> _logger;
        private readonly IRedisCacheClient _redisCacheClient;
        private CosmosSettings _cosmosSettings;
        private IHttpContextAccessor _httpContextAccessor;

        public GameService(IOptions<GameSettings> settings, ILogger<GameService> logger, IRedisCacheClient redisCacheClient, IOptions<CosmosSettings> cosmosSettings, IHttpContextAccessor httpContextAccessor)
        {
            _settings = settings.Value;
            _logger = logger;
            _redisCacheClient = redisCacheClient;
            _cosmosSettings = cosmosSettings.Value;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task<List<GameScore>> GetTopScores()
        {
            List<GameScore> gameScores = new List<GameScore>();
            try
            {
                var sqlQueryText = "SELECT g.Id, g.CompetitionId, g.GameOverDateTime, p.Name, p.FirstName, p.Score FROM Games g JOIN p in g.Players";
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = client.GetDatabase(_cosmosSettings.DatabaseName);
                    var container = database.GetContainer(_cosmosSettings.GamesContainer);
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
            return gameScores;
        }

        public Game NewGame(GameRegistry gameRegistry)
        {
            _logger.LogInformation($"Adding New Game with id: {gameRegistry.Name}.");
            var game = GetGame(gameRegistry.Name);
            if (game != null)
            {
                RemoveGame(gameRegistry);
            }
            var players = new List<Player>();
            foreach (var p in gameRegistry.Players)
            {
                players.Add(new Player { Id = p.Player, Name = p.Player, Email = p.Email, IsGameController = p.IsGameAdmin });
            }

            game = new Game(gameRegistry.CurrentCompetition, gameRegistry.Name, players.ToArray());
            _redisCacheClient.Db0.AddAsync(gameRegistry.Name, game).Wait();
            gameRegistry.GameState = GameStates.GameCreated;
            SaveGameRegistryPersistent(gameRegistry, true).Wait();
            return game;
        }

        public GameRegistry NewGameRegistry(string gameName, int nrPlayers)
        {
            _logger.LogInformation($"Adding New Game Registry with Name: {gameName}.");

            var gameRegistry = new GameRegistry(gameName);
            for (var p = 0; p < nrPlayers; p++)
            {
                var userInGame = new GamePlayer();
                userInGame.GameId = gameName;
                userInGame.Player = "P" + (p + 1).ToString();
                userInGame.Email = "";
                userInGame.IsGameAdmin = p == 0;
                gameRegistry.Players.Add(userInGame);
            }
            SaveGameRegistryPersistent(gameRegistry).Wait();

            return gameRegistry;
        }

        public async Task SaveGame(Game game)
        {
            _logger.LogInformation($"Saving Game with id: {game.Id}.");
            await _redisCacheClient.Db0.RemoveAsync(game.Id);
            await _redisCacheClient.Db0.AddAsync(game.Id, game);
            //await SaveGamePersistent(game, true);
        }

        public async Task SaveGameRegistryPersistent(GameRegistry gameRegistry, bool overwrite = false)
        {
            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.GamesRegistryContainer);
                    if (overwrite)
                    {
                        await container.ReplaceItemAsync(gameRegistry, gameRegistry.id);
                    }
                    else
                    {
                        await container.CreateItemAsync<GameRegistry>(gameRegistry);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
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
                    var container = database.Database.GetContainer(_cosmosSettings.GamesContainer);
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
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
        }

        public Game GetGame(string gameId)
        {
            try
            {
                var gameData = _redisCacheClient.Db0.Database.StringGetAsync(gameId).Result;
                if (gameData != StackExchange.Redis.RedisValue.Null)
                {
                    string result = System.Text.Encoding.UTF8.GetString(gameData);
                    var game = JsonSerializer.Deserialize<Game>(result);
                    return game;
                }
                else
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
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

            game.StartNewGame(true);
            _redisCacheClient.Db0.ReplaceAsync(gameId, game).Wait();

            return game;
        }

        public Game ResetCurrentRound(string gameId)
        {
            _logger.LogInformation($"Reset Round with id: {gameId}.");
            var game = GetGame(gameId);
            game.ResetCurrentRound();

            return game;
        }

        public void RemoveGame(GameRegistry gameRegistry)
        {
            _logger.LogInformation($"Removing Game with id: {gameRegistry.Name}.");
            _redisCacheClient.Db0.RemoveAsync(gameRegistry.Name).Wait();
            gameRegistry.GameState = GameStates.NoGame;
            SaveGameRegistryPersistent(gameRegistry, true).Wait();

        }

        public void ShufflePlayers(GameRegistry gameRegistry)
        {
            if (gameRegistry.GameState == GameStates.NoGame)
            {
                var list = Randomizer.Randomize(gameRegistry.Players);
                int playerId = 1;
                foreach(var player in list)
                {
                    player.Player = $"P{playerId.ToString()}";
                    playerId++;
                }
                gameRegistry.Players = list;
                SaveGameRegistryPersistent(gameRegistry, true).Wait();
            }
        }

        public async Task RemoveGameRegistryPersitent(string gameRegistryId)
        {
            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.GamesRegistryContainer);
                    await container.DeleteItemAsync<GameRegistry>(gameRegistryId, new PartitionKey(gameRegistryId));
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
        }

        public List<GamePlayer> GetPlayerGames()
        {
            string email = GetUser();
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
                    if (player.Email == email || email == _settings.SystemAdmin || IsUserGameController(game.Id, email))
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

        public async Task<List<GameRegistry>> GetGameRegistries()
        {
            var registries = new List<GameRegistry>();
            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.GamesRegistryContainer);

                    QueryDefinition queryDefinition = new QueryDefinition("select * from c");
                    using (FeedIterator<GameRegistry> feedIterator = container.GetItemQueryIterator<GameRegistry>(
                        queryDefinition,
                        null,
                        new QueryRequestOptions()))
                    {
                        while (feedIterator.HasMoreResults)
                        {
                            foreach (var item in await feedIterator.ReadNextAsync())
                            {
                                if ((item.Players.Where(p => p.Email.Trim() == GetUser()).Count() > 0) || IsUserSystemAdmin())
                                {
                                    registries.Add(item);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            return registries;
        }

        public async Task<GameRegistry> GetGameRegistryByName(string GameName)
        {
            var registries = new List<GameRegistry>();
            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.GamesRegistryContainer);

                    QueryDefinition queryDefinition = new QueryDefinition($"SELECT * FROM c where c.Name = \"{GameName}\"");
                    using (FeedIterator<GameRegistry> feedIterator = container.GetItemQueryIterator<GameRegistry>(
                        queryDefinition,
                        null,
                        new QueryRequestOptions()))
                    {
                        while (feedIterator.HasMoreResults)
                        {
                            foreach (var item in await feedIterator.ReadNextAsync())
                            {
                                registries.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            return registries.FirstOrDefault();
        }

        public async Task<GameRegistry> GetGameRegistryById(string GameRegistryId)
        {
            var registries = new List<GameRegistry>();
            try
            {
                using (var client = new CosmosClient(_cosmosSettings.EndpointUrl, _cosmosSettings.Key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_cosmosSettings.DatabaseName);
                    var container = database.Database.GetContainer(_cosmosSettings.GamesRegistryContainer);

                    QueryDefinition queryDefinition = new QueryDefinition($"SELECT * FROM c where c.id = \"{GameRegistryId}\"");
                    using (FeedIterator<GameRegistry> feedIterator = container.GetItemQueryIterator<GameRegistry>(
                        queryDefinition,
                        null,
                        new QueryRequestOptions()))
                    {
                        while (feedIterator.HasMoreResults)
                        {
                            foreach (var item in await feedIterator.ReadNextAsync())
                            {
                                registries.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            return registries.FirstOrDefault();
        }

        private string GetUser()
        {
            return ((System.Security.Claims.ClaimsIdentity)_httpContextAccessor.HttpContext.User.Identity).Claims.FirstOrDefault(c => c.Type == "emails")?.Value.ToString();
        }

        public bool IsUserGameController(string gameId)
        {
            return IsUserGameController(gameId, GetUser());
        }

        private bool IsUserGameController(string gameId, string playerEmail)
        {
            var gameRegistry = GetGameRegistryByName(gameId).Result;
            var gamesControllers = gameRegistry.Players.Where(p => p.Email == playerEmail && p.IsGameAdmin);
            var isController = gamesControllers.Count() > 0;
            return isController;
        }

        public bool IsUserSystemAdmin()
        {
            return (_settings.SystemAdmin == GetUser());
        }

    }
}
