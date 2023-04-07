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
            TalkingBotConfig config = new();

            Console.WriteLine("Generating essential config...");
            Console.Write("Enter application token: ");
            string? token = Console.ReadLine();
            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("Token is not specified!");
                Environment.Exit(-1);
            }
            config.Token = token;
            config.Guilds = new string[] { };

            using (StreamWriter sw = new(cnfpath))
            {
                await sw.WriteAsync(JsonConvert.SerializeObject(config));
            }
            Console.WriteLine($"Config successfully created at {cnfpath}. You may edit some parameters there to your liking.");
        }
    }
}
