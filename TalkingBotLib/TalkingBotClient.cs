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
using TalkingBot.Utils;
using TalkingBot.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TalkingBot.Core.Music;
using TalkingBot.Core.Caching;

namespace TalkingBot
{
    public class TalkingBotClient : IDisposable
    {
        public const int Major = 1;
        public const int Minor = 6;
        public const int Patch = 1;

        // TODO: Change automatically when Release build
        public const bool IsBuilt = true;

        public static LavaNode? _lavaNode;
        public static DiscordShardedClient? _client;
        private static DiscordSocketConfig? _config;
        private static TalkingBotConfig _talkingBotConfig;
        private static SlashCommandHandler? _commandsHandler;

        [System.Serializable]
        public struct CachedMessageRole {
            public ulong messageId;
            public ulong roleId;
        }

        public static List<CachedMessageRole> _cached_message_role = new();
        private static Cacher<CachedMessageRole> _message_cacher = new();

        public static void SaveCache() {
            _message_cacher.SaveCached(nameof(CachedMessageRole), _cached_message_role.ToArray());
        }

        public TalkingBotClient(TalkingBotConfig tbConfig, DiscordSocketConfig? clientConfig = null)
        {
            _talkingBotConfig = tbConfig;
            _config = new DiscordSocketConfig() {
                MessageCacheSize = 100,
                UseInteractionSnowflakeDate = false, // XXX: This sometimes makes my bot throw errors on command, idk why. so i'll keep it false
                AlwaysDownloadUsers = true,
                TotalShards = _talkingBotConfig.Guilds.Length,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences | GatewayIntents.GuildEmojis
            };
            if (clientConfig != null) _config = clientConfig;

            _commandsHandler = CommandsContainer.BuildHandler();
            _client = new(_config);
            _cached_message_role = new();
            _message_cacher = new();

            SetEvents();
            SetServices();

            var cachedMsgs = _message_cacher.LoadCached(nameof(CachedMessageRole));
            if(cachedMsgs is not null) _cached_message_role = cachedMsgs!.ToList();

            Logger.Initialize(_talkingBotConfig.LogLevel);
        }
        private async Task ShardReady(DiscordSocketClient shard) {
            await Log(new(LogSeverity.Info, "TalkingBotClient.Ready()", 
                $"Logged in as a shard {shard.CurrentUser.Username}!"));
        }
        private void SetEvents() {
            _client!.Log += Log;
            _client.ShardReady += ShardReady;
            //_client.MessageUpdated += MessageUpdated;
            _client.UserVoiceStateUpdated += OnUserVoiceUpdate;
            _client.SlashCommandExecuted += _commandsHandler!.HandleCommands;
            _client.ButtonExecuted += _commandsHandler.HandleButtons;
            _client.ShardDisconnected += OnShardDisconnect;
        }

        private void SetServices() {
            ServiceCollection collection = new();
            collection.AddSingleton(_client!);
            collection.AddSingleton<AudioManager>();
            collection.AddSingleton(_commandsHandler!);

            LavaLogger logger = new LavaLogger(LogLevel.Information);
            collection.AddSingleton(logger);

            _lavaNode = new(_client, new(){
                Hostname = _talkingBotConfig.LavalinkHostname,
                Port = (ushort)_talkingBotConfig.LavalinkPort,
                Authorization = "youshallnotpass",
                SelfDeaf = false,
                SocketConfiguration = new() {
                    ReconnectAttempts = 3, 
                    ReconnectDelay = TimeSpan.FromSeconds(5), 
                    BufferSize = 1024
                },
                IsSecure = false
            }, logger);
            collection.AddSingleton(_lavaNode);
            collection.AddSingleton(_message_cacher);

            ServiceManager.SetProvider(collection);
        }

        private async Task OnUserVoiceUpdate(SocketUser user, SocketVoiceState prevVs, SocketVoiceState newVs) {
            if(user is not SocketGuildUser guildUser) return;
            if(guildUser.Id == _client!.CurrentUser.Id && newVs.VoiceChannel == null) {
                var shard = _client.GetShardFor(prevVs.VoiceChannel.Guild);
                if(shard == null) {
                    Logger.Instance?.LogError("Shard doesn't exist for the guild. WTF");
                    return;
                }
                Logger.Instance?.LogDebug("Bot was disconnected from the voice");
                await AudioManager.LeaveAsync(shard.Guilds.First());
            }
            
            SocketVoiceChannel channel = prevVs.VoiceChannel;

            if(channel != null) {
                var shard = _client.GetShardFor(prevVs.VoiceChannel.Guild);

                if(shard != null) {
                    var usr = shard.Guilds.First().GetUser(shard.CurrentUser.Id);
                    if(usr == null) return;

                    var users = channel.ConnectedUsers;
                    if(usr.VoiceChannel != null && usr.VoiceChannel.Id == channel.Id && users.Count == 1) {
                        Logger.Instance?.LogDebug("Bot was disconnected from the voice");
                        await AudioManager.LeaveAsync(shard.Guilds.First());
                    }
                }
            }
        }
        private async Task OnShardDisconnect(Exception e, DiscordSocketClient shard) {
            Logger.Instance?.LogError("Disconnected, {}", e);
            //Console.WriteLine("{0} {1} {2}", e.Message, e.InnerException, e.Data);
            await AudioManager.LeaveAsync(shard.Guilds.First());
        }

        public async Task Run()
        {
            await _client!.LoginAsync(TokenType.Bot, _talkingBotConfig.Token);
            await _client.StartAsync();

            Stopwatch sw = new();

            foreach(var shard in _client.Shards) {
                while (shard.ConnectionState != ConnectionState.Connected) await Task.Delay(10);

                Logger.Instance?.LogDebug($"Shard for {shard.Guilds.First().Name} connected");

                foreach(var guild in shard.Guilds)
                {
                    sw.Restart();
                    await _commandsHandler!.BuildCommands(shard, guild.Id, _talkingBotConfig.ForceUpdateCommands);
                    sw.Stop();
                    await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", 
                        $"Commands ({_commandsHandler.GetLength()} in total) built successfully for {guild.Name} ({guild.Id}) in "+
                        $"{sw.Elapsed.TotalSeconds}s."));
                }

                await shard.SetActivityAsync(
                    new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
            }
            try
            {
                await VictoriaExtensions.UseLavaNodeAsync(ServiceManager.ServiceProvider);
            }
            catch (Exception ex)
            {
                await Log(new(LogSeverity.Critical, "TalkingBotClient.Run()", "Lavalink connection failed!", ex));
            }
            await Task.Delay(-1);
        }
        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, 
            SocketMessage after, ISocketMessageChannel channel)
        {
            var message = await before.GetOrDownloadAsync();
            Logger.Instance?.LogDebug($"Message update: {message} -> {after}");
        }
        public void Dispose()
        {
            _client!.Dispose();
            GC.SuppressFinalize(this);
        }
        private Task Log(LogMessage message)
        {
            if (Logger.Instance is null) throw new NullReferenceException("Logger instance was null");

            LogSeverity sev = message.Severity;
            if (sev == LogSeverity.Error)
                Logger.Instance.Log(LogLevel.Error, message.Exception, message.Message);
            else if (sev == LogSeverity.Warning)
                Logger.Instance.Log(LogLevel.Warning, message.Exception, message.Message);
            else if(sev == LogSeverity.Critical)
                Logger.Instance.Log(LogLevel.Critical, message.Exception, message.Message);
            else if(sev == LogSeverity.Info)
                Logger.Instance.Log(LogLevel.Information, message.Exception, message.Message);
            else if(sev == LogSeverity.Debug)
                Logger.Instance.Log(LogLevel.Debug, message.Exception, message.Message);

            return Task.CompletedTask;
        }
    }
}
