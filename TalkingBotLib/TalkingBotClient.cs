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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Victoria;
using TalkingBot.Utils;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TalkingBot.Core.Music;
using TalkingBot.Core.Caching;
using Discord.Rest;
using TalkingBot.Modules;
using TalkingBot.Services;

namespace TalkingBot
{
    using DiscordClient = DiscordShardedClient;
    public class TalkingBotClient
    {
        public const int Major = 2;
        public const int Minor = 0;
        public const int Patch = 1;

#if DEBUG
        public const bool IsBuilt = false;
#else
        public const bool IsBuilt = true;
#endif

        public LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
        public DiscordClient _client;
        private DiscordSocketConfig _config;
        private TalkingBotConfig _talkingBotConfig;
        private AudioManager _audioManager;
        private CommandHandlerService _commandService;
        private ILogger<DiscordClient> _logger;

        private int shardsReady = 0;

        public TalkingBotClient(TalkingBotConfig tbConfig, DiscordSocketConfig? clientConfig = null)
        {
            _talkingBotConfig = tbConfig;
            _config = new DiscordSocketConfig() {
                MessageCacheSize = 100,
                // NOTE: UseInteractionSnowflakeDate sometimes makes my bot throw errors on command
                // It might be because of datetime issue
                UseInteractionSnowflakeDate = false, 
                AlwaysDownloadUsers = true,
                TotalShards = _talkingBotConfig.Guilds!.Length,
                GatewayIntents = GatewayIntents.AllUnprivileged |
                    GatewayIntents.GuildPresences | GatewayIntents.GuildEmojis |
                    GatewayIntents.GuildMembers
            };
            if (clientConfig != null) _config = clientConfig;

            SetServices();

            _client = ServiceManager.GetService<DiscordClient>();
            _logger = ServiceManager.GetService<ILogger<DiscordClient>>();
            _lavaNode = ServiceManager.GetService<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>>();
            _audioManager = ServiceManager.GetService<AudioManager>();
            _commandService = ServiceManager.GetService<CommandHandlerService>();

            _logger.LogDebug("Connecting to lavalink host: {}:{}",
                _talkingBotConfig.LavalinkHostname, _talkingBotConfig.LavalinkPort);

            SetEvents();
        }

        private void SetEvents() {
            _client.Log += Log;
            _client.ShardReady += Ready;
            _client.UserVoiceStateUpdated += OnUserVoiceUpdate;
            _client.ShardDisconnected += OnDisconnect;

            _lavaNode.OnReady += arg => {
                _logger.LogInformation("Connected to LavaNode.");
                return Task.CompletedTask;
            };
        }

        private async Task Ready(DiscordSocketClient shard) {
            shardsReady++;

            if(shardsReady >= _client.Shards.Count)
                await ShardsReady();

            await shard.SetActivityAsync(
                new Game($"Nothing", ActivityType.Listening, ActivityProperties.Instance));

            _logger.LogInformation("Logged in as a shard {}!", shard.CurrentUser.Username);
        }

        private static async Task ShardsReady() {
            await ServiceManager.ServiceProvider.UseLavaNodeAsync();
        }

        private void SetServices() {
            DiscordClient client = new(_config);
            InteractionService interactionService = new(client.Rest);

            var collection = new ServiceCollection()
                .AddLogging(x => {
                    x.ClearProviders();
                    x.AddConsole();
                    x.SetMinimumLevel(_talkingBotConfig.LogLevel);
                })
                .AddSingleton(_talkingBotConfig)
                .AddSingleton(client)
                .AddSingleton(interactionService)
                .AddSingleton<MessageCacher>()
                .AddSingleton<AudioManager>()
                .AddTransient<AudioModule>()
                .AddTransient<BotModule>()
                .AddSingleton<CommandHandlerService>()
                .AddLavaNode<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>,
                    LavaPlayer<LavaTrack>, LavaTrack>(config => {
                    config.Hostname = _talkingBotConfig.LavalinkHostname;
                    config.Port = _talkingBotConfig.LavalinkPort;
                    config.Authorization = "youshallnotpass";
                    config.SelfDeaf = false;
                    config.SocketConfiguration = new() {
                        ReconnectAttempts = 3,
                        ReconnectDelay = 5, 
                        BufferSize = 1024
                    };
                    config.IsSecure = false;
                });

            ServiceManager.SetProvider(collection);
        }

        private async Task OnUserVoiceUpdate(SocketUser user, SocketVoiceState prevVs, SocketVoiceState newVs) {
            if(user is not SocketGuildUser guildUser) return;
            var guild = guildUser.Guild;
            if(guildUser.Id == _client!.CurrentUser.Id && newVs.VoiceChannel == null) {
                _logger.LogDebug("Bot was disconnected from the voice");
                var voiceState = guild.CurrentUser as IVoiceState;
                await _audioManager.LeaveAsync(voiceState, guild);
            }
            
            SocketVoiceChannel channel = prevVs.VoiceChannel;

            if(channel is not null) {
                var usr = guild.CurrentUser;
                if(usr is null) return;

                var users = channel.ConnectedUsers;
                if(usr.VoiceChannel != null && usr.VoiceChannel.Id == channel.Id && users.Count == 1) {
                    _logger.LogDebug("Bot was disconnected from the voice");
                    var voiceState = usr as IVoiceState;
                    await _audioManager.LeaveAsync(voiceState, guild);
                }
            }
        }
        private async Task OnDisconnect(Exception e, DiscordSocketClient shard) {
            _logger.LogError("Disconnected shard {} from gateway, {}", 
                shard.CurrentUser.Username, e);

            await Task.Delay(1);
        }

        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, _talkingBotConfig.Token);
            await _client.StartAsync();

            Stopwatch sw = new();

            sw.Start();
            await _commandService.InitializeAsync();
            sw.Stop();

            _logger.LogDebug("Commands built, {} seconds elapsed", sw.Elapsed.TotalSeconds);

            // foreach(var shard in _client.Shards) {
            //     while (shard.ConnectionState != ConnectionState.Connected) await Task.Delay(10);

            //     Logger.Instance?.LogDebug($"Shard for {shard.Guilds.First().Name} connected");

            //     foreach(var guild in shard.Guilds)
            //     {
            //         sw.Restart();
            //         await _commandsHandler!.BuildCommands(shard, guild.Id, _talkingBotConfig.ForceUpdateCommands);
            //         sw.Stop();
            //         await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", 
            //             $"Commands ({_commandsHandler.GetLength()} in total) built successfully for {guild.Name} ({guild.Id}) in "+
            //             $"{sw.Elapsed.TotalSeconds}s."));
            //     }

            //     await shard.SetActivityAsync(
            //         new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
            // }
            await Task.Delay(-1);
        }
        private Task Log(LogMessage message)
        {
            LogSeverity sev = message.Severity;
            if (sev == LogSeverity.Error)
                _logger.Log(LogLevel.Error, message.Exception, message.Message);
            else if (sev == LogSeverity.Warning)
                _logger.Log(LogLevel.Warning, message.Exception, message.Message);
            else if(sev == LogSeverity.Critical)
                _logger.Log(LogLevel.Critical, message.Exception, message.Message);
            else if(sev == LogSeverity.Info)
                _logger.Log(LogLevel.Information, message.Exception, message.Message);
            else if(sev == LogSeverity.Debug)
                _logger.Log(LogLevel.Debug, message.Exception, message.Message);

            return Task.CompletedTask;
        }
    }
}
