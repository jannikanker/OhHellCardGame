using BlazorSignalRApp.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSignalRApp.Server.Services
{
    public class GameService
    {
        private Hashtable _games;

        private static GameService instance;

        private GameService()
        {
            _games = new Hashtable();
        }

        public static GameService Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameService();
                return instance;
            }
        }

        public Game NewGame(string name)
        {
            
            return ResetGame(name);
        }

        public Game GetGame(string gameId)
        {
            return (Game)_games[gameId];
        }

        public Game StartGame(string gameId)
        {
            var game = GetGame(gameId);
            game.GameStarted = true;
            game.Rounds[0] = true;
            _games[gameId] = game;
            return game;
        }

        public Game ResetGame(string gameId)
        {
            _games.Remove(gameId);
            var g = new Game(gameId);
            _games.Add(g.Id, g);

            return (Game)_games[gameId]; ;
        }

        public List<string> GetGames()
        {
            var gameIds = new List<string>();
            foreach(var key in _games.Keys)
            {
                gameIds.Add((string)key);
            }
            return gameIds;
        }
    }
}
