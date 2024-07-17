using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Core.Caching;
using TalkingBot.Services;
using TalkingBot.Utils;

namespace TalkingBot.Modules;

public class ButtonHandlerModule(
    MessageCacher cacher, ILogger<ButtonHandlerModule> logger
) : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>> {
    [ComponentInteraction("add-role")]
    public async Task AddRoleButton() {
        var user = Context.User as SocketGuildUser;
        var channel = Context.Channel as SocketGuildChannel;
        var messageId = Context.Interaction.Message.Id;
        var role = channel!.Guild
            .GetRole(cacher.CachedMessages.FirstOrDefault(x => x.messageId == messageId).roleId);
        
        if(role is null) {
            await RespondAsync("Failed to give a role. Ask administrator to "+
                "fix this problem.");
            return;
        }
        
        try {
            await user!.AddRoleAsync(role);
        } catch(Exception) {
            await RespondAsync("Error occured while giving role. "+
                "Probably the bot doesn't have enough permissions. Ask administrator "+
                "if you think this problem shouldn't exist.", ephemeral: true);
            return;
        }

        await RespondAsync($"You successfully got the role {role.Name}!", ephemeral: true);

        logger.LogDebug("{} executed AddRoleButton", user.DisplayName);
    }
}

public class BotModule(
    MessageCacher cacher, ILogger<BotModule> logger
) : InteractionModuleBase {
    
    [SlashCommand("version", "Prints current version of the bot")]
    public async Task GetVersionAsync() {
        await RespondAsync($"Current bot version: {AdditionalUtils.GetVersionString()}");
        logger.LogDebug("User requested Version");
    }

    [SlashCommand("ping", "Ping!")]
    public async Task PingAsync() {
        await RespondAsync("Pong!");
        logger.LogDebug("User requested Ping");
    }

    [SlashCommand("roll", "Rolls a random number")]
    public async Task RollAsync([Summary("limit", "Max number that can be rolled")] int limit=6) {
        await RespondAsync($"**{Random.Shared.NextInt64(limit) + 1:N0}**/{limit:N0}");
    }

    [SlashCommand("role-msg", "Creates a message to get a role")]
    public async Task RoleMsg(
        [Summary("role", "Role to give")] IRole role,
        [Summary("message", "Message to add button to")] string message,
        [Summary("label", "Text on button")] string buttonLabel
    ) {
        var author = Context.User as IGuildUser;

        string newmessage = message!.Replace("  ", "\n");

        if(!author!.GuildPermissions.ManageRoles || author.Hierarchy <= role.Position) {
            await RespondAsync("You don't have permissions " +
                "to perform this command!", ephemeral: true);
            return;
        }

        var channel = Context.Channel;

        var btn = new ButtonBuilder() {
            Label = buttonLabel,
            CustomId = $"add-role",
            Style = ButtonStyle.Primary
        };

        var components = new ComponentBuilder()
            .WithButton(btn);

        var msg = await channel.SendMessageAsync(text: newmessage, components: components.Build());
        
        await RespondAsync("Message sent successfully!", ephemeral: true);

        cacher.CachedMessages.Add(new() { messageId = msg.Id, roleId = role.Id });
        cacher.SaveCache();
    }

    [SlashCommand("embed", "Builds an embed for your tastes")]
    public async Task BuildEmbedAsync(
        [Summary("title", "Title of an embed")] string title,
        [Summary("description", "Description of an embed")] string? _description=null,
        [Summary("color", "Color of an embed in '#123456' format")] string? color=null,
        [Summary("imageUrl", "URL for an image in embed")] string? imageUrl=null,
        [Summary("thumbnailUrl", "URL for a thumbnail in embed")] string? thumbnailUrl=null,
        [Summary("withTimestamp", "Show timestamp for when embed was created")] bool withTimestamp=false,
        [Summary("message", "Additional message outside of an embed")] string? message=null
    ) {
        var embed = new EmbedBuilder()
            .WithTitle(title);
        
        embed = _description is not null ? embed.WithDescription(_description) : embed;
        if(color is not null) {
            Color? colorStruct = AdditionalUtils.ParseColorFromString(color);

            embed = colorStruct is not null ? embed.WithColor(colorStruct.Value) : embed;
        }
        embed = withTimestamp ? embed.WithTimestamp(DateTime.Now) : embed;
        embed = imageUrl is not null ? embed.WithImageUrl(imageUrl) : embed;
        embed = thumbnailUrl is not null ? embed.WithThumbnailUrl(thumbnailUrl) : embed;
        
        await RespondAsync(message, embed: embed.Build());
    }
}
