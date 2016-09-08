using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;

namespace NxtTipBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configSettings = ReadConfig();
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;
            var connector = new SlackConnector(apiToken);
            Task.Run(() => connector.Run()).Wait();
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
