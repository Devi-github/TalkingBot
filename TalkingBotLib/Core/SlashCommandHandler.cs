using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public List<SlashCommandOption> options;
        public Func<SocketSlashCommand, Task> Handler;
    }
    public class SlashCommandHandler
    {
        private List<SlashCommand> commands;
        public SlashCommandHandler() 
        {
            commands = new List<SlashCommand>();
        }
        public SlashCommandHandler AddCommand(SlashCommand command)
        {
            commands.Add(command);
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

                foreach(var option in command.options)
                {
                    guildCommand.AddOption(option.name, option.optionType, 
                        option.description, option.isRequired,
                        option.isDefault, option.isAutocomplete, 
                        option.minValue, option.maxValue);

                    try
                    {
                        await guild.CreateApplicationCommandAsync(guildCommand.Build());
                    } catch(HttpException e)
                    {
                        var json = JsonConvert.SerializeObject(e.Message, Formatting.Indented);
                        Console.Error.WriteLine(json);
                    }
                }
            }
        }
        public async Task HandleCommands(SocketSlashCommand command)
        {
            Console.WriteLine(command.CommandName);

            SlashCommand cmdToExecute = commands.Find(x => x.name == command.CommandName);

            await cmdToExecute.Handler(command);
        }
    }
}
