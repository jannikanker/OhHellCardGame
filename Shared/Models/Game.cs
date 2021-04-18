using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CardGames.Shared.Models
{
    public class Game
    {
        public string Key { get; set; }
        [Newtonsoft.Json.JsonProperty(PropertyName = "id")]
        public string DBid { get; set; }
        public string Id { get; set; }
        public DateTime GameOverDateTime { get; set; }
        public string GameAdmin { get; set; }
        public Stock Stock { get; set; }
        public Player[] Players { get; set; }
        public string[] PlayerConnections { get; set; }
        public List<string> ViewerConnections { get; set; }
        public bool Playing { get; set; }
        public int NrCards { get; set; }
        public int NrPlayers { get; set; }
        public int NrRounds { get; set; }
        public int CurrentRound { get; set; }
        public Round[] Rounds { get; set; }
        public Card PlayingCard { get; set; }
        public int CurrentPlayer { get; set; }
        public int CurrentNrCards
        {
            get
            {
                return this.CurrentRound < NrRounds ? this.CurrentRound+1 : NrRounds - this.CurrentRound;
            }
        }
        public int ShufflingPlayer { get; set; }
        public int PlayerToStart { get; set; }
        public bool GameStarted { get; set; }
        public bool RoundReady { get; set; }
        public bool ChooseWinner { get; set; }
        public bool CleanTable { get; set; }
        public bool Shuffled { get; set; }
        public bool Betted { get; set; }
        public bool GameOver { get; set; }
        public string Status { get; set; }

        public Player CurrentPlayerObj
        {
            get
            {
                return this.Players.Where(p => p.Id == this.Players[CurrentPlayer].Id).First();
            }
        }

        public Player PlayerToStartObj
        {
            get
            {
                return this.Players.Where(p => p.Id == this.Players[PlayerToStart].Id).First();
            }
        }

        public bool AllPlayersSignedIn
        {
            get
            {
                return this.Players.Where(p => p.SignedIn).Count() == this.NrPlayers;
            }
        }

        public Game()
        {
            //empty
        }

        public Game(string gameId, Player[] Players)
        {
            this.Key = Guid.NewGuid().ToString();
            this.DBid = this.Key;
            this.Id = gameId;
            this.NrPlayers = Players.Length;
            this.Players = Players;
            this.PlayerConnections = new string[NrPlayers];
            this.ViewerConnections = new List<string>();
            StartNewGame(true);
        }

        public void StartNewGame(bool keepPlayer = false)
        {
            this.Key = Guid.NewGuid().ToString();
            this.DBid = this.Key;

            this.PlayingCard = new Card { Colour = Colours.H, Value = Values.Four };

            this.CurrentPlayer = 0;
            this.ShufflingPlayer = 0;
            this.PlayerToStart = 0;
            this.Stock = new Stock(GetBeginCard(NrPlayers));

            this.Playing = false;
            this.NrCards = 1;


            this.CurrentRound = 0;
            this.GameStarted = false;
            this.CleanTable = false;

            this.NrRounds = 16;
            PrepareRounds();

            this.SetNewPlayingCards();
            this.RoundReady = false;
            this.Shuffled = false;
            this.Betted = false;
            this.GameOver = false;

            //remove all viewers
            this.ViewerConnections = new List<string>();

            if (!keepPlayer)
            {
                for (int p = 0; p < NrPlayers; p++)
                {
                    this.Players[p] = new Player("P" + (p + 1).ToString());
                }
                this.Players[0].IsGameController = true;
            }
            else
            {
                this.PlayerConnections = new string[NrPlayers];
                for (int p = 0; p < NrPlayers; p++)
                {
                    this.Players[p].SignedIn = false;
                    this.Players[p].Score = 0;
                    this.Players[p].Cards = new List<Card>();
                }

                //randomize the players
                //var rand = new Random();
                //for (int i = 0; i < this.Players.Length - 1; i++)
                //{
                //    int j = rand.Next(i, this.Players.Length);
                //    string tempEmail = this.Players[i].Email;
                //    bool tempController = this.Players[i].IsGameController;

                //    this.Players[i].Email = this.Players[j].Email;
                //    this.Players[i].IsGameController = this.Players[j].IsGameController;
                //    this.Players[j].Email = tempEmail;
                //    this.Players[j].IsGameController = tempController;
                //}
            }

            void PrepareRounds()
            {
                var rounds = new Round[NrRounds];
                var midPoint = (int)(NrRounds / 2);
                for (int i=1; i<=NrRounds; i++)
                {
                    if(i <= midPoint)
                        rounds[i-1] = new Round(i,NrPlayers);
                    else
                        rounds[i - 1] = new Round(NrRounds-i+1, NrPlayers);
                }
                this.Rounds = rounds;
            }
        }

        private int GetBeginCard(int nrPlayers)
        {
            switch(nrPlayers)
            {
                case 2:
                case 3:
                case 4:
                    return 7; //32 cards
                case 5:
                    return 5; //40 cards
                case 6:
                    return 3; //48 cards
                default:
                    return 2; // 52 cards
            }
        }

        public void SetNewPlayingCards()
        {
            var playingCards = new PlayedCard[NrPlayers];
            for (int i = 1; i <= NrPlayers; i++)
            {
                var playingCard = new PlayedCard { PlayerId = "P" + i.ToString(), Card = null };
                playingCards[i - 1] = playingCard;
            }
            this.Rounds[this.CurrentRound].PlayedCards = playingCards;
        }

        public void NewGameSet()
        {
            StartNewGame();
        }

        public void ResetCurrentRound()
        {
            this.Rounds[this.CurrentRound].Current = true;
            this.PlayerToStart = ShufflingPlayer;
            this.CurrentPlayer = this.PlayerToStart;
            this.Playing = false;
            this.Shuffled = false;
            this.RoundReady = false;
            this.CleanTable = false;
            this.ChooseWinner = false;
            this.Betted = false;
            for(int p = 0; p < this.NrPlayers; p++)
            {
                this.Players[p].Cards = new List<Card>();
                this.Rounds[this.CurrentRound].Bets[p] = -1;
                this.Rounds[this.CurrentRound].Wins[p] = 0;
                this.Rounds[this.CurrentRound].Winners[p] = false;
            }
            this.SetNewPlayingCards();
        }

        public void NextRound()
        {
            if(this.GameOver)
            {
                //do nothing.
                return;
            }

            if (this.CurrentRound < this.Rounds.Length)
            {
                this.Rounds[this.CurrentRound].Current = false;
                for (int i = 0; i < this.NrPlayers; i++)
                {
                    var playerRoundScore = this.Rounds[this.CurrentRound].Wins[i] - this.Rounds[this.CurrentRound].Bets[i];
                    var score = playerRoundScore == 0 ? this.Players[i].Score + 10 + (this.Rounds[this.CurrentRound].Wins[i] * 2) : this.Players[i].Score - (Math.Abs(playerRoundScore) * 2);
                    this.Players[i].Score = score;
                    this.Rounds[this.CurrentRound].Scores[i] = score;
                    this.Rounds[this.CurrentRound].Winners[i] = playerRoundScore == 0; //true for players that win their bet.
                }
                if (this.CurrentRound == this.NrRounds-1)
                {
                    this.GameOver = true;
                    this.GameOverDateTime = DateTime.UtcNow;
                }
                else
                {
                    this.CurrentRound++;
                    this.Rounds[this.CurrentRound].Current = true;
                    this.ShufflingPlayer = this.ShufflingPlayer < this.NrPlayers-1 ? this.ShufflingPlayer + 1 : 0;
                    this.PlayerToStart = ShufflingPlayer;
                    this.CurrentPlayer = this.PlayerToStart;
                    this.Playing = false;
                    this.Shuffled = false;
                    this.RoundReady = false;
                    this.Betted = false;
                    this.SetNewPlayingCards();
                }
            }
        }

        public void Shuffle()
        {
            Random _RNG = new Random();
            Rounds[CurrentRound].Current = true;

            var nrCards = Rounds[CurrentRound].NrCards;
            Stock = new Stock(GetBeginCard(NrPlayers));

            for (int p = 0; p < this.NrPlayers; p++)
            {
                Players[p].Cards = new List<Card>();
                for (var s = 0; s < nrCards; s++)
                {
                    var cardNr = _RNG.Next(Stock.Cards.Count);
                    var card = Stock.Cards.ElementAt(cardNr);
                    Players[p].Cards.Add(card);
                    Stock.Cards.RemoveAt(cardNr);
                }
                Players[p].Cards = Players[p].Cards.OrderBy(c => c.Colour).ThenByDescending(c => c.Value).ToList();
            }

            this.Shuffled = true;
            var pCard = new Card();
            pCard.Colour = RandomEnum<Colours>(4);
            pCard.Value = RandomEnum<Values>(5);
            this.PlayingCard = pCard;
        }

        public void ArchivePlayCard(string player, Card card, int _selectedplayer)
        {
            this.Rounds[this.CurrentRound].PlayedCards[_selectedplayer] = new PlayedCard { PlayerId = player, Card = card };
        }

        public void SetNextPlayer()
        {
            if (this.CurrentPlayer < this.NrPlayers - 1)
                this.CurrentPlayer++;
            else
                this.CurrentPlayer = 0;
        }

        public void RemovePlayedCardFromPlayer(Card card, int _selectedplayer)
        {
            var card2Remove = this.Players[_selectedplayer].Cards.Where(c => c.Colour == card.Colour && c.Value == card.Value).FirstOrDefault();
            if (card2Remove != null)
            {
                this.Players[_selectedplayer].Cards.Remove(card2Remove);
            }
        }

        public void FindWinningCard()
        {
            var winningcard = this.Rounds[this.CurrentRound].PlayedCards[this.PlayerToStart];

            for (int i = 0; i < this.NrPlayers; i++)
            {
                if (this.Rounds[this.CurrentRound].PlayedCards[i].Card.Colour == winningcard.Card.Colour)
                {
                    //normal check
                    if (this.Rounds[this.CurrentRound].PlayedCards[i].Card.Value > winningcard.Card.Value)
                    {
                        winningcard = this.Rounds[this.CurrentRound].PlayedCards[i];
                    }
                }
                else
                {
                    if (this.Rounds[this.CurrentRound].PlayedCards[i].Card.Colour == this.PlayingCard.Colour)
                    {
                        //check in case of Trump
                        if (winningcard.Card.Colour == this.PlayingCard.Colour)
                        {
                            //if both are Trump check highest
                            if (this.Rounds[this.CurrentRound].PlayedCards[i].Card.Value > winningcard.Card.Value)
                            {
                                winningcard = this.Rounds[this.CurrentRound].PlayedCards[i];
                            }
                        }
                        else
                        {
                            //if this card is first Trump than it is the winner
                            winningcard = this.Rounds[this.CurrentRound].PlayedCards[i];
                        }
                    }
                }
                for (int c = 0; c < this.NrPlayers; c++)
                {
                    this.Rounds[this.CurrentRound].PlayedCards[c].Winner = false;
                }
                this.Rounds[this.CurrentRound].PlayedCards[Player.GetPlayerId(winningcard.PlayerId)].Winner = true;
            }
        }

        public bool IsGameController(int player)
        {
            return this.Players[player].IsGameController;
        }

        private T RandomEnum<T>(int range)
        {
            Random _RNG = new Random();
            Type type = typeof(T);
            Array values = Enum.GetValues(type);
            lock (_RNG)
            {
                object value = values.GetValue(_RNG.Next(range));
                return (T)Convert.ChangeType(value, type);
            }
        }
    }
}