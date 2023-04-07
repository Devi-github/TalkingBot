using Discord.Interactions;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace TalkingBot
{
    internal struct TalkingBotConfig
    {
        public string Token { get; set; }
    }
    internal class Program
    {
        public static Task Main(string[] args) => MainAsync(args);

        private static async Task MainAsync(string[] args)
        {
            string cnfpath = "Config.json";
            string jsonconfig = "";
            if(args.Length > 1) 
            {
                int idx = Array.FindIndex(args, 0, args.Length, str => str == "-C");

                try
                {
                    if (idx != -1) cnfpath = args[idx + 1];
                } catch(ArgumentOutOfRangeException)
                {
                    Console.Error.WriteLine("Expected string after '-C'. Got null");
                    Environment.Exit(-1);
                }
            }
            if(!File.Exists(cnfpath))
            {
                await CreateDefaultConfig(cnfpath);
            }
            jsonconfig = File.ReadAllText(cnfpath);
            TalkingBotConfig config = JsonConvert.DeserializeObject<TalkingBotConfig>(jsonconfig)!;

            
        }
        private static async Task CreateDefaultConfig(string cnfpath)
        {
            TalkingBotConfig config = new();

            Console.WriteLine("Config not found at: {0}\nCreating new one...", cnfpath);
            Console.Write("Enter application token: ");
            string? token = Console.ReadLine();
            if(string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("Token is not specified!");
                Environment.Exit(-1);
            }
            config.Token = token;

            using(StreamWriter sw = new(cnfpath))
            {
                await sw.WriteAsync(JsonConvert.SerializeObject(config));
            }
        }
    }
}