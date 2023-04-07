using TalkingBot.Core;
using TalkingBot;
using Newtonsoft.Json;

namespace RegenerateConfig
{
    internal class Program
    {
        internal async static Task Main(string[] args)
        {
            string cnfpath = "Config.json";
            string jsonconfig = "";
            if (args.Length > 1)
            {
                int idx = Array.FindIndex(args, 0, args.Length, str => str == "-C");
   
                try
                {
                    if (idx != -1) cnfpath = args[idx + 1];
                    else Console.WriteLine($"Ignoring unrecognized argument '{args[idx]}'");
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.Error.WriteLine("Expected string after '-C'. Got null");
                    Environment.Exit(-1);
                }
            }
            await Config.CreateDefaultConfig(cnfpath);
        }
    }
}