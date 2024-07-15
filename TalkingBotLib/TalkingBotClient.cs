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

namespace TalkingBot
{
    public class TalkingBotClient
    {
        public const int Major = 2;
        public const int Minor = 0;
        public const int Patch = 0;

#if DEBUG
        public const bool IsBuilt = false;
#else
        public const bool IsBuilt = true;
#endif

        public LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
        public DiscordSocketClient _client;
        private DiscordSocketConfig _config;
        private TalkingBotConfig _talkingBotConfig;
        private AudioManager _audioManager;
        private CommandHandlerService _commandService;
        private ILogger<DiscordSocketClient> _logger;

        // private static SlashCommandHandler? _commandsHandler;

        [Serializable]
        public struct CachedMessageRole {
            public ulong messageId;
            public ulong roleId;
        }

        public static List<CachedMessageRole> _cached_message_role = [];
        private static Cacher<CachedMessageRole> _message_cacher = new();

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

            _client = ServiceManager.GetService<DiscordSocketClient>();
            _logger = ServiceManager.GetService<ILogger<DiscordSocketClient>>();
            _lavaNode = ServiceManager.GetService<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>>();
            _audioManager = ServiceManager.GetService<AudioManager>();
            _commandService = ServiceManager.GetService<CommandHandlerService>();

            SetEvents();
        }

        public static void SaveCache() {
            _message_cacher.SaveCached(nameof(CachedMessageRole), [.. _cached_message_role]);
        }

        private async Task Ready() {
            await ServiceManager.ServiceProvider.UseLavaNodeAsync();

            _logger.LogInformation("Logged in as {}!", _client.CurrentUser.Username);
        }

        private void SetEvents() {
            _client.Log += Log;
            _client.Ready += Ready;
            //_client.MessageUpdated += MessageUpdated;
            _client.UserVoiceStateUpdated += OnUserVoiceUpdate;
            // _client.SlashCommandExecuted += _commandsHandler!.HandleCommands;
            // _client.ButtonExecuted += _commandsHandler.HandleButtons;
            _client.Disconnected += OnDisconnect;
        }

        private void SetServices() {
            DiscordSocketClient client = new(_config);
            InteractionService interactionService = new(client.Rest);

            var collection = new ServiceCollection()
                .AddLogging(x => {
                    x.ClearProviders();
                    x.AddConsole();
                    x.SetMinimumLevel(LogLevel.Trace);
                })
                .AddSingleton(_talkingBotConfig)
                .AddSingleton(client)
                .AddSingleton(interactionService)
                .AddSingleton<AudioManager>()
                .AddTransient<AudioModule>()
                .AddTransient<BotModule>()
                .AddSingleton<CommandHandlerService>()
                .AddLavaNode<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>,
                    LavaPlayer<LavaTrack>, LavaTrack>(config => {
                    config = new() {
                        Hostname = _talkingBotConfig.LavalinkHostname,
                        Port = _talkingBotConfig.LavalinkPort,
                        Authorization = "youshallnotpass",
                        SelfDeaf = false,
                        SocketConfiguration = new() {
                            ReconnectAttempts = 3, 
                            ReconnectDelay = 5, 
                            BufferSize = 1024
                        },
                        IsSecure = false
                    };
                })
                .AddSingleton(_message_cacher);

            ServiceManager.SetProvider(collection);
        }

        private async Task OnUserVoiceUpdate(SocketUser user, SocketVoiceState prevVs, SocketVoiceState newVs) {
            // if(user is not SocketGuildUser guildUser) return;
            // if(guildUser.Id == _client!.CurrentUser.Id && newVs.VoiceChannel == null) {
            //     var shard = _client.GetShardFor(prevVs.VoiceChannel.Guild);
            //     if(shard == null) {
            //         _logger.LogError("Shard doesn't exist for the guild. WTF");
            //         return;
            //     }
            //     _logger.LogDebug("Bot was disconnected from the voice");
            //     var voiceState = shard.Guilds.First().CurrentUser as IVoiceState;
            //     await _audioManager.LeaveAsync(voiceState, shard.Guilds.First());
            // }
            
            // SocketVoiceChannel channel = prevVs.VoiceChannel;

            // if(channel != null) {
            //     var shard = _client.GetShardFor(prevVs.VoiceChannel.Guild);

            //     if(shard != null) {
            //         var usr = shard.Guilds.First().GetUser(shard.CurrentUser.Id);
            //         if(usr == null) return;

            //         var users = channel.ConnectedUsers;
            //         if(usr.VoiceChannel != null && usr.VoiceChannel.Id == channel.Id && users.Count == 1) {
            //             _logger.LogDebug("Bot was disconnected from the voice");
            //             var voiceState = shard.Guilds.First().CurrentUser as IVoiceState;
            //             await _audioManager.LeaveAsync(voiceState, shard.Guilds.First());
            //         }
            //     }
            // }
        }
        private async Task OnDisconnect(Exception e) {
            _logger.LogError("Disconnected, {}", e);

            // var voiceState = shard.Guilds.First().CurrentUser as IVoiceState;
            // await _audioManager.LeaveAsync(voiceState, shard.Guilds.First());
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
