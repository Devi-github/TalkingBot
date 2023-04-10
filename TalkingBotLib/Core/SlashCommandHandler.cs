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
        public SlashCommandHandler() 
        {
            commands = new List<InternalSlashCommand>();
            commandHandlers = new List<Func<SocketSlashCommand, Task>>();
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
        public async Task BuildCommands(DiscordSocketClient client, ulong guildId)
        {
            var guild = client.GetGuild(guildId);

            foreach(var command in commands)
            {
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
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
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
    }
}
