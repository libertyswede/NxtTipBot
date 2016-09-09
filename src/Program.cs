using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;

namespace NxtTipBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var serviceProvider = SetupServiceProvider();
            var configSettings = ReadConfig();
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;

            var connector = new SlackConnector(apiToken, serviceProvider.GetService<ILogger>());
            Task.Run(() => connector.Run()).Wait();
        }

        private static IServiceProvider SetupServiceProvider()
        {
            var services = new ServiceCollection();
            SetupLogging(services);
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider;
        }

        private static void SetupLogging(ServiceCollection services)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Debug);
            services.AddSingleton(loggerFactory.CreateLogger(""));
        }

        private static IEnumerable<IConfigurationSection> ReadConfig()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(PlatformServices.Default.Application.ApplicationBasePath);
            configBuilder.AddJsonFile("config.json");
            configBuilder.AddJsonFile("config-Development.json", true);
            var configRoot = configBuilder.Build();
            var configSettings = configRoot.GetChildren();
            return configSettings;
        }
    }
}
