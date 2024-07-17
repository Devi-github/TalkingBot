using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core
{
    /// <summary>
    /// Maybe unused
    /// </summary>
    public static class ServiceManager
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void SetProvider(IServiceCollection collection)
            => ServiceProvider = collection.BuildServiceProvider();

        public static T GetService<T>() where T : notnull {
            if(ServiceProvider is null) 
                throw new Exception("ServiceProvider was not set before requesting service");
            return ServiceProvider.GetRequiredService<T>();
        }
    }
}
