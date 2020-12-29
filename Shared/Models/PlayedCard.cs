using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
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

}
