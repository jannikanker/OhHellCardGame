using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public class Team
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Member> Members { get; set; }

    }

    public class Member
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Emoji { get; set; }
    }
}
