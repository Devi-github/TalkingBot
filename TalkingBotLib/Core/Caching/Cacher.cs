using System;
using Discord.Net;
using Newtonsoft.Json;
using System.IO;

namespace TalkingBot.Core.Caching {
    public class Cacher<T> {
        public Cacher() {}
        public void SaveCached(string typename, T[] cache) {
            string json = JsonConvert.SerializeObject(cache);

            string filename = $"cache_{typename}.json";
            string dir = Directory.GetCurrentDirectory() + "/Cache/";
            
            if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using(StreamWriter sw = new StreamWriter(dir + filename)) {
                sw.Write(json);
            }
        }
        public T[]? LoadCached(string typename) {
            string filename = $"cache_{typename}.json";
            string dir = Directory.GetCurrentDirectory() + "/Cache/";

            string json = "";

            try {
                using StreamReader sr = new(dir + filename);

                json = sr.ReadToEnd();
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