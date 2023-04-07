using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;

namespace TalkingBot
{
    public class TalkingBotClient : IDisposable
    {
        public InteractionService Service { get; set; }
        private string Token { get; set; }

        private DiscordSocketClient _client;
        private DiscordSocketConfig _config;

        private SlashCommandHandler _handler;
        public TalkingBotClient(TalkingBotConfig tbConfig, SlashCommandHandler handler, DiscordSocketConfig? clientConfig = null)
        {
            _config = new DiscordSocketConfig() { MessageCacheSize = 100, UseInteractionSnowflakeDate = true };
            if (clientConfig != null) _config = clientConfig;

            _handler = handler;

            _client = new(_config);
            _client.Log += Log;

            _client.MessageUpdated += MessageUpdated;
            _client.Ready += async () =>
            {
                Console.WriteLine($"Logged in as {_client.CurrentUser.Username}");

                for (int i = 0; i < tbConfig.Guilds.Length; i++)
                {
                    await _handler.BuildCommands(_client, tbConfig.Guilds[i]);
                }
            };
            _client.SlashCommandExecuted += _handler.HandleCommands;

            Token = tbConfig.Token;

            Service = new(_client.Rest);
        }
        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, Token);
            await _client.StartAsync();
        }
        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            var message = await before.GetOrDownloadAsync();
            Console.WriteLine($"Message update: {message} -> {after}");
        }
        public void Dispose()
        {
            _client.StopAsync();
            GC.SuppressFinalize(_client);
            GC.SuppressFinalize(this);
        }
        private Task Log(LogMessage message)
        {
            if (message.Exception is CommandException cmdException)
            {
                Console.Error.WriteLine($"{message.Severity}: Error occured: {cmdException.Command.Aliases.First()} " +
                    $"failed to execute in {cmdException.Context.Channel}");
                Console.Error.WriteLine(cmdException.Message);
            }
            else Console.WriteLine($"{message.Severity}: {message.Message}");

            return Task.CompletedTask;
        }
    }
}
