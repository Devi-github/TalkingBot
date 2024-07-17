using System.Drawing;

namespace TalkingBot.Utils {
    public static class AdditionalUtils {
        public static bool TryParseTimecode(string timecode, out double result) {
            result = 0;
            bool success = true;
            double mins = 0, secs = 0;
            string[] parts = timecode.Split(':');
            if(parts.Length != 2) return false;
            success = success && double.TryParse(parts[0], out mins);
            success = success && double.TryParse(parts[1], out secs);
            result = mins * 60 + secs;
            return success;
        }
        public static string GetVersionString() {
            int major = TalkingBotClient.Major;
            int minor = TalkingBotClient.Minor;
            int patch = TalkingBotClient.Patch;
            string built = TalkingBotClient.IsBuilt ? "" : "-prebuild";

            return $"{major}.{minor}.{patch}{built}";
        }
        public static Discord.Color? ParseColorFromString(string? color) {
            if(color is null) return null;
            
            var conv = new ColorConverter();
            var result = (Color?)conv.ConvertFromString(color);
            if(result is null) return null;

            return new(result.Value.R, result.Value.G, result.Value.B);
        }
    }
}