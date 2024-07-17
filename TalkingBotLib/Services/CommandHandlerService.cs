using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Modules;

namespace TalkingBot.Services;

public class CommandHandlerService(
    InteractionService interactions,
    DiscordSocketClient client,
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

        client.Ready += async () => {
            Stopwatch sw = new();
            foreach(var guild in ServiceManager.GetService<TalkingBotConfig>().Guilds!) {
                sw.Restart();
                await interactions.RegisterCommandsToGuildAsync(guild, true);
                sw.Stop();

                logger.LogInformation("Registered commands for guild: {}. {} seconds elapsed.",
                    client.GetGuild(guild).Name, sw.Elapsed.TotalSeconds
                );
            }
        };
        client.ButtonExecuted += ButtonExecutedAsync;
        client.InteractionCreated += InteractionExecutedAsync;
    }
    
    public async Task ButtonExecutedAsync(SocketMessageComponent interaction) {
        var ctx = new SocketInteractionContext<SocketMessageComponent>(client, interaction);
        await interactions.ExecuteCommandAsync(ctx, ServiceManager.ServiceProvider);
    }

    public async Task InteractionExecutedAsync(SocketInteraction interaction) {
        try {
            var context = new SocketInteractionContext(client, interaction);
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