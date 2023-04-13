using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;
using TalkingBot.Core.Music;
using TalkingBot.Utils;

namespace TalkingBot
{
    internal static class CommandsContainer
    {
        public static SlashCommandHandler BuildHandler()
        {
            SlashCommandHandler handler = new SlashCommandHandler();
            handler.AddCommand(new()
            {
                name = "join",
                description = "Bot joins a channel",
                Handler = JoinChannel
            });
            handler.AddCommand(new()
            {
                name = "play",
                description = "Sets a music to play",
                Handler = Play,
                options = new List<SlashCommandOption>
                {
                    new()
                    {
                        name = "query",
                        description = "Query or URL to find music by",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = true
                    },
                    new() {
                        name = "timecode",
                        description = "Timecode to start the song at (default: 0:00)",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = false
                    }
                }
            });
            handler.AddCommand(new()
            {
                name = "roll",
                description = "Rolls a number",
                Handler = Roll,
                options = new List<SlashCommandOption>
                {
                    new()
                    {
                        name = "limit",
                        description = "Limit to roll until (inclusive)",
                        optionType = ApplicationCommandOptionType.Integer
                    }
                }
            });
            handler.AddCommand(new()
            {
                name = "leave",
                description = "Leaves the voice channel",
                Handler = Leave,
            });
            handler.AddCommand(new()
            {
                name = "stop",
                description = "Stops the music and clears the queue",
                Handler = Stop
            });
            handler.AddCommand(new()
            {
                name = "pause",
                description = "Pauses the music",
                Handler = Pause
            });
            handler.AddCommand(new()
            {
                name = "unpause",
                description = "Resumes the music",
                Handler = Unpause
            });
            handler.AddCommand(new()
            {
                name = "ping",
                description = "Ping!",
                Handler = async (SocketSlashCommand cmd) =>
                {
                    await cmd.RespondAsync("Pong!");
                }
            });
            handler.AddCommand(new()
            {
                name = "skip",
                description = "Skips the currently playing song",
                Handler = Skip
            });
            handler.AddCommand(new()
            {
                name = "queue",
                description = "Displays current queue",
                Handler = Queue
            });
            handler.AddCommand(new()
            {
                name = "volume",
                description = "Changes the current volume of a player",
                Handler = Volume,
                options = new List<SlashCommandOption>
                {
                    new()
                    {
                        name = "volume",
                        description = "Specified volume from 0 to 100",
                        optionType = ApplicationCommandOptionType.Integer,
                        isRequired = true,
                    }
                }
            });
            handler.AddCommand(new()
            {
                name = "remove",
                description = "Removes the song at the index specified",
                Handler = RemoveSong,
                options = new List<SlashCommandOption>
                {
                    new()
                    {
                        name = "index",
                        description = "Song index in queue",
                        optionType = ApplicationCommandOptionType.Integer,
                        isRequired = true,
                    }
                }
            });
            handler.AddCommand(new() {
                name = "goto",
                description = "Goes to a specific timecode in a song",
                Handler = GotoFunc,
                options = new List<SlashCommandOption>
                {
                    new() {
                        name = "timecode",
                        description = "Timecode to go to",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = true
                    }
                }
            });

            return handler;
        }
        private static async Task RespondCommandAsync(SocketSlashCommand command, InteractionResponse response)
        {
            await command.RespondAsync(response.message, isTTS: response.isTts, 
                ephemeral: response.ephemeral, embed: response.embed);
        }
        private static async Task GotoFunc(SocketSlashCommand command) {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            string timecodeStr = (string)command.Data.Options.ToList()[0].Value; // TODO: FIXME: this is bad. need to change it
            bool success = AdditionalUtils.TryParseTimecode(timecodeStr, out double seconds);
            if(!success) {
                await RespondCommandAsync(command, new() {message = "Failed to parse timecode! Format for the timecode is: 0:00", ephemeral = true});
                return;
            }
            await RespondCommandAsync(command, await AudioManager.GoToAsync(guild, seconds));
        }
        private static async Task Volume(SocketSlashCommand command)
        {
            long volume = (long)command.Data.Options.ToList()[0].Value; // TODO: FIXME: this is bad. need to change it
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);

            await RespondCommandAsync(command, await AudioManager.ChangeVolume(guild, (int)volume));
        }
        private static async Task JoinChannel(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.JoinAsync(guild, command.User as IVoiceState, 
                command.Channel as ITextChannel));
        }

        private static async Task Play(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            string query = (string)command.Data.Options.ToList()[0].Value; // TODO: FIXME: this is bad. need to change it
            double secs = 0;
            if(command.Data.Options.ToList().Count == 2) {
                string timecodeStr = (string)command.Data.Options.ToList()[1].Value; // TODO: FIXME: this is bad. need to change it
                bool success = AdditionalUtils.TryParseTimecode(timecodeStr, out secs);
                if(!success) {
                    await RespondCommandAsync(command, new() {message = "Failed to parse timecode! Format for the timecode is: 0:00", ephemeral = true});
                    return;
                }
            }
            await RespondCommandAsync(command, await AudioManager.PlayAsync(command.User as SocketGuildUser, 
                command.Channel as ITextChannel, guild, query, secs));
        }
        private static async Task RemoveSong(SocketSlashCommand command)
        {
            long index = (long)command.Data.Options.ToList()[0].Value;
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, AudioManager.RemoveTrack(guild, Convert.ToInt32(index)));
        }
        private static async Task Leave(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.LeaveAsync(guild));
        }
        private static async Task Pause(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.PauseAsync(guild));
        }
        private static async Task Unpause(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.ResumeAsync(guild));
        }
        private static async Task Stop(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.StopAsync(guild));
        }
        private static async Task Roll(SocketSlashCommand command)
        {
            long limit = 100;
            if(command.Data.Options.Count != 0)
                limit = (long)command.Data.Options.ToList()[0].Value; // TODO: FIXME: this is bad. need to change it

            await RespondCommandAsync(command, new() { message = 
                $"**{(RandomStatic.NextInt64(limit) + 1).ToString("N0")}**/{limit.ToString("N0")}" });
        }
        private static async Task Skip(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, await AudioManager.SkipAsync(guild));
        }
        private static async Task Queue(SocketSlashCommand command)
        {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, AudioManager.GetQueue(guild));
        }
    }
}
