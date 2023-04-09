using Discord.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria.Node;

namespace TalkingBot.Core.Logging
{
    internal class LavaLogger : ILogger<LavaNode>
    {
        private LogLevel _level;
        public LavaLogger(LogLevel logLevel=LogLevel.Debug) 
        {
            _level = logLevel;
        }
        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel < _level) return;
            Console.WriteLine($"[{DateTime.Now.ToString("T")}] [{logLevel}] " + formatter(state, exception));
        }
    }
}
