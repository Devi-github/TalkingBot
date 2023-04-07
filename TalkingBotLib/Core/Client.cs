using Discord;
using Discord.WebSocket;
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
        public string[] Guilds { get; set; }
    }
    public class Client : IDisposable
    {
        private string Token { get; set; }

        private DiscordSocketClient _client;
        public Client(string token) 
        {
            _client = new();
            _client.Log += Log;

            Token = token;
        }
        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, Token);
            await _client.StartAsync();
        }
        public void Dispose()
        {
            _client.StopAsync();
            _client.Dispose();
        }
        private async Task Log(LogMessage message)
        {
            Console.WriteLine($"{message.Severity}: {message.Message}");
        }
    }
}
