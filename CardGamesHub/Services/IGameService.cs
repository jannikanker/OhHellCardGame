using CardGames.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CardGamesHub.Server.Services
{
    public interface IGameService
    {
        Game GetGame(string gameId);
        Task<List<GameRegistry>> GetGameRegistries();
        Task<GameRegistry> GetGameRegistryById(string GameRegistryId);
        Task<GameRegistry> GetGameRegistryByName(string GameName);
        List<GamePlayer> GetPlayerGames();
        Task<List<GameScore>> GetTopScores();
        bool IsUserGameController(string gameId);
        bool IsUserSystemAdmin();
        Game NewGame(GameRegistry gameRegistry);
        GameRegistry NewGameRegistry(string gameName, int nrPlayers);
        void RemoveGame(GameRegistry gameRegistry);
        Task RemoveGameRegistryPersitent(string gameRegistryId);
        Game ResetCurrentRound(string gameId);
        Game ResetGame(string gameId);
        Task SaveGame(Game game);
        Task SaveGamePersistent(Game game, bool overwrite = false);
        Task SaveGameRegistryPersistent(GameRegistry gameRegistry, bool overwrite = false);
        void ShufflePlayers(GameRegistry gameRegistry);
        Game StartGame(string gameId);
    }
}