using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    public struct InteractionResponse
    {
        public string? message { get; set; }
        public Embed? embed { get; set; }
        public bool ephemeral { get; set; }
        public bool isTts { get; set; }
    }
}
