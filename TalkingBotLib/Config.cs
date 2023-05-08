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
            TalkingBotConfig config = new() {
                LavalinkHostname = "localhost",
                LavalinkPort = 2333,
                Token = token,
                Guilds = new ulong[] {},
                ForceUpdateCommands = false
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
