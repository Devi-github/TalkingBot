using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    public class TalkingBotConfig
    {
        [JsonProperty("lavalinkHost")]
        public string? LavalinkHostname { get; set; }
        [JsonProperty("lavalinkPort")]
        public int LavalinkPort { get; set; }
        [JsonProperty("token")]
        public string? Token { get; set; }
        [JsonProperty("guilds")]
        public ulong[]? Guilds { get; set; }
        [JsonProperty("forceUpdateCommands")]
        public bool ForceUpdateCommands { get; set; }
        [JsonProperty("logLevel")]
        public LogLevel LogLevel { get; set; }
    }
}
