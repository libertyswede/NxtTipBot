using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using NxtLib;

namespace NxtTipBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configSettings = ReadConfig();
            
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;
            var walletFile = configSettings.Single(c => c.Key == "walletFile").Value;
            var nxtServerAddress = configSettings.Single(c => c.Key == "nxtServerAddress").Value;

            var logger = SetupLogging();
            var nxtConnector = new NxtConnector(new ServiceFactory(nxtServerAddress), walletFile);
            var slackHandler = new SlackHandler(nxtConnector, logger);
            var slackConnector = new SlackConnector(apiToken, logger, slackHandler);

            Task.Run(() => slackConnector.Run()).Wait();
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

        private static ILogger SetupLogging()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Debug);
            return loggerFactory.CreateLogger("");
        }
    }
}
