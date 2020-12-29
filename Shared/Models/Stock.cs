using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
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
}
