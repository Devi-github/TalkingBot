using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    public class ServiceManager
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void SetProvider(ServiceCollection collection)
            => ServiceProvider = collection.BuildServiceProvider();

        public static T GetService<T>() where T : new()
            => ServiceProvider.GetRequiredService<T>();
    }
}
