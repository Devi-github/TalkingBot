using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Core.Music;
using TalkingBot.Utils;
using Victoria;
using Victoria.Rest;

namespace TalkingBot.Modules;

public class AudioModule(
    AudioManager audioManager
) : InteractionModuleBase
{
    private async Task RespondCommandAsync(InteractionResponse response)
    {
        await RespondAsync(
            response.message,
            isTTS: response.isTts,
            ephemeral: response.ephemeral,
            embed: response.embed
        );
    }

    [SlashCommand("join", "Makes the bot join the voice chat")]
    public async Task JoinAsync() {
        var voiceState = Context.User as IVoiceState;
        if(voiceState?.VoiceChannel == null || voiceState == null) {
            await ReplyAsync("You must be in a voice channel!");
            return;
        }
        await RespondCommandAsync(await audioManager.JoinAsync(Context.Guild, voiceState));
    }

    [SlashCommand("play", "Plays a track specified in query. Could be search in Soundcloud, or any link")]
    public async Task PlayAsync(
        [Summary("query", "Query to find or a URL")] string query,
        [Summary("timeCode", "Time code to start playing from. Example: 1:23")] string timeCode="0:00",
        [Summary("searchType", "Where to search for music (ignore if URL)")] SearchType searchType=SearchType.Soundcloud
        ) {
        if(string.IsNullOrEmpty(query)) {
            await ReplyAsync("Provide search query");
            return;
        }

        if(!AdditionalUtils.TryParseTimecode(timeCode, out double secs)) {
            await RespondCommandAsync(new() {message = "Failed to parse timecode! Format for the timecode is: 0:00", ephemeral = true});
            return;
        }

        var user = Context.User as IGuildUser;

        await RespondCommandAsync(await audioManager.PlayAsync(
            user!, Context.Guild, query, secs, searchType)
        );
    }

    [SlashCommand("stop", "Stops the currently playing track and clears the queue")]
    public async Task StopAsync() {
        await RespondCommandAsync(await audioManager.StopAsync(Context.Guild));
    }

    [SlashCommand("skip", "Skips currently playing track")]
    public async Task SkipAsync() {
        await RespondCommandAsync(await audioManager.SkipAsync(Context.Guild));
    }

    [SlashCommand("pause", "Pauses playback with ability to resume")]
    public async Task PauseAsync() {
        await RespondCommandAsync(await audioManager.PauseAsync(Context.Guild));
    }

    [SlashCommand("resume", "Resumes the playback if paused")]
    public async Task ResumeAsync() {
        await RespondCommandAsync(await audioManager.ResumeAsync(Context.Guild));
    }

    [SlashCommand("leave", "Makes the bot stop and leave the voice channel. The queue is cleared.")]
    public async Task LeaveAsync() {
        IVoiceState? voiceState = Context.User as IVoiceState;
        await RespondCommandAsync(await audioManager.LeaveAsync(voiceState!, Context.Guild));
    }

    [SlashCommand("volume", "Sets the volume of a currently playing song.")]
    public async Task SetVolumeAsync(
        [MinValue(1), MaxValue(100), Summary("volume", "Volume from 1 to 100")] long volume
    ) {
        await RespondCommandAsync(await audioManager.ChangeVolume(Context.Guild, (int)volume));
    }

    [SlashCommand("length", "Shows the length of the currently playing track")]
    public async Task GetLengthAsync() {
        await RespondCommandAsync(await audioManager.GetLength(Context.Guild));
    }

    [SlashCommand("position", "Shows the position of the currently playing track")]
    public async Task GetPositionAsync() {
        await RespondCommandAsync(await audioManager.GetCurrentPosition(Context.Guild));
    }

    [SlashCommand("loop", "Sets the loop for the current song")]
    public async Task SetLoop([MinValue(1), Summary("times", "Times to loop. Cannot be less than 1")] int times=-1) {
        await RespondCommandAsync(await audioManager.SetLoop(Context.Guild, times));
    }
    
    [SlashCommand("goto", "Continues playback from set timecode")]
    public async Task GoToAsync([Summary("timeCode", "Time code to seek to. Example: 1:23")] string timeCode) {
        if(!AdditionalUtils.TryParseTimecode(timeCode, out double secs)) {
            await RespondCommandAsync(new() {message = "Failed to parse timecode! Format for the timecode is: 0:00", ephemeral = true});
            return;
        }
        await RespondCommandAsync(await audioManager.GoToAsync(Context.Guild, secs));
    }

    [SlashCommand("queue", "Shows songs currently in queue")]
    public async Task GetQueueAsync() {
        await RespondCommandAsync(await audioManager.GetQueue(Context.Guild));
    }

    [SlashCommand("remove", "Removes a song from the queue")]
    public async Task RemoveSongAsync(
        [Summary("index", "Index of a song to remove (you can find it by running /queue)")] long index=-1) {
        await RespondCommandAsync(await audioManager.RemoveTrack(Context.Guild, index));
    }
}
