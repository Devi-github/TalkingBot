using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    public struct TalkingBotConfig
    {
        [JsonProperty("token")]
        public string Token { get; set; }
        [JsonProperty("guilds")]
        public ulong[] Guilds { get; set; }
        [JsonProperty("forceUpdateCommands")]
        public bool ForceUpdateCommands { get; set; }
    }
}
