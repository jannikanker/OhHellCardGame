using System.Collections.Generic;
using System.Linq;

namespace CardGames.Shared.Models
{
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