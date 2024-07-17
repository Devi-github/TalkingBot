using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Modules;

namespace TalkingBot.Services;

public class CommandHandlerService {
    private readonly InteractionService _interactions;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<InteractionService> _logger;

    public CommandHandlerService() {
        _interactions = ServiceManager.GetService<InteractionService>();
        _client = ServiceManager.GetService<DiscordSocketClient>();
        _logger = ServiceManager.GetService<ILogger<InteractionService>>();
    }

    ~CommandHandlerService() {
        _interactions.Dispose();
    }

    public async Task InitializeAsync() {
        // IEnumerable<ModuleInfo> modules = 
        //     await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceManager.ServiceProvider);
        
        await _interactions.AddModuleAsync<BotModule>(ServiceManager.ServiceProvider);
        await _interactions.AddModuleAsync<AudioModule>(ServiceManager.ServiceProvider);
        await _interactions.AddModuleAsync<ButtonHandlerModule>(ServiceManager.ServiceProvider);

        _logger.LogInformation("Loaded interaction modules: {} modules", _interactions.Modules.Count);

        _client.Ready += async () => {
            Stopwatch sw = new();
            foreach(var guild in ServiceManager.GetService<TalkingBotConfig>().Guilds!) {
                sw.Restart();
                await _interactions.RegisterCommandsToGuildAsync(guild, true);
                sw.Stop();

                _logger.LogInformation("Registered commands for guild: {}. {} seconds elapsed.",
                    _client.GetGuild(guild).Name, sw.Elapsed.TotalSeconds
                );
            }
        };
        _client.ButtonExecuted += ButtonExecutedAsync;
        _client.InteractionCreated += InteractionExecutedAsync;
    }
    
    public async Task ButtonExecutedAsync(SocketMessageComponent interaction) {
        var ctx = new SocketInteractionContext<SocketMessageComponent>(_client, interaction);
        await _interactions.ExecuteCommandAsync(ctx, ServiceManager.ServiceProvider);
    }

    public async Task InteractionExecutedAsync(SocketInteraction interaction) {
        try {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, ServiceManager.ServiceProvider);

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