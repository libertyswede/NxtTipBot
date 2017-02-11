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
            var masterKey = configSettings.Single(c => c.Key == "masterKey").Value;
            var currencyConfigs = GetTransferableConfiguration(configSettings, "currencies");
            var assetConfigs = GetTransferableConfiguration(configSettings, "assets");
            var blockchainBackup = bool.Parse(configSettings.Single(c => c.Key == "blockchainBackup").Value);

            var logger = SetupLogging(logLevel);
            logger.LogInformation($"logLevel: {logLevel}");
            logger.LogInformation($"nxtServerAddress: {nxtServerAddress}");
            logger.LogInformation($"walletFile: {walletFile}");
            logger.LogInformation($"blockchainBackup: {blockchainBackup}");
            currencyConfigs.ToList().ForEach(c => logger.LogInformation($"currency id: {c.Id} ({c.Name})"));
            assetConfigs.ToList().ForEach(a => logger.LogInformation($"asset id: {a.Id} ({a.Name})"));

            InitDatabase(walletFile);
            var transferables = new Transferables();
            var nxtConnector = new NxtConnector(new ServiceFactory(nxtServerAddress));
            var blockchainStore = blockchainBackup ? new BlockchainStore(masterKey, nxtConnector) : null;
            var walletRepository = new WalletRepository(blockchainStore);
            var slackHandler = new SlackHandler(nxtConnector, walletRepository, transferables, logger);
            var slackConnector = new SlackConnector(apiToken, logger, slackHandler);

            CheckMasterKey(logger, masterKey, nxtConnector);
            nxtConnector.MasterKey = masterKey;
            VerifyBlockchainBackup(blockchainBackup, logger, nxtConnector, blockchainStore, walletRepository);
            slackHandler.SlackConnector = slackConnector;
            var transferableList = GetTransferables(currencyConfigs, assetConfigs, nxtConnector);
            CheckReactionIds(transferableList);
            transferableList.ForEach(t => transferables.AddTransferable(t));

            var slackTask = Task.Run(() => slackConnector.Run());
            Task.WaitAll(slackTask);
            logger.LogInformation("Exiting NxtTipbot");
        }

        private static void CheckReactionIds(List<NxtTransferable> transferableList)
        {
            var reactionIds = new Dictionary<string, string> { { "nxt", "NXT" } };
            foreach (var transferable in transferableList.Where(t => !string.IsNullOrEmpty(t.ReactionId)))
            {
                if (reactionIds.ContainsKey(transferable.ReactionId))
                {
                    Console.WriteLine($"Cannot add reaction {transferable.ReactionId} twice.");
                    throw new Exception($"Cannot add reaction {transferable.ReactionId} twice.");
                }
                reactionIds.Add(transferable.ReactionId, transferable.Name);
            }
        }

        private static void VerifyBlockchainBackup(bool blockchainBackup, ILogger logger, NxtConnector nxtConnector, 
            BlockchainStore blockchainStore, WalletRepository walletRepository)
        {
            if (blockchainBackup)
            {
                logger.LogInformation("Verifying blockchain backup status...");
                var backupCount = 0;
                var accountCount = 0;

                Task.Run(async () =>
                {
                    var balance = await nxtConnector.GetBalance(Nxt.Singleton, blockchainStore.MainAccount.AccountRs);
                    if (balance < 10)
                    {
                        logger.LogWarning($"Make sure your main account ({blockchainStore.MainAccount.AccountRs}) is properly funded!");
                    }
                    var accounts = await walletRepository.GetAllAccounts();
                    accountCount = accounts.Count;
                    await blockchainStore.VerifyBackupStatus(accounts);
                }).Wait();
                logger.LogInformation($"Done, {backupCount} new accounts were backed up ({accountCount} total).");
            }
        }

        private static void CheckMasterKey(ILogger logger, string masterKey, INxtConnector nxtConnector)
        {
            if (!string.IsNullOrEmpty(masterKey))
            {
                return;
            }
            var sampleMasterKey = nxtConnector.GenerateMasterKey();
            var error = "Configuration property 'masterKey' has not been set. Please set it to a secure 256-bit password.\n" +
                        "You may use the generated phrase below:\n" +
                        sampleMasterKey;
            logger.LogCritical(error);
            Environment.Exit(-1);
        }

        private static List<NxtTransferable> GetTransferables(IEnumerable<TransferableConfig> currencyConfigs, IEnumerable<TransferableConfig> assetConfigs, INxtConnector nxtConnector)
        {
            var transferables = new List<NxtTransferable>();
            Task.Run(async () =>
            {
                foreach (var currencyConfig in currencyConfigs)
                {
                    transferables.Add(await nxtConnector.GetCurrency(currencyConfig));
                }
                foreach (var assetConfig in assetConfigs)
                {
                    transferables.Add(await nxtConnector.GetAsset(assetConfig));
                }
            }).Wait();
            return transferables;
        }

        private static IEnumerable<TransferableConfig> GetTransferableConfiguration(IEnumerable<IConfigurationSection> configSettings, string sectionName)
        {
            var sections = configSettings.SingleOrDefault(c => c.Key == sectionName)?.GetChildren();
            if (sections != null)
            {
                foreach (var section in sections)
                {
                    var id = ulong.Parse(section.GetChildren().Single(a => a.Key == "id").Value);
                    var name = section.GetChildren().Single(a => a.Key == "name").Value;
                    var recipientMessage = section.GetChildren().SingleOrDefault(a => a.Key == "recipientMessage")?.Value;
                    var monikers = section.GetChildren().SingleOrDefault(a => a.Key == "monikers");
                    var reactionId = section.GetChildren().SingleOrDefault(c => c.Key == "tipReactionId")?.Value ?? string.Empty;
                    yield return new TransferableConfig(id, name, recipientMessage, GetTransferableMonikers(monikers), reactionId);
                }
            }
        }

        private static IEnumerable<string> GetTransferableMonikers(IConfigurationSection monikerSection)
        {
            if (monikerSection != null)
            {
                foreach (var moniker in monikerSection.GetChildren())
                {
                    yield return moniker.Value.Trim();
                }
            }
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
