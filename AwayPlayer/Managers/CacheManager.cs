using Community.CsharpSqlite.SQLiteClient;
using Newtonsoft.Json;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zenject;
using AwayPlayer.Utils;

#pragma warning disable CS0649 // Value is never assigned to, Zenject will inject
namespace AwayPlayer.Managers
{
    public class CacheManager<T> : IInitializable where T : class
    {
        [Inject] private readonly DatabaseManager DBMgr;
        [Inject] private readonly SiraLog Log;

        private string _tableName;

        public void Initialize()
        {
            Log.Debug($"Initializing CacheManager<{typeof(T)}>...");

            Task.Run(async () =>
            {
                var userId = PlatformUserId.Get();
                _tableName = $"{typeof(T).Name}Cache_{userId}";

                // Create the table if it doesn't exist
                using var command = new SqliteCommand(
                    $"CREATE TABLE IF NOT EXISTS {_tableName} " +
                    "(Id INTEGER PRIMARY KEY AUTOINCREMENT, Key TEXT UNIQUE, Value BLOB, Timestamp DATETIME)",
                    DBMgr.Database);
                command.ExecuteNonQuery();
            }).Wait();

            Log.Debug($"CacheManager<{typeof(T)}> Ready");
        }

        public void AddOrUpdateCache(string leaderboardId, T value)
        {
            var key = leaderboardId;
            var jsonValue = JsonConvert.SerializeObject(value);
            var cacheItem = new CacheItem
            {
                Key = key,
                Value = Encoding.UTF8.GetBytes(jsonValue),
                Timestamp = DateTime.Now
            };

            using var command = new SqliteCommand(
                $"INSERT OR REPLACE INTO {_tableName} (Key, Value, Timestamp) VALUES (@Key, @Value, @Timestamp)",
                DBMgr.Database);
            command.Parameters.Add(new SqliteParameter("@Key", cacheItem.Key));
            command.Parameters.Add(new SqliteParameter("@Value", cacheItem.Value));
            command.Parameters.Add(new SqliteParameter("@Timestamp", cacheItem.Timestamp));

            command.ExecuteNonQuery();
        }

        public T GetCache(string leaderboardId)
        {
            using (var command = new SqliteCommand($"SELECT * FROM {_tableName} WHERE Key = @Key", DBMgr.Database))
            {
                command.Parameters.Add(new SqliteParameter("@Key", leaderboardId));

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var jsonValue = Encoding.UTF8.GetString((byte[])reader["Value"]);
                    return JsonConvert.DeserializeObject<T>(jsonValue);
                }
            }

            return null;
        }

        public List<T> GetAllCacheEntries()
        {
            var cacheItems = new List<CacheItem>();

            using (var command = new SqliteCommand($"SELECT * FROM {_tableName}", DBMgr.Database))
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var cacheItem = new CacheItem
                    {
                        Key = reader["Key"].ToString(),
                        Value = (byte[])reader["Value"],
                        Timestamp = Convert.ToDateTime(reader["Timestamp"])
                    };
                    cacheItems.Add(cacheItem);
                }
            }

            return cacheItems
                .Select(cacheItem =>
                {
                    var jsonValue = Encoding.UTF8.GetString(cacheItem.Value);
                    return JsonConvert.DeserializeObject<T>(jsonValue);
                })
                .ToList();
        }

        public void RemoveCache(string key)
        {
            using var command = new SqliteCommand($"DELETE FROM {_tableName} WHERE Key = @Key", DBMgr.Database);
            command.Parameters.Add(new SqliteParameter("@Key", key));
            command.ExecuteNonQuery();
        }

        public void ClearCache()
        {
            using var command = new SqliteCommand($"DELETE FROM {_tableName}", DBMgr.Database);
            command.ExecuteNonQuery();
        }

        public int CountCacheItems()
        {
            using var command = new SqliteCommand($"SELECT COUNT(*) FROM {_tableName}", DBMgr.Database);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private class CacheItem
        {
            public string Key { get; set; }
            public byte[] Value { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
