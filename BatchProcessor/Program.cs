using System;
using Microsoft.Azure.Cosmos;
using CardGames.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BatchProcessor
{
    class Program
    {
        static readonly string _endpointUrl = "https://mindparkgames.documents.azure.com:443/";
        static readonly string _key = "oQT3tpYlNegUG73Jvo7VeQUNbChvLBK5nNEtohaLnUbLboaveBnVu8OQli5Je7YCJ8Ippn70DMVMzUhnbS15qA==";
        static readonly string _database = "cardgames";
        static readonly string _container = "boerenbridge";

        static async Task Main(string[] args)
        {


            Console.WriteLine("Start");
            List<Game> games = new List<Game>();
            try
            {
                var sqlQueryText = "SELECT * FROM Games g";

                using (var client = new CosmosClient(_endpointUrl, _key))
                {
                    var database = client.GetDatabase(_database);
                    var container = database.GetContainer(_container);
                    QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                    var scores = container.GetItemQueryIterator<Game>(queryDefinition);

                    while (scores.HasMoreResults)
                    {
                        foreach (var game in await scores.ReadNextAsync())
                        {
                            Console.WriteLine(game.Id);
                            game.CompetitionId = "1";
                            await SaveGamePersistent(game, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();
            }
        }



        public static async Task SaveGamePersistent(Game game, bool overwrite = false)
        {
            //TODO: findout why id is not correctly deserialize from Redis, causing DBid to be null;
            game.DBid = game.Key;

            try
            {
                using (var client = new CosmosClient(_endpointUrl, _key))
                {
                    var database = await client.CreateDatabaseIfNotExistsAsync(_database);
                    var container = database.Database.GetContainer(_container);
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();
            }
        }
    }
}
