using AwayPlayer.Models;
using BeatSaberPlaylistsLib.Types;
using SiraUtil.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenject;
using Score = AwayPlayer.Models.Score;

namespace AwayPlayer.Managers
{
    internal class ScoreListManager : IInitializable
    {
        private readonly PlayerData _playerData;
        private readonly APIWrapper API;
        private readonly SiraLog Log;
        private readonly APConfig Config;
        private readonly WhitelistBlacklistManager WBMgr;
        private readonly UnityMainThreadDispatcher Dispatcher;
        private Score[] _filteredScores = new Score[0];

        internal IPlaylist[] Playlists;

        public Score[] FilteredScores 
        {
            get => _filteredScores;
            private set 
            {
                Log.Info("FilteredScores: " + FilteredScores.Length);
                _filteredScores = value;
            } 
        }
        public List<Score> AllScores { get; private set; }

        public bool IsReady { get; private set; } = false;

        public event Action OnScorelistUpdated;

        public ScoreListManager(PlayerDataModel playerDataModel, APIWrapper wrapper, SiraLog siraLog, APConfig config, WhitelistBlacklistManager whitelistBlacklistManager, UnityMainThreadDispatcher dispatcher) 
        { 
            _playerData = playerDataModel.playerData;
            API = wrapper;
            Log = siraLog;
            Config = config;
            WBMgr = whitelistBlacklistManager;
            Dispatcher = dispatcher;
#if DEBUG
            Log.DebugMode = true;
#endif
        }

        public void Initialize()
        {
            Task.Run(FetchReplayAPI);

            Playlists = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.GetAllPlaylists(true, out AggregateException e);

            Config.OnChanged += (x) => Reload(new ScoreFilterSettings(x.FavoritesOnly, x.Playlist, x.HMD, x.Controller));
        }

        private async Task FetchReplayAPI()
        {
            Log.Info("Starting replay fetch...");

            AllScores = await API.GetReplayListAsync();

            Log.Notice($"Replay fetch finished\nTotal fetched: {AllScores.Count}");

            Reload(new ScoreFilterSettings(Config.FavoritesOnly, Config.Playlist, Config.HMD, Config.Controller));
        }

        public void ForceReload()
        {
            Reload(new ScoreFilterSettings(Config.FavoritesOnly, Config.Playlist, Config.HMD, Config.Controller));
        }

        private void Reload(ScoreFilterSettings settings)
        {
            Log.Debug($"Reloading ScoreListManager with the following ScoreFilterSettings settings:\n{settings}");

            IsReady = false;

            try
            {
                var filteredScores = FilterScores(AllScores.ToArray(), settings).ToList();

                var blacklist = WBMgr.GetBlacklist();
                var whitelist = WBMgr.GetWhitelist();

                if (blacklist.Count > 0) filteredScores.RemoveAll(score => blacklist.Contains(score.Song.Hash.ToUpper()));
                if (whitelist.Count > 0) filteredScores.AddRange(AllScores.Where(score => whitelist.Contains(score.Song.Hash.ToUpper())));

                FilteredScores = filteredScores.ToArray();
            }
            catch (Exception e)
            {
                Log.Error("Something happened while reloading the ScoreListManager... this is my fuck you solution (╯°□°）╯︵ ┻━┻");
                Log.Error(e);
            }

            IsReady = true;
            Dispatcher.Enqueue(() => OnScorelistUpdated?.Invoke());

            Log.Notice($"Score list reload finished\nTotal scores: {AllScores.Count}\nTotal filtered: {FilteredScores.Length}");
        }

        public void GeneratePlaylists()
        {
            try
            {
                var allPlaylist = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.DefaultHandler.CreatePlaylist("AllPlayed - Awayplayer", "AllPlayed - AwayPlayer", "AwayPlayer", string.Empty);
                BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.StorePlaylist(allPlaylist);
                allPlaylist.AllowDuplicates = false;
                var created = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.RegisterPlaylist(allPlaylist);
                BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.StorePlaylist(allPlaylist);

                if (!created) Log.Error("Failed to register / create playlist");

                foreach (var item in AllScores)
                {
                    try
                    {
                        var addedSong = allPlaylist.Add(item.Song.Hash, item.Song.Name, item.Song.Id, item.Song.Mapper);
                        Log.Debug($"Success adding {addedSong.Name} to the all songs playlist!");
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.StorePlaylist(allPlaylist);
                BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.RefreshPlaylists(true);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public Score[] FilterScores(Score[] scores, ScoreFilterSettings settings)
        {
            var filteredScores = scores;
            if (settings.FavoritesOnly)
            {
                filteredScores = filteredScores.Where((score) => _playerData.favoritesLevelIds.Contains($"custom_level_{score.Song.Hash.ToUpper()}")).ToArray();
                Log.Debug($"Filtered on favorites: {filteredScores.Length}");
            }

            if (!string.IsNullOrWhiteSpace(settings.Playlist) && settings.Playlist != "None")
            {
                var playlist = Playlists.FirstOrDefault(x => x.Title == settings.Playlist);
                var playlistHashes = playlist == null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(playlist.Select(x => x.Hash), StringComparer.OrdinalIgnoreCase);
                filteredScores = filteredScores.Where(score => playlistHashes.Contains(score.Song.Hash)).ToArray();
                Log.Debug($"Filtered on playlist: {filteredScores.Length}");
            }

            if (settings.HMD != HMD.Ignore)
            {
                if (settings.HMD != HMD.Unknown)
                {
                    filteredScores = filteredScores.Where((score) => score.Hmd == (int)settings.HMD).ToArray();
                    Log.Debug($"Filtered on HMD: {filteredScores.Length}");
                }
                else
                {
                    Log.Warn($"Unknown HMD, ignoring HMD filter....");
                }
            }

            if (settings.Controller != Controller.Ignore)
            {
                if (settings.Controller != Controller.Unknown)
                {
                    filteredScores = filteredScores.Where((score) => score.Controller == (int)settings.Controller).ToArray();
                    Log.Debug($"Filtered on controller: {filteredScores.Length}"); 
                }
                else
                {
                    Log.Warn($"Unknown Controller, ignoring Controller filter....");
                }
            }

            return filteredScores;
        }

        internal List<object> GetPlaylistsAsObject()
        {
            string debugString = "";
            foreach (var item in Playlists.Select((x) => x.Title ?? "Null"))
            {
                debugString = $"{debugString}{item}\n";
            }
            Log.Debug($"Replay list read as:\n{debugString}");
            var list = new List<object>
            {
                "None"
            };
            list.AddRange(Playlists.Select((x) => x.Title ?? "Null"));
            return list;
        }

        internal List<object> GetHMDsAsObject()
        {
            List<int?> hmds = new List<int?>();

            foreach (var item in AllScores)
                if (!hmds.Contains(item.Hmd)) hmds.Add(item.Hmd);


            string debugString = "";
            foreach (var item in hmds)
            {
                debugString = $"{debugString}{(HMD)item}\n";
            }
            Log.Debug($"HMD list read as:\n{debugString}");


            var list = new List<object>
            {
                "Ignore"
            };

            hmds.ConvertAll((HmdInt) => HmdInt ?? 0)
                .ConvertAll((HmdInt) => ((HMD)HmdInt).ToString())
                .ForEach((HmdString) => list.Add(HmdString));
            return list;
        }

        internal List<object> GetControllersAsObject()
        {
            List<int?> controllers = new List<int?>();

            foreach (var item in AllScores)
                if (!controllers.Contains(item.Controller)) controllers.Add(item.Controller);


            string debugString = "";
            foreach (var item in controllers)
            {
                debugString = $"{debugString}{(Controller)item}\n";
            }
            Log.Debug($"Controller list read as:\n{debugString}");


            var list = new List<object>
            {
                "Ignore"
            };

            controllers.ConvertAll((ControllerInt) => ControllerInt ?? 0)
                .ConvertAll((ControllerInt) => ((Controller)ControllerInt).ToString())
                .ForEach((ControllerString) => list.Add(ControllerString));
            return list;
        }
    }
}
