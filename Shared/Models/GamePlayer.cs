using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorSignalRApp.Shared.Models
{
    public class GamePlayer
    {
        public string GameId { get; set; }
        public string Player { get; set; }
        public string Email { get; set; }
        public bool IsGameAdmin { get; set; }
    }
}
