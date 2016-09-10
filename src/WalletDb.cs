using System.IO;
using System.Threading.Tasks;
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

        public async Task<NxtAccount> GetAccount(string slackId)
        {
            var sql = $"SELECT id, slack_id, secret_phrase, nxt_address FROM account WHERE slack_id = '{slackId}'";
            using (var dbConnection = OpenNewDbConnection())
            using (var command = new SqliteCommand(sql, dbConnection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                NxtAccount account = null;
                if (await reader.ReadAsync())
                {
                    account = ParseAccount(reader);
                }
                return account;
            }
        }

        public async Task<NxtAccount> CreateAccount(string slackId, string secretPhrase, string addressRs)
        {
            using (var dbConnection = OpenNewDbConnection())
            {
                var sql = $"INSERT INTO account (slack_id, secret_phrase, nxt_address) VALUES ('{slackId}', '{secretPhrase}', '{addressRs}')";
                using (var command = new SqliteCommand(sql, dbConnection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqliteCommand("SELECT last_insert_rowid()", dbConnection))
                {
                    var id = (long) await command.ExecuteScalarAsync();
                    return new NxtAccount(id, slackId, secretPhrase, addressRs);
                }
            }
        }

        private static NxtAccount ParseAccount(SqliteDataReader reader)
        {
            var id = (long)reader["id"];
            var slackId = reader["slack_id"].ToString();
            var secretPhrase = reader["secret_phrase"].ToString();
            var addressRs = reader["nxt_address"].ToString();
            var account = new NxtAccount(id, slackId, secretPhrase, addressRs);
            return account;
        }

        private SqliteConnection OpenNewDbConnection()
        {
            var dbConnection = new SqliteConnection($"Data Source={filepath}");
            dbConnection.Open();
            return dbConnection;
        }
    }
}