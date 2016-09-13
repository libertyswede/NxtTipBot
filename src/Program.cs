using System;
using System.Collections.Generic;
using System.IO;
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

            InitDatabase(walletFile);
            var walletRepository = new WalletRepository();
            var nxtConnector = new NxtConnector(new ServiceFactory(nxtServerAddress));
            var slackHandler = new SlackHandler(nxtConnector, walletRepository, logger);
            var slackConnector = new SlackConnector(apiToken, logger, slackHandler);
            slackHandler.SlackConnector = slackConnector;

            var slackTask = Task.Run(() => slackConnector.Run());
            Task.WaitAll(slackTask);
            logger.LogInformation("Exiting NxtTipbot");
        }

        private static void InitDatabase(string walletFile)
        {
            var folder = Path.GetDirectoryName(walletFile);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            WalletContext.WalletFile = walletFile; // this ain't pretty, fix when IoC is added
            new WalletContext().Database.Migrate();
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
