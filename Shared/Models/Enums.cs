using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public enum Colours
    {
        C, //Clubs
        D, //Diamonds
        S, //Shade
        H  //Hearts
    }

    public enum Values
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 14,
        Ace = 15
    }

    public enum Players
    {
        P1,P2,P3,P4
    }
}
