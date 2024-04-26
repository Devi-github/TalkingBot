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
        private static ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
        public static HashSet<ulong> VoteQueue;
        static AudioManager() 
        { 
            _lavaNode = ServiceManager.ServiceProvider.GetRequiredService<LavaNode>();

            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
            
            VoteQueue = new HashSet<ulong>();

            _lavaNode.OnTrackEnd += OnTrackEndAsync;
            _lavaNode.OnTrackStart += OnTrackStartAsync;
            _lavaNode.OnStatsReceived += OnStatsReceivedAsync;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            _lavaNode.OnTrackException += OnTrackExceptionAsync;
            _lavaNode.OnUpdateReceived += OnUpdateReceivedAsync;
        }

        private static bool isOnLoop = false;
        private static int loopRemaining = 0;

        private static InteractionResponse LavalinkFailed() {
            return new() { message = "Music service is now unavailable! Contact administrator if you have any questions.", ephemeral = true };
        }

        public static async Task<InteractionResponse> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
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
        public static async Task<InteractionResponse> GoToAsync(IGuild guild, double seconds) {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = "Not connected to any voice!", ephemeral = true };
            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out var player);
                if (!success) throw new Exception("Player get failed. Probably not connected");
                if(player.PlayerState is not PlayerState.Playing) 
                    return new() {
                        message = "Bot is not playing! To go to a timestamp you have to have a song playing!",
                        ephemeral = true
                    };
                if(!player.Track.CanSeek) return new() {
                    message = "Cannot go to any position on this track!",
                    ephemeral = true
                };

                TimeSpan timecode = TimeSpan.FromSeconds(seconds);
                if(player.Track.Duration < timecode) return new() { message = "The timecode is outside of track's length!", ephemeral = true};
                await player.SeekAsync(timecode);

                return new() {
                    message = $"Skipped to {timecode.ToString("c")}"
                };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e}", ephemeral = true };
            }
        }
        public static InteractionResponse SetLoop(IGuild guild, int times) {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = "Not connected to any voice!", ephemeral = true };
            
            if(times == 0 || times < -1) return new() { message = "Cannot loop negative or zero times", ephemeral = true };

            try {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is not PlayerState.Playing) return new() {
                    message = "Music is not playing. To loop, play something first",
                    ephemeral = true
                };

                if(isOnLoop) {
                    loopRemaining = times;
                    if(loopRemaining == -1) return new() { message = $"Successfully reset to loop indefinitely" };
                    return new() { message = $"Successfully reset to loop {times} times" };
                } else {
                    isOnLoop = true;
                    loopRemaining = times;
                    if(loopRemaining == -1) return new() { message = $"Successfully set to loop indefinitely" };
                    return new() { message = $"Successfully set to loop {times} times"};
                }
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> PlayAsync(SocketGuildUser user, 
            ITextChannel channel, IGuild guild, string query, double seconds=0)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (user.VoiceChannel is null) return new() { message = "You must be connected to a vc", ephemeral = true };

            if (!_lavaNode.HasPlayer(guild))
            {
                try
                {
                    await _lavaNode.JoinAsync(user.VoiceChannel, channel);
                } catch(Exception ex)
                {
                    return new() { message = $"Error\n{ex.Message}", ephemeral = true };
                }
            }

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");
                
                LavaTrack track;

                var search_type = Victoria.Responses.Search.SearchType.SoundCloud;

                if(Uri.IsWellFormedUriString(query, UriKind.Absolute)) {
                    search_type = Victoria.Responses.Search.SearchType.Direct;
                } else if(query.Contains("youtube.com")) {
                    return new() { message = $"YouTube is not supported!", ephemeral = true };
                }
                // else if(query.Contains("soundcloud.com")) {
                //     search_type = Victoria.Responses.Search.SearchType.SoundCloud;
                // }

                var search = await _lavaNode.SearchAsync(search_type, query);

                if (search.Status == Victoria.Responses.Search.SearchStatus.NoMatches) 
                    return new() { message = $"Could not find anything for '{query}'", ephemeral = true };
                else if(search.Status == Victoria.Responses.Search.SearchStatus.LoadFailed)
                    return new() { message = $"Failed to load track with URL: '{query}'", ephemeral = true };

                track = search.Tracks.FirstOrDefault()!;
                string? thumbnail = search_type switch
                {
                    Victoria.Responses.Search.SearchType.SoundCloud => await track.FetchArtworkAsync(),
                    _ => null,
                };
                var durstr = track.Duration.ToString("c");

                if (player.Track != null && player.PlayerState is PlayerState.Playing ||  player.PlayerState is PlayerState.Paused)
                {
                    player.Vueue.Enqueue(track);

                    var enqueuedEmbed = new EmbedBuilder()
                        .WithTitle($"Enqueued {track.Title}")
                        .WithDescription($"Added [**{track.Title}**]({track.Url}) to the queue")
                        .WithColor(0x0A90FA)
                        .WithThumbnailUrl(thumbnail)
                        .AddField("Duration", durstr, true)
                        .AddField("Requested by", user.Mention, true)
                        .AddField("Video author", track.Author)
                        .Build();

                    return new() { embed = enqueuedEmbed };
                }
                TimeSpan timecode = TimeSpan.FromSeconds(seconds);
                if(track.Duration < timecode) return new() { message = "Set timecode is outside of track's length!", ephemeral = true};

                await player.PlayAsync(track);
                await player.SeekAsync(timecode);

                await TalkingBotClient._client!.GetShardFor(guild).SetActivityAsync(
                    new Game(track.Title, ActivityType.Listening, ActivityProperties.Join, track.Url));
                
                var embed = new EmbedBuilder()
                    .WithTitle($"{track.Title}")
                    .WithDescription($"Now playing [**{track.Title}**]({track.Url})")
                    .WithColor(0x0A90FA)
                    .WithThumbnailUrl(thumbnail)
                    .AddField("Duration", durstr, true)
                    .AddField("Requested by", user.Mention, true)
                    .AddField("Video author", track.Author)
                    .Build();

                return new() { embed = embed };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> LeaveAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = "Not connected to any voice!", ephemeral = true };
            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out var player);
                if (!success) throw new Exception("Player get failed. Probably not connected");
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                loopRemaining = 0;
                isOnLoop = false;

                await TalkingBotClient._client!.GetShardFor(guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));

                return new() { message = $"I have left the vc" };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> StopAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Stopped || player.PlayerState is PlayerState.None) 
                    return new() { message = $"Music is already stopped" };

                await player.StopAsync();
                player.Vueue.Clear();

                await TalkingBotClient._client!.GetShardFor(guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));

                return new() { message = $"Stopped playing the music and cleared the queue" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> PauseAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Paused) 
                    return new() { message = $"Music is already paused", ephemeral = true};
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

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
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Playing) 
                    return new() { message = $"Music is already playing" };
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                await player.ResumeAsync();

                return new() { message = $"Resumed the music" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static InteractionResponse RemoveTrack(IGuild guild, long index)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");
                if (index - 1 < 0 || index > player.Vueue.Count) return new() 
                { 
                    message = $"Index is not present inside the Queue. Enter values from (1 to {player.Vueue.Count})",
                    ephemeral = true 
                };

                var trackRemoved = player.Vueue.RemoveAt((int)(index - 1));

                return new() { message = $"Removed the track **{trackRemoved.Title}**" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> SkipAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.Paused) 
                    return new() { message = $"Music is paused. Resume to skip." };
                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };
                if (player.Vueue.Count == 0) 
                    return new() { message = $"Only currently playing song is in the queue. " +
                        $"You can stop the playback using `/stop` or `/leave`" };
                
                await player.SkipAsync();

                await TalkingBotClient._client!.GetShardFor(guild).SetActivityAsync( // FIXME: This doesn't get set properly because of how SkipAsync works #11
                    new Game(player.Track.Title, ActivityType.Listening, ActivityProperties.Join, player.Track.Url));
                
                string thumbnail = $"https://img.youtube.com/vi/{player.Track.Id}/0.jpg";

                var embed = new EmbedBuilder()
                    .WithTitle($"{player.Track.Title}")
                    .WithDescription($"Now playing [**{player.Track.Title}**]({player.Track.Url})")
                    .WithColor(0x0A90FA)
                    .WithThumbnailUrl(thumbnail)
                    .AddField("Duration", player.Track.Duration.ToString("c"), true)
                    .AddField("Video author", player.Track.Author)
                    .Build();

                return new() { embed = embed };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static InteractionResponse GetCurrentPosition(IGuild guild) {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                string curpos = $"{player.Track.Position.Hours.ToString("00")}:"+
                $"{player.Track.Position.Minutes.ToString("00")}:{player.Track.Position.Seconds.ToString("00")}";

                return new() { message = $"Current track position: **{curpos}**/{player.Track.Duration.ToString("c")}", ephemeral = true };
            } catch(Exception ex) {
                return new() { message = $"Error\n{ex.Message}", ephemeral = true };
            }
        }
        public static InteractionResponse GetQueue(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if (player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Queue")
                    .WithDescription("This is a list of all tracks on this server currently")
                    .AddField("**Currently playing**", $"[**{player.Track.Title}**]({player.Track.Url})", false)
                    .WithColor(0x10FF90);

                int i = 1;
                foreach(LavaTrack track in player.Vueue)
                {
                    embedBuilder.AddField($"{i}", $"[**{track.Title}**]({track.Url})", true);
                    i++;
                }

                return new() { embed = embedBuilder.Build() };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static InteractionResponse GetLength(IGuild guild) {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                if(player.PlayerState is PlayerState.None || player.PlayerState is PlayerState.Stopped)
                    return new() {
                        message = "Cannot display length of a track if it doesn't exist!",
                        ephemeral = true
                    };
                string durstr = player.Track.Duration.ToString("c");
                return new() {
                    message = $"Duration: {durstr}"
                };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public static async Task<InteractionResponse> ChangeVolume(IGuild guild, int volume)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailed();
            if (!_lavaNode.HasPlayer(guild)) return new() { message = $"Not connected to any voice channel!", ephemeral = true };

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                volume = volume <= 100 ? (volume >= 0 ? volume : 0) : 100;

                await player.SetVolumeAsync(volume);

                return new() { message = $"Changed volume to **{volume}**/100" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        private static async Task OnTrackExceptionAsync(TrackExceptionEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it threw an exception.");
        }
        private static async Task OnTrackStuckAsync(TrackStuckEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it got stuck.");
        }
        private static Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            Logger.Instance?.LogError($"{arg.Code} {arg.Reason}");
            return Task.CompletedTask;
        }
        private static Task OnStatsReceivedAsync(StatsEventArg arg)
        {

            return Task.CompletedTask;
        }
        public static Task OnUpdateReceivedAsync(UpdateEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg) {

            return Task.CompletedTask;
        }
        public static Task OnTrackStartAsync(TrackStartEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg) {
            
            return Task.CompletedTask;
        }
        public static async Task OnTrackEndAsync(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            Logger.Instance?.LogDebug("Track ended!");
            if (arg.Reason != TrackEndReason.Finished) 
            {
                loopRemaining = 0;
                isOnLoop = false;
                Logger.Instance?.LogDebug("Queue finished!");
                await TalkingBotClient._client!.GetShardFor(arg.Player.TextChannel.Guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                return;
            }
            if(isOnLoop && (loopRemaining == -1 || loopRemaining > 0)) { // do loop
                await arg.Player.PlayAsync(arg.Track);
                loopRemaining -= (loopRemaining == -1) ? 0 : 1;
                if(loopRemaining == 0) isOnLoop = false;
                return;
            }
            if (!arg.Player.Vueue.TryDequeue(out var queueable)) 
            {
                loopRemaining = 0;
                isOnLoop = false;
                Logger.Instance?.LogDebug("Dequeue was not successful. Probably no tracks remaining.");
                await TalkingBotClient._client!.GetShardFor(arg.Player.TextChannel.Guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                return;
            }
            if (!(queueable is LavaTrack track))
            {
                loopRemaining = 0;
                isOnLoop = false;
                Logger.Instance?.LogWarning($"Next item in queue is not a track");
                await TalkingBotClient._client!.GetShardFor(arg.Player.TextChannel.Guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                return;
            }

            Logger.Instance?.LogDebug("Trying to play a new track!");

            await arg.Player.PlayAsync(track);

            await TalkingBotClient._client!.GetShardFor(arg.Player.TextChannel.Guild).SetActivityAsync(new Game(track.Title, 
                ActivityType.Listening, ActivityProperties.Join, track.Url));

            string thumbnail = $"https://img.youtube.com/vi/{track.Id}/0.jpg";
            var durstr = track.Duration.ToString("c");

            var embed = new EmbedBuilder()
                    .WithTitle($"{track.Title}")
                    .WithDescription($"Now playing [**{track.Title}**]({track.Url})")
                    .WithColor(0x0A90FA)
                    .WithThumbnailUrl(thumbnail)
                    .AddField("Duration", durstr, true)
                    .AddField("Video author", track.Author)
                    .Build();
            await arg.Player.TextChannel.SendMessageAsync(embed: embed);
        }
    }
}
