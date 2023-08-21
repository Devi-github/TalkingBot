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
using TalkingBot.Core.Caching;
using TalkingBot.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Drawing;

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
            handler.AddCommand(new() {
                name = "length",
                description = "Gets length of a current track",
                Handler = GetLen
            });
            handler.AddCommand(new() {
                name = "position",
                description = "Gets current position of a playing track",
                Handler = GetPosition
            });
            handler.AddCommand(new() {
                name = "version",
                description = "Gets current version of a bot",
                Handler = async (SocketSlashCommand cmd) => {
                    await RespondCommandAsync(cmd, new() {
                        message = AdditionalUtils.GetVersion()
                    });
                }
            });
            handler.AddCommand(new() {
                name = "loop",
                description = "Loops current track",
                Handler = Loop,
                options = new List<SlashCommandOption>() {
                    new() {
                        name = "times",
                        isRequired = false,
                        description = "How many times to loop",
                        optionType = ApplicationCommandOptionType.Integer
                    }
                }
            });
            handler.AddCommand(new() {
                name = "rolemsg",
                description = "Creates a message in the current channel that gives specified role on button click",
                Handler = RoleMsg,
                options = new List<SlashCommandOption>() {
                    new() {
                        name = "role",
                        description = "Role to give",
                        optionType = ApplicationCommandOptionType.Role,
                        isRequired = true
                    },
                    new() {
                        name = "message",
                        description = "Message to send",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = true
                    },
                    new() {
                        name = "label",
                        description = "Button label (title) to get role",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = true
                    }
                }
            });
            handler.AddCommand(new() {
                name = "embed",
                description = "Builds embed with specified parameters",
                Handler = BuildEmbed,
                options = new List<SlashCommandOption>() {
                    new() {
                        name = "title",
                        isRequired = true,
                        description = "Title of an embed",
                        optionType = ApplicationCommandOptionType.String
                    },
                    new() {
                        name = "description",
                        description = "A description for an embed",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = false
                    },
                    new() {
                        name = "color",
                        description = "The color for an embed in CSS format (#ffffff)",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = false
                    },
                    new() {
                        name = "image-url",
                        description = "Image url for an embed",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = false
                    },
                    new() {
                        name = "thumbnail-url",
                        description = "Thumbnail image url for an embed",
                        optionType = ApplicationCommandOptionType.String,
                        isRequired = false
                    },
                    new() {
                        name = "with-timestamp",
                        description = "Use current timestamp for an embed",
                        optionType = ApplicationCommandOptionType.Boolean,
                        isRequired = true
                    }
                }
            });
            handler.AddButtonHandler("add-role", AddRoleButton);

            return handler;
        }
#region Utils
        public static async Task AddRoleButton(SocketMessageComponent component) {
            var user = component.User as SocketGuildUser;
            var channel = component.Channel as SocketGuildChannel;
            var messageId = component.Message.Id;
            var role = channel.Guild
                .GetRole(TalkingBotClient._cached_message_role
                .FirstOrDefault(x => x.messageId == messageId).roleId);

            try {
                await user!.AddRoleAsync(role);
            } catch(Exception e) {
                await component.RespondAsync($"Error occured while giving role. "+
                "Probably the bot doesn't have enough permissions. Ask administrator "+
                "if you think this problem shouldn't exist.", ephemeral: true);
                return;
            }

            await component.RespondAsync($"You successfully got the role!", ephemeral: true);
        }
        private static async Task RespondCommandAsync(SocketSlashCommand command, InteractionResponse response)
        {
            await command.RespondAsync(response.message, isTTS: response.isTts, 
                ephemeral: response.ephemeral, embed: response.embed);
        }
        private static bool ComparePermissions(GuildPermissions targetGuildPerms, GuildPermissions userGuildPerms)
        {
            //True if the target has a higher role.
            bool targetHasHigherPerms = false;
            //If the user is not admin but target is.
            if(!userGuildPerms.Administrator && targetGuildPerms.Administrator) {
                //The target has higher permission than the user.
                targetHasHigherPerms = true;
            } else if(!userGuildPerms.ManageGuild && targetGuildPerms.ManageGuild) {
                targetHasHigherPerms = true;
            } else if(!userGuildPerms.ManageChannels && targetGuildPerms.ManageChannels) {
                targetHasHigherPerms = true;
            } else if(!userGuildPerms.BanMembers && targetGuildPerms.BanMembers) {
                targetHasHigherPerms = true;
            } else if(!userGuildPerms.KickMembers && targetGuildPerms.KickMembers) {
                targetHasHigherPerms = true;
            }

            return targetHasHigherPerms;
        }
        private static List<SocketSlashCommandDataOption> 
            GetListOfOptionsFromCommand(SocketSlashCommand command) 
        => command.Data.Options.ToList();

        private static SocketSlashCommandDataOption? 
            GetOptionDataFromOptionList(List<SocketSlashCommandDataOption> optionList, string name) 
        {
            var result = optionList.Find((x) => x.Name == name);
            return result;
        }
        private static Discord.Color? ParseColorFromString(string color) {
            var conv = new ColorConverter();
            var result = (System.Drawing.Color?)conv.ConvertFromString(color);
            if(result == null) return null;

            return new(result.Value.R, result.Value.G, result.Value.B);
        }
#endregion
#region Commands' handlers
        private static async Task RoleMsg(SocketSlashCommand command) {
            SocketRole role = command.Data.Options.ToList()[0].Value as SocketRole;
            string message = command.Data.Options.ToList()[1].Value as string;
            string buttonLabel = command.Data.Options.ToList()[2].Value as string;

            var author = command.User as SocketGuildUser;

            string newmessage = message!.Replace("  ", "\n");

            if(!author!.GuildPermissions.ManageRoles || !ComparePermissions(author!.GuildPermissions, role!.Permissions)) {
                
                await RespondCommandAsync(command, new() { message = "You don't have permissions " +
                "to perform this command!", ephemeral = true });
                return;
            }

            var actionRow = new ActionRowBuilder();

            var channel = command.Channel;

            var btn = new ButtonBuilder() {
                Label = buttonLabel,
                CustomId = $"add-role",
                Style = ButtonStyle.Primary
            };

            var components = new ComponentBuilder()
                .WithButton(btn);

            var msg = await channel.SendMessageAsync(text: newmessage, components: components.Build());
            
            await RespondCommandAsync(command, new() { message = "Message sent successfully!", ephemeral = true });

            var cacher = ServiceManager.GetService<Cacher<TalkingBotClient.CachedMessageRole>>();

            TalkingBotClient._cached_message_role.Add(new() { messageId = msg.Id, roleId = role.Id });
            TalkingBotClient.SaveCache();
        }
        private static async Task BuildEmbed(SocketSlashCommand command) {
            // 0 - title, 1 - description, 2 - color
            // 3 - imageURL, 4 - thumbnailURL, 5 - withTimestamp
            var list = GetListOfOptionsFromCommand(command);
            var title = GetOptionDataFromOptionList(list, "title")!;
            var description = GetOptionDataFromOptionList(list, "description");
            var color = GetOptionDataFromOptionList(list, "color");
            var imageURL = GetOptionDataFromOptionList(list, "image-url");
            var thumbnailURL = GetOptionDataFromOptionList(list, "thumbnail-url");
            var withTimestamp = GetOptionDataFromOptionList(list, "with-timestamp");

            var embed = new EmbedBuilder();

            embed.WithTitle((string)title.Value);
            if(description != null) embed.WithDescription((string)description.Value);
            if(color != null) {
                var col = ParseColorFromString((string)color.Value);
                if(col == null) {
                    await RespondCommandAsync(command,
                        new() { 
                            message = "Invalid color code provided!", 
                            ephemeral = true 
                        }
                    );
                    return;
                }
                embed.WithColor(col.Value);
            }
            if(imageURL != null) embed.WithImageUrl((string)imageURL.Value);
            if(thumbnailURL != null) embed.WithThumbnailUrl((string)thumbnailURL.Value);
            if((bool)withTimestamp!.Value == true) embed.WithCurrentTimestamp();

            await RespondCommandAsync(command, 
                new() { 
                    message = "In development", embed = embed.Build() 
                }
            );
        }
        private static async Task GetLen(SocketSlashCommand command) {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, AudioManager.GetLength(guild));
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
            if(command.Data.Options.ToList().Count == 2) { // TODO: this is even worse
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
            await RespondCommandAsync(command, AudioManager.RemoveTrack(guild, index));
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

            await RespondCommandAsync(command, new() { message = // TODO: Switch to using Random.Shared
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
        private static async Task GetPosition(SocketSlashCommand command) {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            await RespondCommandAsync(command, AudioManager.GetCurrentPosition(guild));
        }
        private static async Task Loop(SocketSlashCommand command) {
            var guild = TalkingBotClient._client.GetGuild(command.GuildId!.Value);
            int times = -1;
            if(command.Data.Options.Count != 0)
                times = Convert.ToInt32(command.Data.Options.ToList()[0].Value); // TODO: FIXME: this is bad. need to change it
            await RespondCommandAsync(command, AudioManager.SetLoop(guild, times));
        }
#endregion
    }
}
