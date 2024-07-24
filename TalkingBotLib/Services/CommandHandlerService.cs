using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Modules;

namespace TalkingBot.Services;

using DiscordClient = DiscordShardedClient;

public class CommandHandlerService(
    InteractionService interactions,
    DiscordClient client,
    ILogger<CommandHandlerService> logger
) {
    ~CommandHandlerService() {
        interactions.Dispose();
    }

    public async Task InitializeAsync() {
        await interactions.AddModuleAsync<BotModule>(ServiceManager.ServiceProvider);
        await interactions.AddModuleAsync<AudioModule>(ServiceManager.ServiceProvider);
        await interactions.AddModuleAsync<ButtonHandlerModule>(ServiceManager.ServiceProvider);

        logger.LogInformation("Loaded interaction modules: {} modules", interactions.Modules.Count);

        client.ShardReady += async shard => {
            Stopwatch sw = new();
            var guild = shard.Guilds.First() ?? throw new Exception("Shard did not have guilds");

            sw.Restart();
            await interactions.RegisterCommandsToGuildAsync(guild.Id, true);
            sw.Stop();

            logger.LogInformation("Registered commands for guild: {}. {} seconds elapsed.",
                guild.Name, sw.Elapsed.TotalSeconds
            );
        };
        client.ButtonExecuted += ButtonExecutedAsync;
        client.InteractionCreated += InteractionExecutedAsync;
    }
    
    public async Task ButtonExecutedAsync(SocketMessageComponent interaction) {
        var guildId = interaction.GuildId ?? throw new Exception("Interaction did not have valid guild ID associated.");
        var shard = client.GetShardFor(client.GetGuild(guildId)) ??
            throw new Exception($"Failed to get shard for guild ID {guildId}");

        var ctx = new SocketInteractionContext<SocketMessageComponent>(shard, interaction);
        await interactions.ExecuteCommandAsync(ctx, ServiceManager.ServiceProvider);
    }

    public async Task InteractionExecutedAsync(SocketInteraction interaction) {
        try {
            var guildId = interaction.GuildId ?? throw new Exception("Interaction did not have valid guild ID associated.");
            var shard = client.GetShardFor(client.GetGuild(guildId));

            var context = new SocketInteractionContext(shard, interaction);
            var result = await interactions.ExecuteCommandAsync(context, ServiceManager.ServiceProvider);

            if(!result.IsSuccess) {
                await context.Channel.SendMessageAsync(result.ToString(), flags: MessageFlags.Ephemeral);
            }
        } catch {
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
            }
        }
    }
}