using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public enum GameStates
    {
        NoGame,
        GameCreated,
        GameRunning,
        GameOver
    }
    public class GamePlayer
    {
        public string GameId { get; set; }
        public string Player { get; set; }
        public string Email { get; set; }
        public bool IsGameAdmin { get; set; }
    }

    public class GameRegistry
    {
        public GameRegistry()
        {
            Players = new List<GamePlayer>();
        }

        public GameRegistry(string gameName)
        {
            this.id = Guid.NewGuid().ToString();
            this.GameState = GameStates.NoGame;
            this.Name = gameName;
            this.Players = new List<GamePlayer>();
        }

        public string id { get; set; }
        public string Name { get; set; }
        public GameStates GameState { get; set; }
        public  List<GamePlayer> Players { get; set; }
    }
}
