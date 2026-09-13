using Community.CsharpSqlite.SQLiteClient;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using AwayPlayer.Utils;

namespace AwayPlayer.Managers
{
    public class WhitelistBlacklistManager : IInitializable
    {
        private readonly DatabaseManager DBMgr;
        private string BlacklistTableName;
        private string WhitelistTableName;
        private readonly SiraLog Log;
        public List<string> Blacklist { get; private set; }
        public List<string> Whitelist { get; private set; }

        public WhitelistBlacklistManager(SiraLog log, DatabaseManager dbmgr)
        {
            Log = log;
            DBMgr = dbmgr;
        }

        public void Initialize()
        {
            Log.Debug($"Initializing the WhitelistBlacklistManager...");
            Task.Run(async () =>
            {
                var userId = PlatformUserId.Get();
                BlacklistTableName = $"Blacklist_{userId}";
                WhitelistTableName = $"Whitelist_{userId}";

                // Create the table if it doesn't exist
                using (var command = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {BlacklistTableName} (Id INTEGER PRIMARY KEY AUTOINCREMENT, Key TEXT UNIQUE, Timestamp DATETIME)", DBMgr.Database))
                {
                    ExecuteSQLiteCommand(command, "Creating blacklist table");
                }

                using (var command2 = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {WhitelistTableName} (Id INTEGER PRIMARY KEY AUTOINCREMENT, Key TEXT UNIQUE, Timestamp DATETIME)", DBMgr.Database))
                {
                    ExecuteSQLiteCommand(command2, "Creating whitelist table");
                }
            }).Wait();
            Log.Debug($"WhitelistBlacklistManager Ready");
        }

        private void ExecuteSQLiteCommand(SqliteCommand command, string actionDescription)
        {
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                if (ex.Message.Contains("UNIQUE constraint failed"))
                {
                    Log.Warn($"{actionDescription} - Key already exists.");
                }
                else if (ex.Message.Contains("FOREIGN KEY constraint failed"))
                {
                    Log.Warn($"{actionDescription} - Key does not exist.");
                }
                else
                {
                    Log.Error($"{actionDescription} - SQLite error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{actionDescription} - An error occurred: {ex.Message}");
            }
        }

        public void AddToWhitelist(string songHash)
        {
            using (var command = new SqliteCommand($"INSERT INTO {WhitelistTableName} (Key, Timestamp) VALUES (@Key, @Timestamp)", DBMgr.Database))
            {
                command.Parameters.Add(new SqliteParameter("@Key", songHash));
                command.Parameters.Add(new SqliteParameter("@Timestamp", DateTime.Now));

                ExecuteSQLiteCommand(command, $"Adding {songHash} to whitelist");
            }

            Whitelist?.Add(songHash);
        }

        public void RemoveFromWhitelist(string songHash)
        {
            using (var command = new SqliteCommand($"DELETE FROM {WhitelistTableName} WHERE Key = @Key", DBMgr.Database))
            {
                command.Parameters.Add(new SqliteParameter("@Key", songHash));
                ExecuteSQLiteCommand(command, $"Removing {songHash} from whitelist");
            }

            Whitelist?.Remove(songHash);
        }

        public List<string> GetWhitelist()
        {
            if (Whitelist != null)
                return Whitelist;

            var items = new List<string>();
            using (var command = new SqliteCommand($"SELECT * FROM {WhitelistTableName}", DBMgr.Database))
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Column name: "Key"
                    items.Add(reader["Key"].ToString());
                }
            }

            Whitelist = items;
            return Whitelist;
        }

        public void AddToBlacklist(string songHash)
        {
            using (var command = new SqliteCommand($"INSERT INTO {BlacklistTableName} (Key, Timestamp) VALUES (@Key, @Timestamp)", DBMgr.Database))
            {
                command.Parameters.Add(new SqliteParameter("@Key", songHash));
                command.Parameters.Add(new SqliteParameter("@Timestamp", DateTime.Now));

                ExecuteSQLiteCommand(command, $"Adding {songHash} to blacklist");
            }

            Blacklist?.Add(songHash);
        }

        public void RemoveFromBlacklist(string songHash)
        {
            using (var command = new SqliteCommand($"DELETE FROM {BlacklistTableName} WHERE Key = @Key", DBMgr.Database))
            {
                command.Parameters.Add(new SqliteParameter("@Key", songHash));
                ExecuteSQLiteCommand(command, $"Removing {songHash} from blacklist");
            }

            Blacklist?.Remove(songHash);
        }

        public List<string> GetBlacklist()
        {
            if (Blacklist != null)
                return Blacklist;

            var items = new List<string>();
            using (var command = new SqliteCommand($"SELECT * FROM {BlacklistTableName}", DBMgr.Database))
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Column name: "Key"
                    items.Add(reader["Key"].ToString());
                }
            }

            Blacklist = items;
            return Blacklist;
        }
    }
}
