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
        public string[] Connections { get; set; }
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

        public Game(string gameId, int nrPlayers)
        {
            this.Key = Guid.NewGuid().ToString();
            this.DBid = this.Key;
            this.Id = gameId;
            this.NrPlayers = nrPlayers;
            this.Players = new Player[NrPlayers];
            this.Connections = new string[NrPlayers];

            StartNewGame();
        }

        public void StartNewGame(bool keepPlayer = false)
        {
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
                this.Connections = new string[NrPlayers];
                for (int p = 0; p < NrPlayers; p++)
                {
                    this.Players[p].SignedIn = false;
                    this.Players[p].Score = 0;
                    this.Players[p].Cards = new List<Card>();
                }
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

    public class Stock
    {
        private static Random _RNG = new Random();

        public List<Card> Cards = new List<Card>();

        public Stock()
        {

        }

        public Stock(int beginCardValue)
        {
            var beginCardIndex = (Values)beginCardValue;
            for (int c = 0; c < 4; c++)
            {
                for (int v = beginCardValue; v < 15; v++)
                {
                    var card = new Card
                    {
                        Colour = EnumValue<Colours>(c),
                        Value = (Values)(v)
                    };
                    Cards.Add(card);
                }
            }
        }

        private static T EnumValue<T>(int index)
        {
            Type type = typeof(T);
            Array values = Enum.GetValues(type);
            object value = values.GetValue(index);
            return (T)Convert.ChangeType(value, type);
        }
    }

    public class Card
    {
        public Colours Colour { get; set; }
        public Values Value { get; set; }

        public string Face
        {
            get
            {
                var v = "";
                switch (this.Value)
                {
                    case Values.Two:
                    case Values.Three:
                    case Values.Four:
                    case Values.Five:
                    case Values.Six:
                    case Values.Seven:
                    case Values.Eight:
                    case Values.Nine:
                    case Values.Ten:
                        v = ((int)this.Value).ToString();
                        break;

                    case Values.Jack:
                    case Values.Queen:
                    case Values.King:
                    case Values.Ace:
                        v = this.Value.ToString().Substring(0, 1);
                        break;
                }
                return v + this.Colour.ToString();
            }
        }
    }

    public class PlayedCard
    {
        public PlayedCard()
        {
            Winner = false;
        }

        public string PlayerId { get; set; }
        public Card Card { get; set; }
        public bool Winner { get; set; }
    }

    public class Player
    {
        public static int GetPlayerId(string id)
        {
            var pId = 0;
            pId = Convert.ToInt32(id.Substring(1, 1)) - 1;
            return pId;
        }

        public Player()
        {
        }

        public Player(string Id)
        {
            this.Cards = new List<Card>();
            this.Id = Id;
            this.Score = 0;
            this.SignedIn = false;
            this.Name = Id;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string FirstName
        {
            get
            {
                return String.IsNullOrEmpty(Name) ? "" : Name.Split(" ")[0];
            }
        }
        public string Email { get; set; }
        public bool SignedIn { get; set; }
        public bool IsGameController { get; set; }
        public int Score { get; set; }
        public List<Card> Cards { get; set; }
    }

    public class Round
    {
        public Round()
        {
        }

        public Round(int nrCards, int nrPlayers)
        {
            this.Current = false;
            this.Bets = new int[nrPlayers];
            for(int i=0; i<nrPlayers; i++)
            {
                this.Bets[i] = -1;
            }
            this.Wins = new int[nrPlayers];
            this.Winners = new bool[nrPlayers];
            this.NrCards = nrCards;
            this.Scores = new int[nrPlayers];
            for (int i = 0; i < nrPlayers; i++)
            {
                this.Scores[i] = 0;
            }
            this.PlayHistory = new List<PlayedCard[]>();
        }

        public Card Trump { get; set; }
        public PlayedCard[] PlayedCards { get; set; }
        public List<PlayedCard[]> PlayHistory { get; set; }
        public bool Current { get; set; }
        public int NrCards { get; set; }
        public int[] Bets { get; set; }
        public int[] Wins { get; set; }
        public int[] Scores { get; set; }
        public bool[] Winners { get; set; }

        public bool AllBetsPlaced
        {
            get
            {
                return (Bets.Where(b => b > -1).Count() == this.Bets.Length);
            }
        }
    }
}