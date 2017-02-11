using Microsoft.EntityFrameworkCore;
using NxtTipbot.Model;

namespace NxtTipbot
{
    public class WalletContext : DbContext
    {
        internal static string WalletFile { get; set; }

        public DbSet<NxtAccount> NxtAccounts { get; set; }
        public DbSet<UserSetting> UserSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Filename={WalletFile}");
        }
    }
}