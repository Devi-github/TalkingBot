using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;
using TalkingBot.Utils;

namespace TalkingBot.Modules;

public class BotModule(ILogger<BotModule> logger) : InteractionModuleBase {
    
    [SlashCommand("version", "TODO")]
    public async Task GetVersionAsync() {
        await RespondAsync($"Current bot version: {AdditionalUtils.GetVersionString()}");
        logger.LogDebug("User requested Version");
    }

    [SlashCommand("ping", "TODO")]
    public async Task PingAsync() {
        await RespondAsync("Pong!");
        logger.LogDebug("User requested Ping");
    }

    [SlashCommand("roll", "TODO")]
    public async Task RollAsync(int limit=6) {
        await RespondAsync($"**{Random.Shared.NextInt64(limit) + 1:N0}**/{limit:N0}");
    }

    [SlashCommand("embed", "TODO")]
    public async Task BuildEmbedAsync(
        string title,
        string? _description=null,
        string? color=null,
        string? imageUrl=null,
        string? thumbnailUrl=null,
        bool withTimestamp=false,
        string? message=null
    ) {
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(_description)
            // .WithColor(color)
            .WithImageUrl(imageUrl)
            .WithThumbnailUrl(thumbnailUrl)
            .WithTimestamp(DateTime.Now);
        
        await RespondAsync(message, embed: embed.Build());
    }
}
