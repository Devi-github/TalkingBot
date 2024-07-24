using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;

namespace TalkingBot
{
    public static class Config
    {
        public static async Task CreateDefaultConfigNoIO(string cnfpath) {
            TalkingBotConfig config = new() {
                LavalinkHostname = "localhost",
                LavalinkPort = 2333,
                Token = "",
                Guilds = [],
                BuildCommandsGlobally = false,
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information
            };
            using (StreamWriter sw = new(cnfpath))
            {
                await sw.WriteAsync(JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            Console.WriteLine($"Config successfully created at {cnfpath}. " +
                $"You may edit some parameters there to your liking.");
        }

        public static async Task CreateDefaultConfig(string cnfpath)
        {
            Console.WriteLine("Generating essential config...");
            Console.Write("Enter application token: ");
            string? token = Console.ReadLine();
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("Token is not specified!");
                Environment.Exit(-1);
            }
            
            Console.Write("Enter guild ids (separated by commas): ");
            string? guildIds = Console.ReadLine();
            List<ulong> guilds = new();
            if(string.IsNullOrEmpty(guildIds))
            {
                Console.WriteLine("Guild ids not specified! The bot will not work in any guild!");
            } else
            {
                string[] guildsSplit = guildIds.Split(',');
                foreach(string guildId in guildsSplit)
                {
                    if (!ulong.TryParse(guildId, out ulong _guildId))
                    {
                        Console.Error.WriteLine("Invalid guild id! Skipping '{0}'", guildId);
                        continue;
                    }
                    guilds.Add(_guildId);
                }
            }
            
            TalkingBotConfig config = new() {
                LavalinkHostname = "localhost",
                LavalinkPort = 2333,
                Token = token,
                Guilds = [.. guilds],
                BuildCommandsGlobally = false,
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information
            };

            using (StreamWriter sw = new(cnfpath))
            {
                await sw.WriteAsync(JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            Console.WriteLine($"Config successfully created at {cnfpath}. " +
                $"You may edit some parameters there to your liking.");
        }
    }
}
