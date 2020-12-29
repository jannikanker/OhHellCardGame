using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
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
}
