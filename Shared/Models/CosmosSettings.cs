using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Shared.Models
{
    public class CosmosSettings
    {
        public string EndpointUrl { get; set; }
        public string Key { get; set; }
        public string DatabaseName { get; set; }
        public string GamesContainer { get; set; }

        public string GamesRegistryContainer { get; set; }
    }
}
