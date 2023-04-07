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
using Microsoft.Extensions.DependencyInjection;
using Victoria.Player;
using Victoria.Node;
using Victoria;

namespace TalkingBot
{
    public class TalkingBotClient : IDisposable
    {
        public InteractionService Service { get; set; }
        private string Token { get; set; }

        private DiscordSocketClient _client;
        private DiscordSocketConfig _config;

        private SlashCommandHandler _handler;

        private LavaNode _lavaNode = ServiceManager.ServiceProvider.GetService<LavaNode>();
        public TalkingBotClient(TalkingBotConfig tbConfig, SlashCommandHandler handler, DiscordSocketConfig? clientConfig = null)
        {
            _config = new DiscordSocketConfig() {
                MessageCacheSize = 100,
                UseInteractionSnowflakeDate = true,
                AlwaysDownloadUsers = true,
            };
            if (clientConfig != null) _config = clientConfig;

            _handler = handler;

            _client = new(_config);
            _client.Log += Log;

            _client.MessageUpdated += MessageUpdated;
            _client.Ready += async () =>
            {
                Console.WriteLine($"Logged in as {_client.CurrentUser.Username}");

                try
                {
                    await _lavaNode.ConnectAsync();
                } catch(Exception ex)
                {
                    Console.Error.WriteLine($"Lava node error: {ex}");
                    Environment.Exit(-1);
                }

                for (int i = 0; i < tbConfig.Guilds.Length; i++)
                {
                    await _handler.BuildCommands(_client, tbConfig.Guilds[i]);
                }
            };
            _client.SlashCommandExecuted += _handler.HandleCommands;

            Token = tbConfig.Token;

            Service = new(_client.Rest);

            var collection = new ServiceCollection();

            collection.AddSingleton(_client);
            collection.AddSingleton(_handler);
            collection.AddSingleton<LavaNode>();
            collection.AddLavaNode(x =>
            {
                x.SelfDeaf = false;
            });

            ServiceManager.SetProvider(collection);
        }
        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, Token);
            await _client.StartAsync();

            await Task.Delay(-1);
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
                Console.Error.WriteLine($"[{DateTime.Now}] {message.Severity}: Error occured: {cmdException.Command.Aliases.First()} " +
                    $"failed to execute in {cmdException.Context.Channel}");
                Console.Error.WriteLine(cmdException.Message);
            }
            else Console.WriteLine($"[{DateTime.Now}] {message.Severity}: {message.Message}");

            return Task.CompletedTask;
        }
    }
}
