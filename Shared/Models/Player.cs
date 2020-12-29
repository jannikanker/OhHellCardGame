using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public class Player
    {
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

        public static int GetPlayerId(string id)
        {
            var pId = 0;
            pId = Convert.ToInt32(id.Substring(1, 1)) - 1;
            return pId;
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

}
