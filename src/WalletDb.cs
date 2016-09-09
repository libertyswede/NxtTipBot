using System.IO;
using Microsoft.Data.Sqlite;

namespace NxtTipBot
{
    public class WalletDb
    {
        private readonly string filepath;

        public WalletDb(string filePath)
        {
            this.filepath = filePath;
        }
        
        public void Init()
        {
            if (File.Exists(filepath))
            {
                return;
            }

            var folder = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (var dbConnection = OpenNewDbConnection())
            {
                const string createAccountSql = "CREATE TABLE account (id INTEGER PRIMARY KEY, slack_id TEXT, secret_phrase TEXT, nxt_address TEXT)";
                using (var command = new SqliteCommand(createAccountSql, dbConnection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private SqliteConnection OpenNewDbConnection()
        {
            var dbConnection = new SqliteConnection($"Data Source={filepath}");
            dbConnection.Open();
            return dbConnection;
        }
    }
}