using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using TalkingBot.Core.Logging;

namespace TalkingBot.Core
{
    public struct SlashCommandOption
    {
        public string name;
        public string description;
        public ApplicationCommandOptionType optionType;
        public bool? isRequired;
        public bool? isDefault;
        public bool isAutocomplete;
        public double? minValue;
        public double? maxValue;
    }
    public struct SlashCommand
    {
        public string name;
        public string description;
        public List<SlashCommandOption>? options;
        public Func<SocketSlashCommand, Task> Handler;
    }
    struct InternalSlashCommand
    {
        public string name;
        public string description;
        public List<SlashCommandOption>? options;
        public int handlerId;
    }
    public class SlashCommandHandler
    {
        private List<InternalSlashCommand> commands;
        private List<Func<SocketSlashCommand, Task>> commandHandlers;
        private Dictionary<string, Func<SocketMessageComponent, Task>> buttonHandlers;
        public SlashCommandHandler() 
        {
            commands = new List<InternalSlashCommand>();
            commandHandlers = new List<Func<SocketSlashCommand, Task>>();
            buttonHandlers = new Dictionary<string, Func<SocketMessageComponent, Task>>();
        }
        public SlashCommandHandler AddCommand(SlashCommand command)
        {
            InternalSlashCommand cmd = new()
            {
                name = command.name,
                description = command.description,
                handlerId = commandHandlers.Count,
                options = command.options,
            };
            commands.Add(cmd);
            commandHandlers.Add(command.Handler);
            return this;
        }
        public int GetLength()
            => commands.Count;
        public void AddButtonHandler(string buttonId, Func<SocketMessageComponent, Task> handler) {
            buttonHandlers.Add(buttonId, handler);
        }
        public async Task BuildCommands(DiscordSocketClient client, ulong guildId, bool forceUpdateCommands=false)
        {
            var guild = client.GetGuild(guildId);
            var rest = client.Rest;
            var existingCommands = await rest.GetGuildApplicationCommands(guildId);

            Stopwatch sw = new();

            if(forceUpdateCommands) {
                Console.WriteLine("Forcefully overwriting commands for {0} ({1}).", guild.Name, guildId);
                await rest.BulkOverwriteGuildCommands(new ApplicationCommandProperties[0] {}, guildId);
            }

            foreach(var command in commands)
            {
                if(!forceUpdateCommands && 
                    existingCommands.ToList().Find(x => x.Name == command.name) is not null) {
                    Logger.Instance?.LogDebug($"Skipped '{command.name}' because it already exists in {guild.Name} ({guild.Id})");
                    continue;
                }
                sw.Restart();
                
                var guildCommand = new SlashCommandBuilder()
                    .WithName(command.name)
                    .WithDescription(command.description);
                
                if (command.options is not null)
                {
                    foreach (var option in command.options)
                    {
                        guildCommand.AddOption(option.name, option.optionType,
                            option.description, option.isRequired,
                            option.isDefault, option.isAutocomplete,
                            option.minValue, option.maxValue);
                    }
                }
                try
                {
                    await rest.CreateGuildCommand(guildCommand.Build(), guildId);
                    sw.Stop();
                    Logger.Instance?.LogDebug($"Command '{guildCommand.Name}' built in {sw.Elapsed.TotalSeconds}s for " + 
                    $"{guild.Name} ({guild.Id})");
                }
                catch (HttpException e)
                {
                    var json = JsonConvert.SerializeObject(e.Message, Formatting.Indented);
                    Logger.Instance?.LogError(json);
                }
            }
        }
        public async Task HandleCommands(SocketSlashCommand command)
        {
            if (Logger.Instance is null) throw new NullReferenceException("Logger was null when accessed");
            Logger.Instance?.Log(LogLevel.Debug, $"'{command.CommandName}' was " +
                $"executed by {command.User.Username} at #{command.Channel.Name}");

            InternalSlashCommand cmdToExecute = commands.Find(x => x.name == command.CommandName);

            await commandHandlers[cmdToExecute.handlerId](command);
        }
        public async Task HandleButtons(SocketMessageComponent component) {
            if (Logger.Instance is null) throw new NullReferenceException("Logger was null when accessed");

            

            await buttonHandlers[component.Data.CustomId](component);
        }
    }
}
