using AwayPlayer.Managers;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Utilities;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

#pragma warning disable CS0649 // Value is never assigend to - We have zenject
namespace AwayPlayer.UI
{
    internal class MenuButtonManager : BSMLAutomaticViewController, IInitializable
    {
        private const float TitleUnderlineHeightScale = 1.6f;
        private const string AFK_BUTTON = "" +
            "<button id='afk-button' text='AFK' font-size='3.5' word-wrapping='false' anchor-pos-x='69' anchor-pos-y='-7' pref-width='10.5' pref-height='7' on-click='afk-click'/>";
        private const string BLACKLIST_BUTTON = "" +
            "<bg><button id='blacklist-button' text='B' font-size='3.5' active='~blacklist-button-active' hover-hint='Adds the current song to the AwayPlayer backlist' anchor-pos-x='10.5' anchor-pos-y='-16.3' pref-width='6.8' pref-height='5.7' on-click='blacklist-click'/></bg>";
        private const string PRIMARY_BLACKLIST_BUTTON = "" +
            "<bg><primary-button id='primary-blacklist-button' active='~primary-blacklist-button-active' text='B' font-size='3.5' hover-hint='Removes the current song to the AwayPlayer backlist' anchor-pos-x='10.5' anchor-pos-y='-16.3' pref-width='6.8' pref-height='5.7' on-click='primary-blacklist-click'/></bg>";
        private const string WHITELIST_BUTTON = "" +
            "<bg><button id='whitelist-button' text='W' font-size='3.5' active='~whitelist-button-active' hover-hint='Adds the current song to the AwayPlayer whitelist' anchor-pos-x='3.5' anchor-pos-y='-16.3' pref-width='6.8' pref-height='5.7' on-click='whitelist-click'/></bg>";
        private const string PRIMARY_WHITELIST_BUTTON = "" +
            "<bg><primary-button id='primary-whitelist-button' active='~primary-whitelist-button-active' text='W' font-size='3.5' hover-hint='Removes the current song to the AwayPlayer whitelist' anchor-pos-x='3.5' anchor-pos-y='-16.3' pref-width='6.8' pref-height='5.7' on-click='primary-whitelist-click'/></bg>";

        private bool _blacklistButtonActive = true;
        private bool _whitelistButtonActive = true;
        private bool _primaryBlacklistButtonActive = false;
        private bool _primaryWhitelistButtonActive = false;

        [UIComponent("afk-button")]
        private RectTransform afkButtonTransform { get; set; }

        [UIComponent("blacklist-button")]
        private RectTransform blacklistButtonTransform { get; set; }

        [UIComponent("primary-blacklist-button")]
        private RectTransform primaryBlacklistButtonTransform { get; set; }

        [UIComponent("whitelist-button")]
        private RectTransform whitelistButtonTransform { get; set; }

        [UIComponent("primary-whitelist-button")]
        private RectTransform primaryWhitelistButtonTransform { get; set; }

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
            var underline = afkButtonTransform.Find("Underline");
            if (underline != null)
            {
                var effect = underline.gameObject.AddComponent<TitleUnderlineHeightEffect>();
                effect.HeightScale = TitleUnderlineHeightScale;
            }
            var afkRoot = afkButtonTransform.gameObject;
            title.gameObject.AddComponent<AfkTitleButtonVisibility>().Initialize(afkRoot, levelSelection.gameObject);
            var standardLevel = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First(); // Stolen from song core, thank you ;)
            var levelDetail = standardLevel.transform.Find("LevelDetail").gameObject;
            if (_config.BlacklistEnable)
            {
                BSMLParser.Instance.Parse(BLACKLIST_BUTTON, levelDetail, this);
                BSMLParser.Instance.Parse(PRIMARY_BLACKLIST_BUTTON, levelDetail, this);
                AlignSelectedButton(blacklistButtonTransform, primaryBlacklistButtonTransform);
            }
            if (_config.WhitelistEnable)
            {
                BSMLParser.Instance.Parse(WHITELIST_BUTTON, levelDetail, this);
                BSMLParser.Instance.Parse(PRIMARY_WHITELIST_BUTTON, levelDetail, this);
                AlignSelectedButton(whitelistButtonTransform, primaryWhitelistButtonTransform);
            }

            var controller = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
            controller.didChangeDifficultyBeatmapEvent += OnDifficultyChanged;
            controller.didChangeContentEvent += OnContentChanged;
        }

        private static void AlignSelectedButton(RectTransform regular, RectTransform selected)
        {
            if (regular == null || selected == null) return;
            selected.SetParent(regular.parent, false);
            selected.anchorMin = regular.anchorMin;
            selected.anchorMax = regular.anchorMax;
            selected.pivot = regular.pivot;
            selected.anchoredPosition = regular.anchoredPosition;
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

    internal sealed class TitleUnderlineHeightEffect : BaseMeshEffect
    {
        public float HeightScale { get; set; } = 1f;

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            float bottom = graphic.rectTransform.rect.yMin;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                var position = vertex.position;
                position.y = bottom + (position.y - bottom) * HeightScale;
                vertex.position = position;
                vertices.SetUIVertex(vertex, i);
            }
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
