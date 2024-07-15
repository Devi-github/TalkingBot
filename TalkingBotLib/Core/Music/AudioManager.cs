using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;
using Victoria;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Utils;
using Victoria.WebSocket.EventArgs;

namespace TalkingBot.Core.Music
{
    public class AudioManager
    {
        private DiscordSocketClient _client;
        private LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
        private ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
        private ILogger<AudioManager> _logger;
        public HashSet<ulong> VoteQueue;
        public AudioManager() 
        {
            _client = ServiceManager.GetService<DiscordSocketClient>();
            _lavaNode = ServiceManager.GetService<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>>();
            _logger = ServiceManager.GetService<ILogger<AudioManager>>();

            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
            
            VoteQueue = [];

            _lavaNode.OnTrackEnd += OnTrackEndAsync;
            // _lavaNode.OnTrackStart += OnTrackStartAsync;
            // _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            // _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            // _lavaNode.OnTrackException += OnTrackExceptionAsync;
        }

        private static bool isOnLoop = false;
        private static int loopRemaining = 0;

        public async Task<InteractionResponse> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            // if(_lavaNode.HasPlayer(guild))
            if((await TryGetVoiceState(guild))?.VoiceChannel is not null)
                return new() { message = "I am already connected to a vc", ephemeral = true };
            
            if (voiceState.VoiceChannel is null) return new() { message = "You must be connected to a vc", ephemeral = true };
            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel);
                return new() { message = $"Connected to a {voiceState.VoiceChannel.Name}" };
            } catch(Exception ex)
            {
                return new() { message = $"Error\n{ex.Message}" , ephemeral = true};
            }
        }
        public async Task<InteractionResponse> GoToAsync(IGuild guild, double seconds) {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();
            try
            {
                if(player.Track is null) 
                    return new() {
                        message = "Bot is not playing! To go to a timestamp you have to have a song playing!",
                        ephemeral = true
                    };
                if(!player.Track.IsSeekable) return new() {
                    message = "Cannot go to any position on this track!",
                    ephemeral = true
                };

                // Bug when seeking doesn't allow timecodes of 0
                // The payload sent becomes empty
                // So there is this workaround
                if(seconds == 0.0) {
                    // This way something will be in the payload, tested 💯
                    seconds = 0.001;
                }

                TimeSpan timecode = TimeSpan.FromSeconds(seconds);
                if(player.Track.Duration < timecode) return new() { message = "The timecode is outside of track's length!", ephemeral = true};
                _logger.LogDebug("Trying to seek to {} seconds", seconds);
                await player.SeekAsync(_lavaNode, timecode);

                return new() {
                    message = $"Skipped to {timecode.ToString("c")}"
                };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> SetLoop(IGuild guild, int times) {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();
            
            if(times == 0 || times < -1) return new() { message = "Cannot loop negative or zero times", ephemeral = true };

            try {
                if (!player.State.IsConnected || player.Track is null) return new() {
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
        public async Task<InteractionResponse> PlayAsync(IGuildUser user, 
            ITextChannel channel, IGuild guild, string query, double seconds=0)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            if (user.VoiceChannel is null) return new() { message = "You must be connected to a vc", ephemeral = true };

            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
            {
                try
                {
                    await _lavaNode.JoinAsync(user.VoiceChannel);
                } catch(Exception ex)
                {
                    return new() { message = $"Error\n{ex.Message}", ephemeral = true };
                }
            }

            try
            {
                var player = await _lavaNode.GetPlayerAsync(guild.Id);
                
                LavaTrack track;

                var trackSearchResponse = await _lavaNode.LoadTrackAsync(query);
                
                if(trackSearchResponse.Type is Victoria.Rest.Search.SearchType.Empty or Victoria.Rest.Search.SearchType.Error) {
                    return new() { message = "Couldn't find anything.", ephemeral = true };
                }

                track = trackSearchResponse.Tracks.FirstOrDefault()!;
                string? thumbnail = track.Artwork;
                
                var durstr = track.Duration.ToString("c");

                if (player.Track is not null)
                {
                    player.GetQueue().Enqueue(track);

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

                await player.PlayAsync(_lavaNode, track);
                await player.SeekAsync(_lavaNode, timecode);
                
                // await _client.GetShardFor(guild).SetActivityAsync(
                //     new Game(track.Title, ActivityType.Listening, ActivityProperties.Join, track.Url));
                
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
        public async Task<InteractionResponse> LeaveAsync(IVoiceState voiceState, IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();

            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            var playerVoiceState = await TryGetVoiceState(guild);
            if (playerVoiceState?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.Track is not null)
                    await StopAsync(guild);
                await _lavaNode.LeaveAsync(playerVoiceState.VoiceChannel);

                loopRemaining = 0;
                isOnLoop = false;

                // await _client.GetShardFor(guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));

                return new() { message = $"I have left the vc" };
            } catch(Exception e)
            {
                return new() { message = $"Error\n{e}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> StopAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.Track is null)
                    return new() { message = $"Music is already stopped" };

                await player.StopAsync(_lavaNode, player.Track);
                player.GetQueue().Clear();

                // await _client.GetShardFor(guild).SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));

                return new() { message = $"Stopped playing the music and cleared the queue" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> PauseAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.IsPaused) 
                    return new() { message = $"Music is already paused", ephemeral = true};
                if (player.Track is null) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                await player.PauseAsync(_lavaNode);

                return new() { message = $"Paused the music" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> ResumeAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.Track is null) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };
                else if(!player.IsPaused)
                    return new() { message = $"Music is already playing" };

                await player.ResumeAsync(_lavaNode, player.Track);

                return new() { message = $"Resumed the music" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> RemoveTrack(IGuild guild, long index)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                LavaTrack trackRemoved;

                // Remove last
                if(index == -1) {
                    trackRemoved = player.GetQueue().Last();
                    player.GetQueue().Remove(trackRemoved);
                    return new() { message = $"Removed the track **{trackRemoved.Title}**" };
                }

                if (index - 1 < 0 || index > player.GetQueue().Count) return new() 
                { 
                    message = $"Index is not present inside the Queue. Enter values from (1 to {player.GetQueue().Count})",
                    ephemeral = true 
                };

                trackRemoved = player.GetQueue().RemoveAt((int)(index - 1));

                return new() { message = $"Removed the track **{trackRemoved.Title}**" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> SkipAsync(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.Track is null) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };
                else if(player.IsPaused)
                    return new() { message = $"Music is paused. Resume to skip." };
                else if (player.GetQueue().Count == 0)
                    return new() { message = $"Only currently playing song is in the queue. " +
                        $"You can stop the playback using `/stop` or `/leave`" };
                
                await player.SkipAsync(_lavaNode);

                // await _client.GetShardFor(guild).SetActivityAsync( // FIXME: This doesn't get set properly because of how SkipAsync works #11
                //     new Game(player.Track.Title, ActivityType.Listening, ActivityProperties.Join, player.Track.Url));
                
                // string thumbnail = $"https://img.youtube.com/vi/{player.Track.Id}/0.jpg";
                string thumbnail = player.Track.Artwork;

                // TODO: Move to different place, like in some kind of PlayingNewTrack event
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
        public async Task<InteractionResponse> GetCurrentPosition(IGuild guild) {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try {
                if (player.Track is null) 
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                string curpos = $"{player.Track.Position.Hours.ToString("00")}:"+
                $"{player.Track.Position.Minutes.ToString("00")}:{player.Track.Position.Seconds.ToString("00")}";

                return new() { message = $"Current track position: **{curpos}**/{player.Track.Duration.ToString("c")}", ephemeral = true };
            } catch(Exception ex) {
                return new() { message = $"Error\n{ex.Message}", ephemeral = true };
            }
        }
        public async Task<InteractionResponse> GetQueue(IGuild guild)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if (player.Track is null)
                    return new() { message = $"No songs in queue. Add a song with `/play` command" };

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Queue")
                    .WithDescription("This is a list of all tracks on this server currently")
                    .AddField("**Currently playing**", $"[**{player.Track.Title}**]({player.Track.Url})", false)
                    .WithColor(0x10FF90);

                int i = 1;
                foreach(LavaTrack track in player.GetQueue())
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
        public async Task<InteractionResponse> GetLength(IGuild guild) {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                if(player.Track is null)
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
        public async Task<InteractionResponse> ChangeVolume(IGuild guild, int volume)
        {
            if(!_lavaNode.IsConnected) return LavalinkFailedMessage();
            var player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if ((await TryGetVoiceState(guild))?.VoiceChannel is null)
                return PlayerNotConnectedMessage();

            try
            {
                volume = volume <= 100 ? (volume >= 0 ? volume : 0) : 100;

                await player.SetVolumeAsync(_lavaNode, volume);

                return new() { message = $"Changed volume to **{volume}**/100" };
            }
            catch (Exception e)
            {
                return new() { message = $"Error\n{e.Message}", ephemeral = true };
            }
        }
        private async Task OnTrackExceptionAsync(TrackExceptionEventArg arg)
        {
            var player = await _lavaNode.GetPlayerAsync(arg.GuildId);
            player.GetQueue().Enqueue(arg.Track);
            // await arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it threw an exception."); // TODO
        }
        private async Task OnTrackStuckAsync(TrackStuckEventArg arg)
        {
            // var guild = TalkingBotClient._client!.GetGuild(arg.GuildId);
            var player = await _lavaNode.GetPlayerAsync(arg.GuildId);
            player.GetQueue().Enqueue(arg.Track);
            // await player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it got stuck."); // TODO
        }
        private Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            _logger.LogError($"{arg.Code} {arg.Reason}");
            return Task.CompletedTask;
        }
        public Task OnTrackStartAsync(TrackStartEventArg arg) {
            
            return Task.CompletedTask;
        }
        public async Task OnTrackEndAsync(TrackEndEventArg arg)
        {
            var player = await _lavaNode.TryGetPlayerAsync(arg.GuildId);
            _logger.LogDebug("Track ended!");

            if(player is null) {
                _logger.LogDebug("Player was null. Unable to continue playback.");
                return;
            }

            if (arg.Reason != Victoria.Enums.TrackEndReason.Finished)
            {
                loopRemaining = 0;
                isOnLoop = false;
                _logger.LogDebug("Queue finished!");
                
                // var shard = _client.GetShardFor(guild);
                // await shard.SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                
                return;
            }
            if(isOnLoop && (loopRemaining == -1 || loopRemaining > 0)) { // do loop
                await player.PlayAsync(_lavaNode, arg.Track);
                loopRemaining -= (loopRemaining == -1) ? 0 : 1;
                if(loopRemaining == 0) isOnLoop = false;
                return;
            }
            if (!player.GetQueue().TryDequeue(out var queueable)) 
            {
                loopRemaining = 0;
                isOnLoop = false;
                _logger.LogDebug("Dequeue was not successful. Probably no tracks remaining.");
                // await _client.GetShardFor(guild)
                //     .SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                return;
            }
            if (queueable is not LavaTrack track)
            {
                loopRemaining = 0;
                isOnLoop = false;
                _logger.LogWarning($"Next item in queue is not a track");
                // await _client.GetShardFor(guild)
                //     .SetActivityAsync(new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
                return;
            }

            _logger.LogDebug("Trying to play a new track!");

            await player.PlayAsync(_lavaNode, track);

            // await _client.GetShardFor(guild)
            //     .SetActivityAsync(new Game(track.Title, ActivityType.Listening, ActivityProperties.Join, track.Url));

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
            
            // await player.TextChannel.SendMessageAsync(embed: embed); // TODO
        }

        private static InteractionResponse LavalinkFailedMessage() =>
            new() {
                message = "Music service is now unavailable! Contact administrator if you have any questions.",
                ephemeral = true 
            };
        
        private static InteractionResponse PlayerNotConnectedMessage() =>
            new() { message = $"Not connected to any voice channel!", ephemeral = true };

        private async Task<IVoiceState?> TryGetVoiceState(IGuild guild) {
            LavaPlayer<LavaTrack>? player = await _lavaNode.TryGetPlayerAsync(guild.Id);
            if(player is null) return null;
            IVoiceState? voiceState = await guild.GetCurrentUserAsync();
            return voiceState;
        }
    }
}
