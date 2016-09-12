using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using NxtLib;

namespace NxtTipbot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting up NxtTipbot");
            var logger = SetupLogging();
            var configSettings = ReadConfig();
            
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;
            var walletFile = configSettings.Single(c => c.Key == "walletFile").Value;
            var nxtServerAddress = configSettings.Single(c => c.Key == "nxtServerAddress").Value;
            logger.LogInformation($"nxtServerAddress: {nxtServerAddress}");
            logger.LogInformation($"walletFile: {walletFile}");

            WalletContext.WalletFile = walletFile;
            new WalletContext().Database.Migrate();

            var nxtConnector = new NxtConnector(new ServiceFactory(nxtServerAddress), walletFile);
            var slackHandler = new SlackHandler(nxtConnector, logger);
            var slackConnector = new SlackConnector(apiToken, logger, slackHandler);
            slackHandler.SlackConnector = slackConnector;

            var slackTask = Task.Run(() => slackConnector.Run());
            Task.WaitAll(slackTask);
            logger.LogInformation("Exiting program.");
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
            loggerFactory.AddConsole(LogLevel.Trace);
            return loggerFactory.CreateLogger("");
        }
    }
}
