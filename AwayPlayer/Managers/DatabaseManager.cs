using Community.CsharpSqlite.SQLiteClient;
using SiraUtil.Logging;
using System;
using System.IO;
using Zenject;

namespace AwayPlayer.Managers
{
    public class DatabaseManager : IInitializable, IDisposable
    {
        internal SqliteConnection Database;

        [Inject]
        private readonly SiraLog Log = null!;

        public void Initialize()
        {
            Log.Debug("Initializing database manager...");
            var dbPath = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, "AwayPlayer_Data.sqlite");
            Database = new SqliteConnection($"Data Source={dbPath};Version=3;");
            Database.Open();

            Log.Debug("Database manager ready...");
        }

        public void Dispose()
        {
            if (Database != null)
            {
                Database.Close();
                Database.Dispose();
                Database = null;
            }
        }
    }
}
