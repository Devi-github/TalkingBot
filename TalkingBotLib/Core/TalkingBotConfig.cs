using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    public struct TalkingBotConfig
    {
        public string Token { get; set; }
        public ulong[] Guilds { get; set; }
    }
}
