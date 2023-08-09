using System;
using Discord.Net;
using Newtonsoft.Json;
using TalkingBot.Core.Logging;
using System.IO;

namespace TalkingBot.Core.Caching {
    public class Cacher<T> {
        public Cacher() {

        }
        public void SaveCached(string typename, T[] cache) {
            if(!typeof(T).IsSerializable) {
                Console.WriteLine($"Error: type not serializable {typename}");
                return;
            }

            string json = JsonConvert.SerializeObject(cache);

            string filename = $"cache_{typename}.json";
            string dir = Directory.GetCurrentDirectory() + "/Cache/";
            
            if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using(StreamWriter sw = new StreamWriter(dir + filename)) {
                sw.Write(json);
            }
        }
        public T[]? LoadCached(string typename) {
            if(!typeof(T).IsSerializable) {
                return null;
            }

            string filename = $"cache_{typename}.json";
            string dir = Directory.GetCurrentDirectory() + "/Cache/";

            string json = "";

            try {
                using(StreamReader sr = new StreamReader(dir + filename)) {
                    json = sr.ReadToEnd();
                }
            } catch(IOException) {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.Create(dir + filename);
                return null;
            }

            var result = JsonConvert.DeserializeObject<T[]>(json);

            return result;
        }
    }
}