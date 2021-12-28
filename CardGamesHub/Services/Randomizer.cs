using System;
using System.Collections.Generic;

namespace CardGames.Services
{
    public class Randomizer
    {
        public static T[] Randomize<T>(T[] items)
        {
            Random rand = new Random();

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (var i = 0; i < items.Length - 1; i++)
            {
                int j = rand.Next(i, items.Length);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }

            return items;
        }

        public static List<T> Randomize<T>(List<T> list)
        {
            var randomizedList = new List<T>();
            var rnd = new Random();
            while (list.Count > 0)
            {
                var index = rnd.Next(0, list.Count); //pick a random item from the master list
                randomizedList.Add(list[index]); //place it at the end of the randomized list
                list.RemoveAt(index);
            }
            return randomizedList;
        }
    }
}
