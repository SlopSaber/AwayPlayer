using AwayPlayer.Managers;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Utilities;
using System.Linq;
using TMPro;
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
        private const string WHITELIST_BUTTON = "" +
            "<bg><button id='whitelist-button' text='W' font-size='3.5' active='~whitelist-button-active' hover-hint='Adds the current song to the AwayPlayer whitelist' anchor-pos-x='3.5' anchor-pos-y='-16.3' pref-width='6.8' pref-height='5.7' on-click='whitelist-click'/></bg>";

        private static readonly Color SelectedButtonColor = new Color32(0, 150, 255, 255);

        private bool _blacklistButtonActive = true;
        private bool _whitelistButtonActive = true;
        private bool _blacklistSelected;
        private bool _whitelistSelected;
        private ButtonStyle _blacklistStyle;
        private ButtonStyle _whitelistStyle;

        private struct ButtonStyle
        {
            public Button Button;
            public HMUI.ImageView Background;
            public Color BackgroundColor;
            public bool BackgroundGradient;
            public GameObject SelectionBorder;
            public GameObject SelectionOutline;
            public GameObject Underline;
            public bool UnderlineActive;
            public TMP_Text Text;
            public Color TextColor;
        }

        [UIComponent("afk-button")]
        private RectTransform afkButtonTransform { get; set; }

        [UIComponent("blacklist-button")]
        private Button blacklistButton { get; set; }

        [UIComponent("whitelist-button")]
        private Button whitelistButton { get; set; }

        [UIValue("blacklist-button-active")]
        public bool BlacklistButtonActive
        {
            get => _blacklistButtonActive;
            set
            {
                _blacklistButtonActive = value;
                NotifyPropertyChanged();
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
                _blacklistStyle = CaptureStyle(blacklistButton);
            }
            if (_config.WhitelistEnable)
            {
                BSMLParser.Instance.Parse(WHITELIST_BUTTON, levelDetail, this);
                _whitelistStyle = CaptureStyle(whitelistButton);
            }

            var controller = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().First();
            controller.didChangeDifficultyBeatmapEvent += OnDifficultyChanged;
            controller.didChangeContentEvent += OnContentChanged;
        }

        private static ButtonStyle CaptureStyle(Button button)
        {
            if (button == null) return default;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            var background = button.transform.Find("BG")?.GetComponent<HMUI.ImageView>();
            var underline = button.transform.Find("Underline")?.gameObject;
            var style = new ButtonStyle
            {
                Button = button,
                Background = background,
                BackgroundColor = background != null ? background.color : Color.white,
                BackgroundGradient = background != null && background.gradient,
                Underline = underline,
                UnderlineActive = underline != null && underline.activeSelf,
                Text = text,
                TextColor = text != null ? text.color : Color.white,
            };

            foreach (var animation in button.GetComponentsInChildren<HMUI.ButtonStaticAnimations>(true))
                animation.enabled = false;

            if (background != null)
            {
                var primary = new BeatSaberMarkupLanguage.Tags.PrimaryButtonTag().PrefabButton;
                style.SelectionBorder = CreateSelectionDecoration(primary.transform.Find("Border"), background.transform);
                style.SelectionOutline = CreateSelectionDecoration(primary.transform.Find("OutlineWrapper"), background.transform);
            }

            return style;
        }

        private static GameObject CreateSelectionDecoration(Transform template, Transform parent)
        {
            if (template == null) return null;

            var decoration = Object.Instantiate(template.gameObject, parent, false);
            var rect = (RectTransform)decoration.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            foreach (var image in decoration.GetComponentsInChildren<Image>(true))
                image.raycastTarget = false;
            decoration.SetActive(false);
            return decoration;
        }

        private static void SetSelectedStyle(ButtonStyle style, bool selected, string hoverHint)
        {
            if (style.Button == null) return;

            if (style.Background != null)
            {
                style.Background.color = selected ? SelectedButtonColor : style.BackgroundColor;
                style.Background.gradient = selected ? false : style.BackgroundGradient;
            }
            if (style.SelectionBorder != null) style.SelectionBorder.SetActive(selected);
            if (style.SelectionOutline != null) style.SelectionOutline.SetActive(selected);
            if (style.Underline != null) style.Underline.SetActive(!selected && style.UnderlineActive);
            if (style.Text != null)
            {
                style.Text.color = selected && style.Background == null ? SelectedButtonColor : style.TextColor;
            }

            var hint = style.Button.GetComponent<HMUI.HoverHint>();
            if (hint != null) hint.text = hoverHint;
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
                return;
            }

            var blacklist = WBMgr.GetBlacklist();
            var whitelist = WBMgr.GetWhitelist();
            _blacklistSelected = blacklist.Contains(selectedSong);
            _whitelistSelected = whitelist.Contains(selectedSong);
            BlacklistButtonActive = _config.BlacklistEnable;
            WhitelistButtonActive = _config.WhitelistEnable;
            SetSelectedStyle(_blacklistStyle, _blacklistSelected,
                _blacklistSelected ? "Removes the current song from the AwayPlayer blacklist" : "Adds the current song to the AwayPlayer blacklist");
            SetSelectedStyle(_whitelistStyle, _whitelistSelected,
                _whitelistSelected ? "Removes the current song from the AwayPlayer whitelist" : "Adds the current song to the AwayPlayer whitelist");
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

            var wasBlacklisted = WBMgr.GetBlacklist().Contains(selectedSong);
            if (wasBlacklisted)
            {
                WBMgr.RemoveFromBlacklist(selectedSong);
            }
            else
            {
                if (WBMgr.GetWhitelist().Contains(selectedSong)) WBMgr.RemoveFromWhitelist(selectedSong);
                WBMgr.AddToBlacklist(selectedSong);
            }

            SLM.ForceReload();

            if (!wasBlacklisted && _replayManager.Enabled && _replayManager.CurrentScore.Song.Hash == selectedSong)
            {
                _replayManager.SkipCurrentSelection();
            }
            UpdateButtons(selectedSong);
        }

        [UIAction("whitelist-click")]
        public void WhitelistClick()
        {
            var selectedSong = GetSelectedSongHash();
            if (selectedSong == null) return;

            if (WBMgr.GetWhitelist().Contains(selectedSong))
            {
                WBMgr.RemoveFromWhitelist(selectedSong);
            }
            else
            {
                if (WBMgr.GetBlacklist().Contains(selectedSong)) WBMgr.RemoveFromBlacklist(selectedSong);
                WBMgr.AddToWhitelist(selectedSong);
            }

            SLM.ForceReload();
            UpdateButtons(selectedSong);
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
