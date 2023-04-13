namespace TalkingBot.Utils {
    public static class AdditionalUtils {
        public static bool TryParseTimecode(string timecode, out double result) {
            result = 0;
            string[] parts = timecode.Split(':');
            if(parts.Length != 2) return false;
            try {
                result = double.Parse(parts[0]) * 60 + double.Parse(parts[1]);
            } catch(Exception e) {
                return false;
            }
            return true;
        }
    }
}