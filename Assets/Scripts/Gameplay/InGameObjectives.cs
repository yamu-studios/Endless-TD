// ============================================================================
// ETD.Gameplay - InGameObjectives.cs  [REWRITTEN v1.0 tutorial redesign]
// Corner objective checklist driving the sandboxed practice tutorial (see
// [[etd-v1-full-release]]). Gated on GameManager.IsTutorialMode instead of the
// old "!save.TutorialCompleted" auto-trigger — this component only ever runs
// during a dedicated tutorial session now, launched from the Hub.
//
// Turret leveling toward evolution thresholds is scripted (repeated calls to
// the same public TurretController.Upgrade() a real Upgrade-button click would
// use — it doesn't check gold itself, the caller does, so calling it directly
// here is a legitimate "free upgrade" in this sandboxed, infinite-gold run,
// not a special-cased method). Tier1 evolution is still a real player choice
// (Path A/B click); Tier2 evolves automatically with no confirm step, matching
// real gameplay (see TurretController.Upgrade()).
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Gameplay;
using ETD.Turrets;

namespace ETD.Gameplay
{
    public class InGameObjectives : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _objectiveText;
        [SerializeField] private Image _objectiveIcon;
        [SerializeField] private GameObject _completeIndicator;
        [SerializeField] private Sprite _tabKeyIcon;

        [Header("Timing")]
        [SerializeField] private float _completeShowDuration = 1.2f;
        [SerializeField] private float _nextObjectiveDelay = 0.5f;

        private int _currentObjective = 0;
        private bool _active = false;

        private int _turretsPlaced;
        private int _firstTurretId = -1;
        private TurretManager _turretManager;

        private static readonly string[] ObjectiveKeys =
        {
            "tutorial_objective_build_turret",
            "tutorial_objective_build_maze",
            "tutorial_objective_upgrade_turret",
            "tutorial_objective_evolve_choice",
            "tutorial_objective_evolve_tier2",
            "tutorial_objective_level_up_spec_card",
            "tutorial_objective_cast_spell",
            "tutorial_objective_tab_stats",
        };

        private static readonly string[] ObjectiveFallbacks =
        {
            "Build a turret",
            "Place 2 more turrets to build a maze",
            "Select your turret and press Q to upgrade it",
            "Your turret can evolve at level {0}! Choose a path",
            "It will automatically evolve again at level {0}!",
            "Defeat enemies, level up, and choose a Spec Card",
            "Press the spell button to cast your chosen spell",
            "Press Tab to see your Spec Card stats",
        };

        private const int StepBuildTurret = 0;
        private const int StepBuildMaze = 1;
        private const int StepUpgradeTurret = 2;
        private const int StepEvolveChoice = 3;
        private const int StepEvolveTier2 = 4;
        private const int StepLevelUpSpecCard = 5;
        private const int StepCastSpell = 6;
        private const int StepTabStats = 7;

        /// <summary>
        /// Gates real kill-XP level-ups during the tutorial so the player can't level
        /// up before the objective sequence actually reaches the LevelUp step. Always
        /// true outside tutorial mode (see RunManager.AddXP).
        /// </summary>
        public static bool TutorialLevelUpGateOpen { get; private set; } = true;

        private void Awake()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void Start()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsTutorialMode)
            {
                gameObject.SetActive(false);
                return;
            }

            ServiceLocator.TryGet(out _turretManager);

            if (_completeIndicator != null) _completeIndicator.SetActive(false);
            EnsureExitButton();

            TutorialLevelUpGateOpen = false;

            SubscribeEvents();
            _active = true;
            ShowObjective(0);
        }

        private void Update()
        {
            if (!_active) return;

            if (_currentObjective == StepTabStats && UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                CompleteCurrentObjective();
        }

        // =================================================================
        // SHOW OBJECTIVE
        // =================================================================

        private void ShowObjective(int index)
        {
            _currentObjective = index;

            if (index >= ObjectiveKeys.Length) { CompleteTutorial(); return; }

            if (_panel != null) _panel.SetActive(true);
            if (_completeIndicator != null) _completeIndicator.SetActive(false);

            bool isTabStep = index == StepTabStats;
            if (_objectiveIcon != null)
            {
                _objectiveIcon.gameObject.SetActive(isTabStep && _tabKeyIcon != null);
                if (isTabStep && _tabKeyIcon != null) _objectiveIcon.sprite = _tabKeyIcon;
            }

            if (_objectiveText != null)
                _objectiveText.text = BuildObjectiveText(index);

            if (index == StepLevelUpSpecCard)
                TutorialLevelUpGateOpen = true;

            // Count-based steps can already be satisfied by the time we enter them —
            // e.g. a fast player placing 3 turrets within the ~1.7s completion-animation
            // window of step 0 would otherwise leave step 1 waiting forever for a 4th
            // TurretPlacedEvent that never comes. Re-check on entry, not just on event.
            if (index == StepBuildTurret && _turretsPlaced >= 1)
                CompleteCurrentObjective();
            else if (index == StepBuildMaze && _turretsPlaced >= 3)
                CompleteCurrentObjective();
            else if (index == StepEvolveTier2)
            {
                // Tier2 now evolves automatically the instant the turret hits the
                // level threshold (no confirm button), which can easily happen
                // while we're still mid-animation on the EvolveChoice step above.
                var t = ResolveFirstTurret();
                if (t != null && t.IsEvolvedTier2)
                    CompleteCurrentObjective();
            }
        }

        /// <summary>
        /// Evolve objective text includes the turret's actual evolution level
        /// instead of generic wording.
        /// </summary>
        private string BuildObjectiveText(int index)
        {
            if (index == StepEvolveChoice || index == StepEvolveTier2)
            {
                var turret = ResolveFirstTurret();
                int level = index == StepEvolveChoice
                    ? (turret != null && turret.Data != null ? turret.Data.EvolveLevel : 0)
                    : (turret != null && turret.Data != null ? turret.Data.EvolveLevel2 : 0);
                return LocalizationManager.GetFormat(ObjectiveKeys[index], ObjectiveFallbacks[index], level);
            }
            return LocalizationManager.Get(ObjectiveKeys[index], ObjectiveFallbacks[index]);
        }

        // =================================================================
        // COMPLETE — guard against inactive GO before StartCoroutine
        // =================================================================

        private void CompleteCurrentObjective()
        {
            if (!_active) return;
            if (!gameObject.activeInHierarchy) return;
            _active = false;
            StartCoroutine(ShowCompleteAndAdvance());
        }

        private IEnumerator ShowCompleteAndAdvance()
        {
            if (_completeIndicator != null) _completeIndicator.SetActive(true);

            yield return new WaitForSecondsRealtime(_completeShowDuration);
            yield return new WaitForSecondsRealtime(_nextObjectiveDelay);

            _active = true;
            ShowObjective(_currentObjective + 1);
        }

        // =================================================================
        // EVENT HANDLERS — all guard _active first
        // =================================================================

        private void OnTurretPlaced(TurretPlacedEvent evt)
        {
            _turretsPlaced++;

            if (_firstTurretId < 0)
                _firstTurretId = evt.TurretId;

            if (!_active) return;

            if (_currentObjective == StepBuildTurret && _turretsPlaced >= 1)
                CompleteCurrentObjective();
            else if (_currentObjective == StepBuildMaze && _turretsPlaced >= 3)
                CompleteCurrentObjective();
        }

        private void OnTurretUpgraded(TurretUpgradedEvent evt)
        {
            if (!_active || _currentObjective != StepUpgradeTurret) return;

            // Accept a Q-press on ANY placed turret, not just the first one built.
            // Whichever turret the player actually upgrades becomes the tracked
            // turret for the rest of the flow (evolve steps below).
            _firstTurretId = evt.TurretId;

            // Player pressed Q for real once — script-jump the rest of the way to
            // this turret's Tier1 evolve threshold using the same public Upgrade()
            // a real click would call (it doesn't check gold; the UI caller does,
            // and this session has infinite gold anyway).
            var turret = ResolveFirstTurret();
            if (turret != null && turret.Data != null)
            {
                while (turret.Level < turret.Data.EvolveLevel && !turret.IsEvolved)
                    turret.Upgrade();
            }

            CompleteCurrentObjective();
        }

        private void OnTurretEvolved(TurretEvolvedEvent evt)
        {
            if (!_active || evt.TurretId != _firstTurretId) return;

            var turret = ResolveFirstTurret();
            if (turret == null) return;

            if (_currentObjective == StepEvolveChoice)
            {
                // Evolutions no longer pause the game, so grind straight to the
                // Tier2 threshold here. If that already flips IsEvolvedTier2 before
                // the StepEvolveTier2 objective is even shown, ShowObjective's
                // entry re-check (above) catches it and completes the step.
                if (turret.Data != null)
                {
                    while (turret.Level < turret.Data.EvolveLevel2 && !turret.IsEvolvedTier2)
                        turret.Upgrade();
                }
                CompleteCurrentObjective();
            }
            else if (_currentObjective == StepEvolveTier2 && turret.IsEvolvedTier2)
            {
                CompleteCurrentObjective();
            }
        }

        private TurretController ResolveFirstTurret()
        {
            if (_firstTurretId < 0) return null;
            if (_turretManager == null) ServiceLocator.TryGet(out _turretManager);
            return _turretManager != null ? _turretManager.GetTurret(_firstTurretId) : null;
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            if (!_active || _currentObjective != StepLevelUpSpecCard) return;
            // Wait for the actual spec-card pick, not just the level-up itself —
            // OnSpecCardChosen below completes this step.
        }

        private void OnSpecCardChosen(SpecCardChosenEvent evt)
        {
            if (!_active || _currentObjective != StepLevelUpSpecCard) return;
            CompleteCurrentObjective();
        }

        private void OnSpellCast(SpellCastEvent evt)
        {
            if (!_active || _currentObjective != StepCastSpell) return;
            CompleteCurrentObjective();
        }

        // =================================================================
        // COMPLETE TUTORIAL / EXIT
        // =================================================================

        private void CompleteTutorial()
        {
            _active = false;
            UnsubscribeEvents();
            TutorialLevelUpGateOpen = true;

            if (_panel != null) _panel.SetActive(false);
            if (_endTutorialButtonGO != null) _endTutorialButtonGO.SetActive(false);

            ShowCompletionPanel();
        }

        public void ExitTutorial()
        {
            _active = false;
            UnsubscribeEvents();
            TutorialLevelUpGateOpen = true;
            GameManager.Instance?.LoadHub();
        }

        private TMP_Text _endTutorialLabel;
        private GameObject _endTutorialButtonGO;

        /// <summary>
        /// Runtime "End Tutorial" button, bottom-right of the screen, so the player
        /// can bail out of the practice run early. Built inline rather than via
        /// ETD.UI.PanelCloseButton — that class lives in the ETD.UI assembly, which
        /// already references ETD.Gameplay, so referencing it back here would be a
        /// circular assembly dependency. Parented to the root canvas (not _panel)
        /// so it sits in the screen corner rather than the small objectives widget.
        /// </summary>
        private void EnsureExitButton()
        {
            if (_panel == null) return;

            var canvas = _panel.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : _panel.transform;

            const string buttonName = "[EndTutorialButton]";
            if (parent.Find(buttonName) != null) return;

            var buttonGO = new GameObject(buttonName, typeof(RectTransform));
            buttonGO.transform.SetParent(parent, false);
            buttonGO.layer = _panel.layer;

            var rect = (RectTransform)buttonGO.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(170f, 42f);
            rect.anchoredPosition = new Vector2(-20f, 20f);

            var background = buttonGO.AddComponent<Image>();
            background.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.22f, 0.22f, 1f);
            colors.pressedColor = new Color(0.55f, 0.12f, 0.12f, 1f);
            button.colors = colors;
            button.onClick.AddListener(ExitTutorial);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            labelGO.layer = _panel.layer;

            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = LocalizationManager.Get("tutorial_end_button", "End Tutorial");
            label.fontSize = 16f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            label.raycastTarget = false;
            _endTutorialLabel = label;
            _endTutorialButtonGO = buttonGO;

            buttonGO.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Runtime "Tutorial Complete" panel shown once all objectives are done —
        /// replaces the old auto-return-to-hub-after-a-delay flow with an explicit
        /// "Return to Menu" button. Built inline for the same reason as
        /// EnsureExitButton (avoids a circular ETD.UI &lt;-&gt; ETD.Gameplay dependency).
        /// </summary>
        private void ShowCompletionPanel()
        {
            // includeInactive: true — CompleteTutorial() deactivates _panel just
            // before calling this, and GetComponentInParent skips inactive objects
            // by default, which would otherwise make this silently find nothing.
            var canvas = _panel != null ? _panel.GetComponentInParent<Canvas>(true) : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            Transform parent = canvas.transform;

            var dimmerGO = new GameObject("[TutorialCompletePanel]", typeof(RectTransform));
            dimmerGO.transform.SetParent(parent, false);
            dimmerGO.layer = gameObject.layer;

            var dimmerRect = (RectTransform)dimmerGO.transform;
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;

            var dimmer = dimmerGO.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.6f);

            var boxGO = new GameObject("Box", typeof(RectTransform));
            boxGO.transform.SetParent(dimmerGO.transform, false);
            boxGO.layer = gameObject.layer;

            var boxRect = (RectTransform)boxGO.transform;
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(460f, 260f);
            boxRect.anchoredPosition = Vector2.zero;

            var boxImage = boxGO.AddComponent<Image>();
            boxImage.color = new Color(0.11f, 0.12f, 0.15f, 0.98f);

            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(boxGO.transform, false);
            titleGO.layer = gameObject.layer;
            var titleRect = (RectTransform)titleGO.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-40f, 60f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = LocalizationManager.Get("tutorial_complete_title", "Tutorial Complete!");
            title.fontSize = 28f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(1f, 0.86f, 0.4f, 1f);
            title.raycastTarget = false;

            var bodyGO = new GameObject("Body", typeof(RectTransform));
            bodyGO.transform.SetParent(boxGO.transform, false);
            bodyGO.layer = gameObject.layer;
            var bodyRect = (RectTransform)bodyGO.transform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.sizeDelta = new Vector2(-60f, 70f);
            bodyRect.anchoredPosition = new Vector2(0f, -100f);
            var body = bodyGO.AddComponent<TextMeshProUGUI>();
            body.text = LocalizationManager.Get("tutorial_complete_message", "Great work! You've learned the basics.");
            body.fontSize = 18f;
            body.alignment = TextAlignmentOptions.Center;
            body.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            body.enableWordWrapping = true;
            body.raycastTarget = false;

            var buttonGO = new GameObject("ReturnButton", typeof(RectTransform));
            buttonGO.transform.SetParent(boxGO.transform, false);
            buttonGO.layer = gameObject.layer;
            var buttonRect = (RectTransform)buttonGO.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(220f, 52f);
            buttonRect.anchoredPosition = new Vector2(0f, 28f);

            var buttonBg = buttonGO.AddComponent<Image>();
            buttonBg.color = new Color(0.2f, 0.55f, 0.3f, 1f);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonBg;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.27f, 0.7f, 0.38f, 1f);
            colors.pressedColor = new Color(0.16f, 0.42f, 0.23f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => GameManager.Instance?.LoadHub());

            var buttonLabelGO = new GameObject("Label", typeof(RectTransform));
            buttonLabelGO.transform.SetParent(buttonGO.transform, false);
            buttonLabelGO.layer = gameObject.layer;
            var buttonLabelRect = (RectTransform)buttonLabelGO.transform;
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;
            var buttonLabel = buttonLabelGO.AddComponent<TextMeshProUGUI>();
            buttonLabel.text = LocalizationManager.Get("tutorial_return_to_menu_button", "Return to Menu");
            buttonLabel.fontSize = 18f;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.color = Color.white;
            buttonLabel.raycastTarget = false;

            dimmerGO.transform.SetAsLastSibling();
        }

        private void SubscribeEvents()
        {
            EventBus.Subscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Subscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Subscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Subscribe<SpellCastEvent>(OnSpellCast);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<TurretPlacedEvent>(OnTurretPlaced);
            EventBus.Unsubscribe<TurretUpgradedEvent>(OnTurretUpgraded);
            EventBus.Unsubscribe<TurretEvolvedEvent>(OnTurretEvolved);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<SpecCardChosenEvent>(OnSpecCardChosen);
            EventBus.Unsubscribe<SpellCastEvent>(OnSpellCast);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            if (_active && _currentObjective >= 0 && _currentObjective < ObjectiveKeys.Length && _objectiveText != null)
                _objectiveText.text = BuildObjectiveText(_currentObjective);
            if (_endTutorialLabel != null)
                _endTutorialLabel.text = LocalizationManager.Get("tutorial_end_button", "End Tutorial");
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }
    }
}
