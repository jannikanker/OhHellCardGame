using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared
{
    public static class GameUtils
    {
        public static int GetPlayerId(string player)
        {
            var pId = 0;
            pId = Convert.ToInt32(player.Substring(1, 1)) - 1;

            return pId;
        }
    }
}
