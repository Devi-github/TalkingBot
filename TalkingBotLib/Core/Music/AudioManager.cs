using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;
using Victoria;
using Victoria.Node;
using Victoria.Node.EventArgs;
using Victoria.Player;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using TalkingBot.Core.Logging;
using Microsoft.Extensions.Logging;
using TalkingBot.Utils;

namespace TalkingBot.Core.Music
{
    public class AudioManager
    {
        private static LavaNode _lavaNode;
        private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
        public static readonly HashSet<ulong> VoteQueue;
        static AudioManager() 
        { 
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            VoteQueue = new HashSet<ulong>();

            _lavaNode = TalkingBotClient._lavaNode;
            _lavaNode.OnStatsReceived += OnStatsReceivedAsync;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            _lavaNode.OnTrackException += OnTrackExceptionAsync;
            _lavaNode.OnTrackEnd += TrackEnded;
        }
        public static async Task<InteractionResponse> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if(_lavaNode.HasPlayer(guild))
                return new() { message = "I am already connected to a vc", ephemeral = true };
            
            if (voiceState.VoiceChannel is null) return new() { message = "You must be connected to a vc", ephemeral = true };
            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                return new() { message = $"Connected to a {voiceState.VoiceChannel.Name}" };
            } catch(Exception ex)
            {
                return new() { message = $"Error\n{ex.Message}" , ephemeral = true};
            }
        }
        public static async Task<InteractionResponse> PlayAsync(SocketGuildUser user, IGuild guild, string query)
        {
            if (user.VoiceChannel is null) return new() { message = "You must be connected to a vc", ephemeral = true };

            if (!_lavaNode.HasPlayer(guild))
            {
                try
                {
                    await _lavaNode.JoinAsync(user.VoiceChannel);
                } catch(Exception ex)
                {
                    return new() { message = $"Error\n{ex.Message}" };
                }
            }

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                LavaTrack track;

                var search = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(query, UriKind.Absolute) ? 
                    Victoria.Responses.Search.SearchType.Direct : 
                    Victoria.Responses.Search.SearchType.YouTube, query);

                if (search.Status == Victoria.Responses.Search.SearchStatus.NoMatches) return new() { message = $"Could not find anything for '{query}'", ephemeral = true };

                track = search.Tracks.FirstOrDefault();

                if(player.Track != null && player.PlayerState is PlayerState.Playing ||  player.PlayerState is PlayerState.Paused)
                {
                    player.Vueue.Enqueue(track);

                    Logger.Instance?.LogInformation("(AUDIO) Track is already playing");
                    return new() { message = $"{track.Title} has been added to the queue" };
                }
                await player.PlayAsync(track);

                var embed = new EmbedBuilder()
                    .WithTitle($"{track.Title}")
                    .WithDescription($"Now playing [**{track.Title}**]({track.Url})")
                    .WithColor(0x0A90FA)
                    .Build();

                return new() { embed = embed };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> LeaveAsync(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = "Not connected to any voice!" };
            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out var player);
                if (!success) throw new Exception("Player get failed. Probably not connected");
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                Logger.Instance?.LogInformation($"(AUDIO) Bot left the channel");
                return new() { message = $"I have left the vc" };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> StopAsync(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!" };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Stopped || player.PlayerState is PlayerState.None) return new() { message = $"Music is already stopped" };

                await player.StopAsync();
                player.Vueue.Clear();

                return new() { message = $"Stopped playing the music and cleared the queue" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> PauseAsync(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!" };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Paused) return new() { message = $"Music is already paused", ephemeral = true};
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) return new() { message = $"No songs in queue. Add a song with `/play` command" };

                await player.PauseAsync();

                return new() { message = $"Paused the music" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> ResumeAsync(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!" };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Playing) return new() { message = $"Music is already playing" };
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) return new() { message = $"No songs in queue. Add a song with `/play` command" };

                await player.ResumeAsync();

                return new() { message = $"Resumed the music" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> SkipAsync(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!" };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Paused) return new() { message = $"Music is paused. Resume to skip." };
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) return new() { message = $"No songs in queue. Add a song with `/play` command" };

                await player.SkipAsync();

                return new() { message = $"Skipped a song. Now playing {player.Track}" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static InteractionResponse GetQueue(IGuild guild)
        {
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!" };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) return new() { message = $"No songs in queue. Add a song with `/play` command" };

                string tracks = "Queue:\n";

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Queue")
                    .WithDescription("This is a list of all tracks on this server currently")
                    .AddField("**Currently playing**", $"[**{player.Track.Title}**]({player.Track.Url})", false)
                    .WithColor(0x10FF90);

                int i = 1;
                foreach(LavaTrack track in player.Vueue)
                {
                    embedBuilder.AddField($"{i}", $"[**[{track.Title}**]({track.Url})", false);
                    i++;
                }

                return new() { embed = embedBuilder.Build() };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        private static Task OnTrackExceptionAsync(TrackExceptionEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            return arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it threw an exception.");
        }
        private static Task OnTrackStuckAsync(TrackStuckEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            return arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it got stuck.");
        }
        private static Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            Logger.Instance?.LogError($"{arg.Code} {arg.Reason}");
            return Task.CompletedTask;
        }
        private static Task OnStatsReceivedAsync(StatsEventArg arg)
        {
            
            Logger.Instance?.LogDebug(JsonConvert.SerializeObject(arg));
            return Task.CompletedTask;
        }
        public static async Task TrackEnded(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            if (arg.Reason != TrackEndReason.Finished)
                return;
            if (arg.Player.Vueue.TryDequeue(out var queueable))
                return;
            if (!(queueable is LavaTrack track))
            {
                Logger.Instance?.LogWarning($"Next item in queue is not a track");
                return;
            }

            await arg.Player.PlayAsync(track);

            var embed = new EmbedBuilder()
                    .WithTitle($"{track.Title}")
                    .WithDescription($"Now playing [**{track.Title}**]({track.Url})")
                    .WithColor(0x0A90FA)
                    .Build();
            await arg.Player.TextChannel.SendMessageAsync(embed: embed);
        }
    }
}
