using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using CardGames.Shared;
using CardGames.Shared.Models;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace CardGames.Pages
{
    public class BoerenBridgeBase : ComponentBase
    {
        [Inject] NavigationManager NavigationManager { get; set; }
        [Inject] ITokenAcquisition TokenHandler { get; set; }
        [Inject] IConfiguration  Configuration { get; set; }
        [Inject] Microsoft.Extensions.Options.IOptions<GameSettings> GameSettings { get; set; }
        [Inject] protected IStringLocalizer<UIStrings> L { get; set; }

        [Parameter]
        public string _selectedPlayer { get; set; }
        [Parameter]
        public string _selectedGame { get; set; }

        [CascadingParameter]
        protected Task<AuthenticationState> authenticationStateTask { get; set; }

        protected HubConnection _hubConnection;
        protected bool _playerSelected;
        protected Game _game;
        protected List<Card> _cards = new List<Card>();
        protected List<PlayerSelection> _playerSelections = new List<PlayerSelection>();
        protected List<GameScore> _topScores;
        protected bool _inprogress = false;

        protected string _modalClass = "";
        protected string _modalDisplay = "none;";

        protected string _modalClassTrump = "";
        protected string _modalDisplayTrump = "none;";
        protected string _modalClassTopscores = "";
        protected string _modalDisplayTopscores = "none;";
        protected string _modalClassLastCards = "";
        protected string _modalDisplayLastCards = "none;";
        protected string _modalClassGameCards = "";
        protected string _modalDisplayGameCards = "none;";
        protected string _sideView = "show;";

        protected string _inputLabels = "";
        protected string[][] _inputSeries;
        protected string _xAxisLabels;

        protected int _cardsWidth = 482;
        protected int _cardIncrease = 121;
        protected GameSettings settings;


        protected override async Task OnInitializedAsync()
        {
            settings = GameSettings.Value;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(settings.GameHubUrl, options =>
                {
                    options.AccessTokenProvider = GetAccessToken;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<Game, List<GameScore>>("JoinedGame", (game, topScores) =>
            {
                if (_selectedPlayer.ToLower() == "view")
                {
                    return;
                }

                _inprogress = false;
                _game = game;
                _topScores = topScores;
                _cardsWidth = 482 + (game.Players[GameUtils.GetPlayerId(_selectedPlayer)].Cards.Count() > 4 ? (_cardIncrease * (game.Players[GameUtils.GetPlayerId(_selectedPlayer)].Cards.Count() - 4)) : 0);
                if (game != null)
                {
                    if (!string.IsNullOrEmpty(_selectedPlayer))
                    {
                        _cards = _game.Players[GameUtils.GetPlayerId(_selectedPlayer)].Cards;
                    }
                    _inputSeries = new string[_game.Players.Length][];
                    for (int p = 0; p < _game.Players.Length; p++)
                    {
                        _inputSeries[p] = new string[_game.Rounds.Length + 1];
                        Array.Fill(_inputSeries[p], "0");
                    }
                }
                StateHasChanged();
            });

            _hubConnection.On<Game, List<GameScore>>("ViewedGame", (game, topScores) =>
            {
                _inprogress = false;
                _game = game;
                _topScores = topScores;

                StateHasChanged();
            });

            _hubConnection.On<Game>("GameStarted", (game) =>
            {
                _inprogress = false;
                _game = game;

                StateHasChanged();
            });

            _hubConnection.On<Game>("GameResetted", (game) =>
            {
                _game = game;
                _cards = _game.Players[GameUtils.GetPlayerId(_selectedPlayer)].Cards;
                StateHasChanged();
            });

            _hubConnection.On<Game>("NewGameSet", (game) =>
            {
                _game = game;
                _cards = new List<Card>();
                StateHasChanged();
            });

            _hubConnection.On<Game, string>("PlayerSelected", (game, signedInPlayerID) =>
            {
                _inprogress = false;
                _game = game;
                var index = _playerSelections.FindIndex(p => p.Id == signedInPlayerID);
                if (index > 0)
                    _playerSelections.RemoveAt(index);

                if (_selectedPlayer == signedInPlayerID)
                {
                    _cards = _game.Players[GameUtils.GetPlayerId(signedInPlayerID)].Cards;
                }

                StateHasChanged();
            });

            _hubConnection.On<List<Card>, Game>("Shuffled", (cards, game) =>
            {
                _inprogress = false;
                _game = game;
                _cards = cards;
                _game.Playing = true;
                _cardsWidth = 482 + (cards.Count() > 4 ? (_cardIncrease * (cards.Count() - 4)) : 0);
                StateHasChanged();
            });

            _hubConnection.On<List<Card>, Game>("PlayedCard", (cards, game) =>
            {
                _inprogress = false;
                _game = game;
                _cards = cards;
                _cardsWidth = 482 + (cards.Count() > 4 ? (_cardIncrease * (cards.Count() - 4)) : 0);

                StateHasChanged();
            });

            _hubConnection.On<string[]>("ReturnAvailablePlayers", (rap) =>
            {
                _inprogress = false;
                _playerSelections = new List<PlayerSelection>();
                foreach (var p in rap)
                {
                    _playerSelections.Add(new PlayerSelection { Id = p, Name = p });
                }
                StateHasChanged();
            });

            _hubConnection.On<Game>("WinnerRegistered", (game) =>
            {
                _inprogress = false;
                _game = game;
                StateHasChanged();
            });

            _hubConnection.On<Game>("StartNextRound", (game) =>
            {
                _inprogress = false;
                _game = game;
                StateHasChanged();
            });

            _hubConnection.On<Game>("CleanedTable", (game) =>
            {
                _inprogress = false;
                _game = game;
                StateHasChanged();
            });

            _hubConnection.On<Game>("BetPlaced", (game) =>
            {
                _inprogress = false;
                _game = game;
                StateHasChanged();
            });

            _hubConnection.On<Game>("GameSettingsResetted", (game) =>
            {
                _inprogress = false;
                _game = game;
                StateHasChanged();
            });

            var authState = await authenticationStateTask;
            if (authState.User.Identity.IsAuthenticated)
            {
                await _hubConnection.StartAsync();
                if (_selectedPlayer.ToLower() == "view")
                    await ViewGame();
                else
                    await JoinGame();
            }
        }

        public void OpenTrump()
        {
            _modalDisplayTrump = "block;";
            _modalClassTrump = "Show";
            StateHasChanged();
        }

        public void CloseTrump()
        {
            _modalDisplayTrump = "none";
            _modalClassTrump = "";
            StateHasChanged();
        }

        public void ToggleSideView()
        {
            if (_sideView == "show")
                _sideView = "none";
            else
                _sideView = "show";
            StateHasChanged();
        }

        public void OpenScoreBoard()
        {
            _modalDisplay = "block;";
            _modalClass = "Show";
            StateHasChanged();
        }

        public void CloseScoreBoard()
        {
            _modalDisplay = "none";
            _modalClass = "";
            StateHasChanged();
        }

        public void OpenTopScoreBoard()
        {
            _modalDisplayTopscores = "block;";
            _modalClassTopscores = "Show";
            StateHasChanged();
        }


        public void CloseTopScoreBoard()
        {
            _modalDisplayTopscores = "none";
            _modalClassTopscores = "";
            StateHasChanged();
        }


        public void OpenLastPlayedCards()
        {
            _modalDisplayLastCards = "block;";
            _modalClassLastCards = "Show";
            StateHasChanged();
        }

        public void OpenGamePlayedCards()
        {
            _modalDisplayGameCards = "block;";
            _modalClassGameCards = "Show";
            StateHasChanged();
        }

        public void CloseGamePlayedCards()
        {
            _modalClassGameCards = "none";
            _modalDisplayGameCards = "";
            StateHasChanged();
        }

        public void CloseLastPlayedCards()
        {
            _modalClassLastCards = "none";
            _modalDisplayLastCards = "";
            StateHasChanged();
        }

        public void CreateScoreGraphDataSets()
        {
            var xAxisLabelsArray = new string[_game.Rounds.Length + 1];
            for (int l = 0; l <= _game.Rounds.Length; l++)
            {
                xAxisLabelsArray[l] = l.ToString();
            }

            foreach (var player in _game.Players)
            {
                for (int r = 0; r <= _game.CurrentRound + 1; r++)
                {
                    if (r > 0)
                        _inputSeries[GameUtils.GetPlayerId(player.Id)][r] = _game.Rounds[r - 1].Scores[GameUtils.GetPlayerId(player.Id)].ToString();
                }
            }

            //create inputs for graph
            _xAxisLabels = string.Join(",", xAxisLabelsArray);
        }

        protected async Task<string> GetAccessToken()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            var token = "";
            if (user.Identity.IsAuthenticated)
            {
                try
                {
                    var initialScopes = Configuration.GetValue<string>("DownstreamApi:Scopes")?.Split(' ');
                    var userflow = Configuration.GetValue<string>("AzureAdB2C:SignUpSignInPolicyId");
                    token = await TokenHandler.GetAccessTokenForUserAsync(initialScopes);
                }
                catch (Exception)
                {
                    //ConsentHandler.HandleException(ex);
                    NavigationManager.NavigateTo("MicrosoftIdentity/Account/SignOut");
                }
            }
            return token;
        }

        protected async Task PlaceBet(MouseEventArgs e, string bet)
        {
            if (!_inprogress)
            {
                await _hubConnection.SendAsync("PlaceBet", _selectedGame, _selectedPlayer, bet);
                _inprogress = true;
            }
        }

        protected async Task JoinGame()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (!string.IsNullOrEmpty(_selectedGame))
                if (!_inprogress)
                {
                    await _hubConnection.SendAsync("JoinGame", _selectedGame, _selectedPlayer, user.Identity.Name);
                    _inprogress = true;
                }
        }

        protected async Task ViewGame()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (!string.IsNullOrEmpty(_selectedGame))
                if (!_inprogress)
                {
                    await _hubConnection.SendAsync("ViewGame", _selectedGame);
                    _inprogress = true;
                }
        }

        protected async Task CleanTable()
        {
            if (!_inprogress)
            {
                await _hubConnection.SendAsync("CleanTable", _game.Id);
                _inprogress = true;
            }
        }

        protected async Task StartGame()
        {
            if (!String.IsNullOrEmpty(_selectedPlayer))
            {
                if (!_inprogress)
                {
                    await _hubConnection.SendAsync("StartGame", _game.Id);
                    _inprogress = true;
                }
            }
        }

        protected void NextRound()
        {
            if (!_inprogress)
            {
                _hubConnection.SendAsync("NextRound", _game.Id);
                _inprogress = true;
            }
        }

        protected void Shuffle()
        {
            if (!_inprogress)
            {
                _hubConnection.SendAsync("Shuffle", _game.Id);
                _inprogress = true;
            }
        }

        public void Play(Card card)
        {
            if (!_inprogress)
            {
                _hubConnection.SendAsync("PlayCard", _game.Id, _selectedPlayer, card);
                _inprogress = true;
            }
        }

        public void Winner(PlayedCard winningCard)
        {
            if (!_inprogress)
            {
                _hubConnection.SendAsync("RoundWinner", _game.Id, winningCard.PlayerId);
                _inprogress = true;
            }
        }

        protected async Task SelectPlayer()
        {
            if (!_inprogress)
            {
                _playerSelected = true;
                await _hubConnection.SendAsync("JoinGame", _game.Id, _selectedPlayer);
                _inprogress = true;
            }
        }

        protected async Task GetAvailablePlayers()
        {
            if (!_inprogress)
            {
                await _hubConnection.SendAsync("GetAvailablePlayers", _game.Id);
                _inprogress = true;
            }
        }


        protected async Task<string> GetUserEmail()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity.IsAuthenticated)
            {
                var email = ((System.Security.Claims.ClaimsIdentity)user.Identity).Claims.FirstOrDefault(c => c.Type == "emails");
                if (email != null)
                    return email.Value;
                else
                    return null;
            }
            else
            {
                return null;
            }
        }

        protected bool IsGameController(string player)
        {
            return _game.IsGameController(GameUtils.GetPlayerId(player));
        }

        protected string GetColourChar(string colour)
        {
            switch (colour)
            {
                case "S":
                    return "♠️";
                case "H":
                    return "♥️";
                case "D":
                    return "♦";
                case "C":
                    return "♣️";
                default:
                    return "";
            }
            return "";
        }
    }

    public class PlayerSelection
    {
        public string Id;
        public string Name;
    }
}
