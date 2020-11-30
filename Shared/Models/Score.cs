﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public class GameScore
    {
        public string Id { get; set; }
        public DateTime GameOverDateTime { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
    }
}
