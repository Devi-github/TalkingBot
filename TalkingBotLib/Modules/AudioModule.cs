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
    LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode,
    AudioManager audioManager,
    ILogger<AudioModule> logger
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

    [SlashCommand("join", "Joins voice chat")]
    public async Task JoinAsync() {
        var voiceState = Context.User as IVoiceState;
        if(voiceState?.VoiceChannel == null || voiceState == null) {
            await ReplyAsync("You must be in a voice channel!");
            return;
        }
        await RespondCommandAsync(await audioManager.JoinAsync(Context.Guild, voiceState, null));
    }

    [SlashCommand("play", "TODO")]
    public async Task PlayAsync(string query, string timeCode="0:00") {
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
            user!, null, Context.Guild, query, secs));
    }

    [SlashCommand("stop", "TODO")]
    public async Task StopAsync() {
        await RespondCommandAsync(await audioManager.StopAsync(Context.Guild));
    }

    [SlashCommand("skip", "TODO")]
    public async Task SkipAsync() {
        await RespondCommandAsync(await audioManager.SkipAsync(Context.Guild));
    }

    [SlashCommand("pause", "TODO")]
    public async Task PauseAsync() {
        await RespondCommandAsync(await audioManager.PauseAsync(Context.Guild));
    }

    [SlashCommand("resume", "TODO")]
    public async Task ResumeAsync() {
        await RespondCommandAsync(await audioManager.ResumeAsync(Context.Guild));
    }

    [SlashCommand("leave", "TODO")]
    public async Task LeaveAsync() {
        IVoiceState? voiceState = Context.User as IVoiceState;
        await RespondCommandAsync(await audioManager.LeaveAsync(voiceState!, Context.Guild));
    }

    [SlashCommand("volume", "TODO")]
    public async Task SetVolumeAsync(long volume) {
        await RespondCommandAsync(await audioManager.ChangeVolume(Context.Guild, (int)volume));
    }

    [SlashCommand("length", "TODO")]
    public async Task GetLengthAsync() {
        await RespondCommandAsync(await audioManager.GetLength(Context.Guild));
    }

    [SlashCommand("position", "TODO")]
    public async Task GetPositionAsync() {
        await RespondCommandAsync(await audioManager.GetCurrentPosition(Context.Guild));
    }

    [SlashCommand("loop", "TODO")]
    public async Task SetLoop(int times=-1) {
        await RespondCommandAsync(await audioManager.SetLoop(Context.Guild, times));
    }
    
    [SlashCommand("goto", "TODO")]
    public async Task GoToAsync([Summary("timeCode", "Time code to seek to. Example: 1:23")] string timeCode) {
        if(!AdditionalUtils.TryParseTimecode(timeCode, out double secs)) {
            await RespondCommandAsync(new() {message = "Failed to parse timecode! Format for the timecode is: 0:00", ephemeral = true});
            return;
        }
        await RespondCommandAsync(await audioManager.GoToAsync(Context.Guild, secs));
    }

    [SlashCommand("queue", "TODO")]
    public async Task GetQueueAsync() {
        await RespondCommandAsync(await audioManager.GetQueue(Context.Guild));
    }

    [SlashCommand("remove", "TODO")]
    public async Task RemoveSongAsync(long index=-1) {
        await RespondCommandAsync(await audioManager.RemoveTrack(Context.Guild, index));
    }
}
