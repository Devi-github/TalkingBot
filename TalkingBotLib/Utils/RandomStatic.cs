using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Utils
{
    public static class RandomStatic
    {
        private static Random? random;

        static RandomStatic() {
            Initialize();
        }

        public static void Initialize()
        {
            random = new Random();
        }
        // XXX: I am not sure if those checks are necessary since static constructor should initialize random immediately
        public static int Next()
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.Next();
        }
        public static int Next(int max)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.Next(max);
        }
        public static int Next(int min, int max)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.Next(min, max);
        }

        public static long NextInt64()
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.NextInt64();
        }
        public static long NextInt64(long max)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.NextInt64(max);
        }
        public static long NextInt64(long min, long max)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.NextInt64(min, max);
        }

        public static double NextDouble()
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.NextDouble();
        }
        public static float NextSingle()
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            return random.NextSingle();
        }
        public static void NextBytes(byte[] buffer)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            random.NextBytes(buffer);
        }
        public static void NextBytes(Span<byte> buffer)
        {
            if (random == null) throw new NullReferenceException("Uninitialized random was used");
            random.NextBytes(buffer);
        }

    }
}
