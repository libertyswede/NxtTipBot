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
            var configSettings = ReadConfig();

            var logLevel = GetLogLevel(configSettings);
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;
            var walletFile = configSettings.Single(c => c.Key == "walletFile").Value;
            var nxtServerAddress = configSettings.Single(c => c.Key == "nxtServerAddress").Value;
            var currencyIds = GetCurrenciesConfiguration(configSettings);
            var assetConfigs = GetAssetsConfiguration(configSettings);

            var logger = SetupLogging(logLevel);
            logger.LogInformation($"logLevel: {logLevel}");
            logger.LogInformation($"nxtServerAddress: {nxtServerAddress}");
            logger.LogInformation($"walletFile: {walletFile}");
            currencyIds.ToList().ForEach(c => logger.LogInformation($"currency id: {c}"));
            assetConfigs.ToList().ForEach(a => logger.LogInformation($"asset id: {a.Id} ({a.Name})"));

            InitDatabase(walletFile);
            var walletRepository = new WalletRepository();
            var nxtConnector = new NxtConnector(new ServiceFactory(nxtServerAddress));
            var slackHandler = new SlackHandler(nxtConnector, walletRepository, logger);
            var slackConnector = new SlackConnector(apiToken, logger, slackHandler);

            Task.Run(async () =>
            {
                await walletRepository.Init(nxtConnector.GenerateMasterKey);
                nxtConnector.MasterKey = await walletRepository.GetMasterKey();
            }).Wait();
            slackHandler.SlackConnector = slackConnector;
            var transferables = GetTransferables(currencyIds, assetConfigs, nxtConnector);
            transferables.ForEach(t => slackHandler.AddTransferable(t));

            var slackTask = Task.Run(() => slackConnector.Run());
            Task.WaitAll(slackTask);
            logger.LogInformation("Exiting NxtTipbot");
        }

        private static List<NxtTransferable> GetTransferables(IEnumerable<ulong> currencyIds, IEnumerable<AssetConfig> assetConfigs, NxtConnector nxtConnector)
        {
            List<NxtTransferable> transferables = new List<NxtTransferable>();
            Task.Run(async () =>
            {
                foreach (var currencyId in currencyIds)
                {
                    transferables.Add(await nxtConnector.GetCurrency(currencyId));
                }
                foreach (var assetConfig in assetConfigs)
                {
                    transferables.Add(await nxtConnector.GetAsset(assetConfig.Id, assetConfig.Name));
                }
            }).Wait();
            return transferables;
        }

        private static IEnumerable<AssetConfig> GetAssetsConfiguration(IEnumerable<IConfigurationSection> configSettings)
        {
            var assetsSection = configSettings.SingleOrDefault(c => c.Key == "assets")?.GetChildren();
            if (assetsSection != null)
            {
                foreach (var assetConfig in assetsSection)
                {
                    var id = ulong.Parse(assetConfig.GetChildren().Single(a => a.Key == "id").Value);
                    var name = assetConfig.GetChildren().Single(a => a.Key == "name").Value;
                    yield return new AssetConfig { Id = id, Name = name };
                }
            }
        }

        private static IEnumerable<ulong> GetCurrenciesConfiguration(IEnumerable<IConfigurationSection> configSettings)
        {
            var currencyConfigs = configSettings.SingleOrDefault(c => c.Key == "currencies")?.GetChildren() 
                ?? new List<IConfigurationSection>().AsEnumerable();
            var currencyIds = currencyConfigs.Select(c => ulong.Parse(c.Value));
            return currencyIds;
        }

        private static void InitDatabase(string walletFile)
        {
            var folder = Path.GetDirectoryName(walletFile);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            WalletContext.WalletFile = walletFile;
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

        private static LogLevel GetLogLevel(IEnumerable<IConfigurationSection> configSettings)
        {
            var loggingSection = configSettings.Single(c => c.Key == "logging").GetChildren();
            var logLevelText = loggingSection.Single(c => c.Key == "logLevel").Value;
            var logLevel = (LogLevel) Enum.Parse(typeof(LogLevel), logLevelText);
            return logLevel;
        }

        private static ILogger SetupLogging(LogLevel logLevel)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(logLevel);
            return loggerFactory.CreateLogger("");
        }
    }
}
