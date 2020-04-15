using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Schema;

namespace BlazorSignalRApp.Shared
{
    public class Game
    {
        public string Id { get; set; }
        public Stock Stock { get; set; }
        public Player[] Players { get; set; }
        public PlayedCard[] PlayedCards { get; set; }
        public bool Playing { get; set; }
        public int NrCards { get; set; }
        public int CurrentRound { get; set; }
        public Round[] Rounds { get; set; }
        public Colours PlayingColour { get; set; }
        public int CurrentPlayer { get; set; }
        public int ShufflingPlayer { get; set; }
        public int PlayerToStart { get; set; }
        public bool GameStarted { get; set; }
        public bool RoundReady { get; set; }
        public bool ChooseWinner { get; set; }
        public bool CleanTable { get; set; }

        public bool AllPlayersSignedIn
        {
            get 
            { 
                return this.Players.Where(p => p.SignIn).Count() == 4; 
            }
        }

        public Game()
        {
            //empty
        }

        public Game(string gameId)
        {
            this.Id = gameId;
            this.Players = new Player[4];
            for (int p = 0; p < 4; p++)
            {
                this.Players[p] = new Player("P" + (p+1).ToString());
            }

            this.CurrentPlayer = 1;
            this.ShufflingPlayer = 4;
            this.PlayerToStart = 1;
            this.Stock = new Stock();

            this.SetNewPlayingCards();
            this.Playing = false;
            this.NrCards = 1;
            this.CurrentRound = 0;
            this.GameStarted = false;
            this.CleanTable = false;
            this.Rounds = new Round[] { new Round(1), new Round(2), new Round(3), new Round(4), new Round(5), new Round(6), new Round(7), new Round(8), new Round(8), new Round(7), new Round(6), new Round(5), new Round(4), new Round(3), new Round(2), new Round(1) };
            this.RoundReady = false;
        }

        public void SetNewPlayingCards()
        {
            var playingCards = new PlayedCard[4];
            for(int i=1; i<=4; i++)
            {
                var playingCard = new PlayedCard { PlayerName = "P" + i.ToString(), Card = null };
                playingCards[i - 1] = playingCard;
            }
            this.PlayedCards = playingCards;
        }

        public void NextRound()
        {
            this.Rounds[this.CurrentRound].Current = false;
            if (this.CurrentRound < this.Rounds.Length - 1)
            {
                this.CurrentRound++;
                this.Rounds[this.CurrentRound].Current = true;
                this.ShufflingPlayer = this.ShufflingPlayer < 4 ? this.ShufflingPlayer+1 : 1;
                this.PlayerToStart = this.ShufflingPlayer < 4 ? this.ShufflingPlayer + 1 : 1;
                this.CurrentPlayer = this.PlayerToStart;
            }
            this.Playing = false;
            this.SetNewPlayingCards();
            this.RoundReady = false;
        } 

        public void Shuffle()
        {
            Random _RNG = new Random();
            Rounds[CurrentRound].Current = true;

            var nrCards = Rounds[CurrentRound].NrCards;
            Stock = new Stock();

            for (int p = 0; p < 4; p++)
            {
                Players[p].Cards = new List<Card>();
                for (var s = 0; s < nrCards; s++)
                {
                    var cardNr = _RNG.Next(Stock.Cards.Count);
                    var card = Stock.Cards.ElementAt(cardNr);
                    Players[p].Cards.Add(card);
                    Stock.Cards.RemoveAt(cardNr);
                }
            }

            this.PlayingColour = RandomEnum<Colours>();
            Playing = true;
        }

        private T RandomEnum<T>()
        {
            Random _RNG = new Random();
            Type type = typeof(T);
            Array values = Enum.GetValues(type);
            lock (_RNG)
            {
                object value = values.GetValue(_RNG.Next(values.Length));
                return (T)Convert.ChangeType(value, type);
            }
        }
    }


    public enum Colours
    {
        C,
        S,
        D,
        H
    }

    public enum Values
    {
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 14,
        Ace = 1
    }

    public class Stock
    {
        private static Random _RNG = new Random();

        public List<Card> Cards = new List<Card>();
        public Stock()
        {
            for (int c = 0; c < 4; c++)
            {
                for (int v = 0; v < 8; v++)
                {
                    var card = new Card
                    {
                        Colour = EnumValue<Colours>(c),
                        Value = EnumValue<Values>(v)
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
                switch(this.Value)
                {
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
                        v = this.Value.ToString().Substring(0,1);
                        break;
                }
                return v + this.Colour.ToString();
            }
        }
    }

    public class PlayedCard
    {
        public string PlayerName { get; set; }
        public Card Card { get; set; }
    }

    public class Player
    {
        public Player()
        {

        }

        public Player(string name)
        {
            this.Cards = new List<Card>();
            this.Name = name;
            this.SignIn = false;
        }
        public string Name { get; set; }
        public bool SignIn { get; set; }
        public List<Card> Cards { get; set; }
    }

    public class Round
    {
        public Round()
        {

        }

        public Round(int nrCards)
        {
            this.Current = false;
            this.Bets = new int[4];
            this.Wins = new int[4];
            this.NrCards = nrCards;
        }

        public bool Current { get; set; }
        public int NrCards { get; set; }
        public int[] Bets { get; set; }
        public int[] Wins { get; set; }
        public int Points(int player)
        {
            return Wins[player] >= Bets[player] ? 5 + Bets[player] - Wins[player] : -1;
        }

    }
}
