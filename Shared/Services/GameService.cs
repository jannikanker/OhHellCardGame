using BlazorSignalRApp.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorSignalRApp.Shared.Models;
using Microsoft.Extensions.Options;

namespace BlazorSignalRApp.Server.Services
{
    public class GameService
    {
        private Hashtable _games;
        private GameSettings _settings;

        //private static GameService instance;

        public GameService(IOptions<GameSettings> settings)
        {
            _settings = settings.Value;
            _games = new Hashtable();
        }

        //public static GameService Instance
        //{
        //    get
        //    {
        //        if (instance == null)
        //            instance = new GameService(_settings);
        //        return instance;
        //    }
        //}

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
            game.Rounds[0].Current = true;
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

        public void RemoveGame(string gameId, string userEmail)
        {
            if (userEmail == _settings.SystemAdmin)
            {
                _games.Remove(gameId);
            }
        }

        public List<GamePlayer> GetGames(string userEmail)
        {
            var gameIds = new List<GamePlayer>();
            foreach (var key in _games.Keys)
            {
                var gameId = (string)key;
                var game = (Game)_games[key];
                foreach (var player in game.Players)
                {
                    var userInGame = new GamePlayer();
                    userInGame.GameId = gameId;
                    if (player.Email == userEmail || userEmail == _settings.SystemAdmin)
                    {
                        userInGame.Player = player.Name;
                        userInGame.Email = player.Email; 
                        gameIds.Add(userInGame);
                    }                  
                }              
            }
            return gameIds;
        }
    }
}
