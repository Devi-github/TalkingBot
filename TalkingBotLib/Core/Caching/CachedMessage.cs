using System;
using Discord.Net;
using Newtonsoft.Json;

namespace TalkingBot.Core.Caching {
    public struct CachedMessage {
        [JsonProperty("messageId")]
        public ulong MessageId { get; set; }

        public CachedMessage(ulong id) {
            MessageId = id;
        }
    }
}
