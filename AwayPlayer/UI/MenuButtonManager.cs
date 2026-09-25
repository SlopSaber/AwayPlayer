using AwayPlayer.Managers;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Utilities;
using System.Linq;
using UnityEngine;
using Zenject;

#pragma warning disable CS0649 // Value is never assigend to - We have zenject
namespace AwayPlayer.UI
{
    internal class MenuButtonManager : BSMLAutomaticViewController, IInitializable
    {
        private const string AFK_BUTTON = "" +
            "<button id='afk-button' text='AFK' font-size='3' word-wrapping='false' anchor-pos-x='-15' anchor-pos-y='2' pref-width='9' pref-height='6' on-click='afk-click'/>";
        private const string BLACKLIST_BUTTON = "" +
            "<bg id='root'>" +
            "<button id='blacklist-button' text='B' active='~blacklist-button-active' hover-hint='Adds the current song to the AwayPlayer backlist' anchor-pos-x='56' anchor-pos-y='-3' pref-width='8' pref-height='8' on-click='blacklist-click'/>" +
            "</bg>";
        private const string PRIMARY_BLACKLIST_BUTTON = "" +
            "<bg id='root'>" +
            "<primary-button id='blacklist-button' active='~primary-blacklist-button-active' text='B' hover-hint='Removes the current song to the AwayPlayer backlist' anchor-pos-x='26' anchor-pos-y='0' pref-width='8' pref-height='8' on-click='primary-blacklist-click'/>" +
            "</bg>";
        private const string WHITELIST_BUTTON = "" +
            "<bg id='root'>" +
            "<button id='whitelist-button' text='W' active='~whitelist-button-active' hover-hint='Adds the current song to the AwayPlayer whitelist' anchor-pos-x='48' anchor-pos-y='-3' pref-width='8' pref-height='8' on-click='whitelist-click'/>" +
            "</bg>";
        private const string PRIMARY_WHITELIST_BUTTON = "" +
            "<bg id='root'>" +
            "<primary-button id='whitelist-button' active='~primary-whitelist-button-active' text='W' hover-hint='Removes the current song to the AwayPlayer whitelist' anchor-pos-x='18' anchor-pos-y='0' pref-width='8' pref-height='8' on-click='primary-whitelist-click'/>" +
            "</bg>";

        private bool _blacklistButtonActive = true;
        private bool _whitelistButtonActive = true;
        private bool _primaryBlacklistButtonActive = false;
        private bool _primaryWhitelistButtonActive = false;

        [UIComponent("afk-button")]
        private RectTransform afkButtonTransform { get; set; }

        [UIValue("blacklist-button-active")]
        public bool BlacklistButtonActive
        {
            get => _blacklistButtonActive;
            set
            {
                _blacklistButtonActive = value;
                NotifyPropertyChanged();
                if (value) PrimaryBlacklistButtonActive = false;
            }
        }

        [UIValue("primary-blacklist-button-active")]
        public bool PrimaryBlacklistButtonActive
        {
            get => _primaryBlacklistButtonActive;
            set
            {
                _primaryBlacklistButtonActive = value;
                NotifyPropertyChanged();
                if (value) BlacklistButtonActive = false;
            }
        }

        [UIValue("whitelist-button-active")]
        public bool WhitelistButtonActive
        {
            get => _whitelistButtonActive;
            set
            {
                _whitelistButtonActive = value;
                NotifyPropertyChanged();
                if (value) PrimaryWhitelistButtonActive = false;
            }
        }

        [UIValue("primary-whitelist-button-active")]
        public bool PrimaryWhitelistButtonActive
        {
            get => _primaryWhitelistButtonActive;
            set
            {
                _primaryWhitelistButtonActive = value;
                NotifyPropertyChanged();
                if (value) WhitelistButtonActive = false;
            }
        }

        [Inject]
        private readonly ReplayManager _replayManager;

        [Inject]
        private readonly APMenuFloatingScreen _menuFloatingScreen;

        [Inject]
        private readonly APConfig _config;

        [Inject]
        private readonly WhitelistBlacklistManager WBMgr;

        [Inject]
        private readonly ScoreListManager SLM;

        public void Initialize()
        {
            var levelSelection = Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>().First();
            var hierarchy = BeatSaberUI.DiContainer.Resolve<HMUI.HierarchyManager>();
            var title = hierarchy.GetField<HMUI.ScreenSystem, HMUI.HierarchyManager>("_screenSystem").titleViewController;
            BSMLParser.Instance.Parse(AFK_BUTTON, title.gameObject, this);
            var afkRoot = afkButtonTransform.gameObject;
            title.gameObject.AddComponent<AfkTitleButtonVisibility>().Initialize(afkRoot, levelSelection.gameObject);
            var standardLevel = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First(); // Stolen from song core, thank you ;)
            if (_config.BlacklistEnable)
            {
                BSMLParser.Instance.Parse(BLACKLIST_BUTTON, standardLevel.transform.Find("LevelDetail").gameObject, this);
                BSMLParser.Instance.Parse(PRIMARY_BLACKLIST_BUTTON, standardLevel.transform.Find("LevelDetail").gameObject, this);
            }
            if (_config.WhitelistEnable)
            {
                BSMLParser.Instance.Parse(WHITELIST_BUTTON, standardLevel.transform.Find("LevelDetail").gameObject, this);
                BSMLParser.Instance.Parse(PRIMARY_WHITELIST_BUTTON, standardLevel.transform.Find("LevelDetail").gameObject, this);
            }

            var controller = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
            controller.didChangeDifficultyBeatmapEvent += OnDifficultyChanged;
            controller.didChangeContentEvent += OnContentChanged;
        }

        private void OnContentChanged(StandardLevelDetailViewController controller, StandardLevelDetailViewController.ContentType type)
        {
            if (type == StandardLevelDetailViewController.ContentType.OwnedAndReady) UpdateButtons(GetSelectedSongHash(controller));
        }

        private void OnDifficultyChanged(StandardLevelDetailViewController controller)
        {
            UpdateButtons(GetSelectedSongHash(controller));
        }

        private void UpdateButtons(string selectedSong)
        {
            if (selectedSong == null)
            {
                BlacklistButtonActive = false;
                WhitelistButtonActive = false;
                PrimaryBlacklistButtonActive = false;
                PrimaryWhitelistButtonActive = false;
                return;
            }

            var blacklist = WBMgr.GetBlacklist();
            var whitelist = WBMgr.GetWhitelist();

            if (blacklist.Contains(selectedSong))
            {
                PrimaryBlacklistButtonActive = true;
                return;
            }

            if (whitelist.Contains(selectedSong))
            {
                PrimaryWhitelistButtonActive = true;
                return;
            }

            WhitelistButtonActive = true;
            BlacklistButtonActive = true;
        }

        [UIAction("afk-click")]
        public void AFKClick()
        {
            if (_replayManager.Enabled)
            {
                _replayManager.Enabled = false;
                _menuFloatingScreen.Visible = false;
                return;
            }
            if (!SLM.IsReady || SLM.FilteredScores.Length == 0)
            {
                Plugin.Logger.Warn("AwayPlayer scores are not ready or the current filter has no scores.");
                return;
            }

            _replayManager.Setup();
            _menuFloatingScreen.Visible = true;
        }

        [UIAction("blacklist-click")]
        public void BlacklistClick()
        {
            var selectedSong = GetSelectedSongHash();
            if (selectedSong == null) return;

            if (WBMgr.GetWhitelist().Contains(selectedSong))
            {
                WBMgr.RemoveFromWhitelist(selectedSong);
                WhitelistButtonActive = true;
            }

            WBMgr.AddToBlacklist(selectedSong);
            PrimaryBlacklistButtonActive = true;

            SLM.ForceReload();

            if (_replayManager.Enabled && _replayManager.CurrentScore.Song.Hash == selectedSong)
            {
                _replayManager.SkipCurrentSelection();
            }
        }

        [UIAction("primary-blacklist-click")]
        public void PrimaryBlacklistClick()
        {
            var selectedSong = GetSelectedSongHash();
            if (selectedSong == null) return;
            WBMgr.RemoveFromBlacklist(selectedSong);
            BlacklistButtonActive = true;

            SLM.ForceReload();
        }

        [UIAction("whitelist-click")]
        public void WhitelistClick()
        {
            var selectedSong = GetSelectedSongHash();
            if (selectedSong == null) return;

            if (WBMgr.GetBlacklist().Contains(selectedSong))
            {
                WBMgr.RemoveFromBlacklist(selectedSong);
                BlacklistButtonActive = true;
            }

            WBMgr.AddToWhitelist(selectedSong);
            PrimaryWhitelistButtonActive = true;

            SLM.ForceReload();
        }

        [UIAction("primary-whitelist-click")]
        public void PrimaryWhitelistClick()
        {
            var selectedSong = GetSelectedSongHash();
            if (selectedSong == null) return;
            WBMgr.RemoveFromWhitelist(selectedSong);
            WhitelistButtonActive = true;

            SLM.ForceReload();
        }

        private string GetSelectedSongHash()
        {
            var standardLevel = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
            return GetSelectedSongHash(standardLevel);
        }

        private static string GetSelectedSongHash(StandardLevelDetailViewController controller)
        {
            const string customLevelPrefix = "custom_level_";
            var levelId = controller.beatmapLevel?.levelID;
            return levelId != null && levelId.StartsWith(customLevelPrefix, System.StringComparison.OrdinalIgnoreCase)
                ? levelId.Substring(customLevelPrefix.Length).ToUpperInvariant()
                : null;
        }
    }

    internal sealed class AfkTitleButtonVisibility : MonoBehaviour
    {
        private GameObject buttonRoot;
        private GameObject levelSelection;
        private bool wasVisible;

        internal void Initialize(GameObject root, GameObject menu)
        {
            buttonRoot = root;
            levelSelection = menu;
            UpdateVisibility();
        }

        private void Update()
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (buttonRoot == null) return;
            var shouldShow = levelSelection != null && levelSelection.activeInHierarchy;
            if (buttonRoot.activeSelf != shouldShow) buttonRoot.SetActive(shouldShow);
            if (shouldShow && !wasVisible)
            {
                buttonRoot.transform.SetAsLastSibling();
                Debug.Log("[AwayPlayer] AFK title button visible");
            }
            wasVisible = shouldShow;
        }
    }
}
