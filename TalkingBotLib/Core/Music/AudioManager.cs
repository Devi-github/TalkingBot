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

namespace TalkingBotLib.Core.Music
{
    public class AudioManager
    {
        private static LavaNode _lavaNode = ServiceManager.ServiceProvider.GetRequiredService<LavaNode>();
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
        public readonly HashSet<ulong> VoteQueue;
        public AudioManager() 
        { 
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            VoteQueue = new HashSet<ulong>();

            _lavaNode.OnTrackEnd += OnTrackEndAsync;
            _lavaNode.OnTrackStart += OnTrackStartAsync;
            _lavaNode.OnStatsReceived += OnStatsReceivedAsync;
            _lavaNode.OnUpdateReceived += OnUpdateReceivedAsync;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            _lavaNode.OnTrackException += OnTrackExceptionAsync;
        }
        public static async Task<string> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if(_lavaNode.HasPlayer(guild))
            {
                return "I am already connected to a vc";
            }
            if (voiceState.VoiceChannel is null) return "You must be connected to a vc";
            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                return $"Connected to a {voiceState.VoiceChannel.Name}";
            } catch(Exception ex)
            {
                return $"Error\n{ex.Message}";
            }
        }
        public static async Task<string> PlayAsync(SocketGuildUser user, IGuild guild, string query)
        {
            if (user.VoiceChannel is null) return "You must be connected to a vc";

            if (_lavaNode.HasPlayer(guild)) return "I am already connected to a vc";

            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out LavaPlayer<LavaTrack> player);
                if (!success) throw new Exception("Player get failed. Idk what is the problem");

                LavaTrack track;

                var search = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(query, UriKind.Absolute) ? 
                    Victoria.Responses.Search.SearchType.Direct : 
                    Victoria.Responses.Search.SearchType.YouTube, query);

                if (search.Status == Victoria.Responses.Search.SearchStatus.NoMatches) return $"Could not find anything for '{query}'";

                track = search.Tracks.FirstOrDefault();

                //if(player.Track != null && player.PlayerState is PlayerState.Playing ||  player.PlayerState is PlayerState.Paused)
                //{
                //    await player.PlayAsync(track);
                //    Console.WriteLine($"[{DateTime.Now}] (AUDIO) Track is playing");
                //    return $"{track.Title} is playing now";
                //}
                await player.PlayAsync(track);

                return $"Now playing {track.Title}";
            } catch(Exception e)
            {
                return $"Error\n{e.Message}";
            }
        }
        public static async Task<string> LeaveAsync(IGuild guild)
        {
            try
            {
                var success = _lavaNode.TryGetPlayer(guild, out var player);
                if (!success) throw new Exception("Player get failed. Probably not connected");
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                Console.WriteLine($"[{DateTime.Now}] (AUDIO) Bot left the channel");
                return $"I have left the vc";
            } catch(Exception e)
            {
                return $"Error\n{e}";
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

        private Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            Console.Error.WriteLine($"{arg.Code} {arg.Reason}");
            return Task.CompletedTask;
        }

        private Task OnStatsReceivedAsync(StatsEventArg arg)
        {
            Console.WriteLine(JsonConvert.SerializeObject(arg));
            return Task.CompletedTask;
        }

        private static Task OnUpdateReceivedAsync(UpdateEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            return arg.Player.TextChannel.SendMessageAsync(
                $"Player update received: {arg.Position}/{arg.Track?.Duration}");
        }
        public static Task OnTrackStartAsync(TrackStartEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            return arg.Player.TextChannel.SendMessageAsync($"Started playing {arg.Track}");
        }
        public static Task OnTrackEndAsync(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            return arg.Player.TextChannel.SendMessageAsync($"Finished playing {arg.Track}");
        }
    }
}
