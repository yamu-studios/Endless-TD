// ============================================================================
// ETD.Hub - HubTutorialAnimator.cs  [REWRITTEN v1.0 tutorial redesign]
// On-demand guided flow, started only by clicking the Hub "Tutorial" button
// (see [[etd-v1-full-release]]) — no longer auto-triggers on first Hub visit.
//   1. Open Planning, pulse first unlocked trait until selected
//   2. Pulse the Spells toggle until clicked
//   3. Pulse the first spell item until selected
//   4. Launch the sandboxed tutorial run (GameManager.LoadTutorial)
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ETD.Core;

namespace ETD.Hub
{
    public class HubTutorialAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button           _planningButton;
        [SerializeField] private PlanningWindowUI _planningWindow;
        [SerializeField] private HubBadge         _newBadge;

        [Header("Pulse Settings")]
        [SerializeField] private float _scaleMin    = 0.92f;
        [SerializeField] private float _scaleMax    = 1.10f;
        [SerializeField] private float _scaleMaxInTab    = 1.10f;
        [SerializeField] private float _pulsePeriod = 0.65f;
        [SerializeField] private float _launchDelay = 0.6f;

        private Coroutine _pulseCoroutine;
        private bool      _flowActive;
        private PlanningTabSwitcher _tabSwitcher;

        private void Start()
        {
            RefreshBadge();
        }

        private void RefreshBadge()
        {
            var save = SaveSystem.Load();
            _newBadge?.SetVisible(!save.HubTutorialSeen);
        }

        /// <summary>Called by the Hub "Tutorial" button's onClick.</summary>
        public void StartGuidedFlow()
        {
            if (_flowActive) return;
            _flowActive = true;

            EventBus.Subscribe<HubWindowOpenedEvent>(OnWindowOpened);
            EventBus.Subscribe<SpellsViewToggledEvent>(OnSpellsViewToggled);

            HubController.Instance?.OnPlanningClicked();
            // OnPlanningClicked already publishes HubWindowOpenedEvent(Planning),
            // which OnWindowOpened below reacts to.
        }

        // =================================================================
        // STEP 1: Planning opened -> pulse first unlocked trait item
        // =================================================================

        private void OnWindowOpened(HubWindowOpenedEvent evt)
        {
            if (evt.WindowType != HubWindowType.Planning) return;
            StartCoroutine(WaitAndPulseFirstTrait());
        }

        private IEnumerator WaitAndPulseFirstTrait()
        {
            yield return null; // let OnEnable generate the trait list

            if (_planningWindow == null) { AdvanceToSpells(); yield break; }

            PlanningTabItem firstUnlocked = null;
            foreach (var item in _planningWindow.TabItems)
            {
                if (item != null && item.IsUnlocked && !item.IsSelected)
                {
                    firstUnlocked = item;
                    break;
                }
            }

            if (firstUnlocked == null) { AdvanceToSpells(); yield break; }

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseTransform(firstUnlocked.transform, _scaleMin, _scaleMaxInTab, _pulsePeriod));

            firstUnlocked.OnSelectedCallback += OnTraitSelected;
        }

        private void OnTraitSelected(string traitId)
        {
            StopPulse();
            AdvanceToSpells();
        }

        // =================================================================
        // STEP 2: pulse the Spells toggle until clicked
        // =================================================================

        private void AdvanceToSpells()
        {
            _tabSwitcher = FindFirstObjectByType<PlanningTabSwitcher>();
            var toggle = _tabSwitcher != null ? _tabSwitcher.ToggleButton : null;
            if (toggle == null) { FinishFlow(); return; }

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseTransform(toggle.transform, _scaleMin, _scaleMax, _pulsePeriod));
        }

        private void OnSpellsViewToggled(SpellsViewToggledEvent evt)
        {
            if (!evt.ShowingSpells) return;
            StopPulse();
            StartCoroutine(WaitAndPulseFirstSpell());
        }

        // =================================================================
        // STEP 3: pulse the first spell item until selected
        // =================================================================

        private IEnumerator WaitAndPulseFirstSpell()
        {
            yield return null; // let SpellPlanningUI.Refresh() run

            var spellUI = _tabSwitcher != null ? _tabSwitcher.SpellPlanningUI : null;
            if (spellUI == null) { FinishFlow(); yield break; }

            SpellTabItem firstUnselected = null;
            foreach (var item in spellUI.Items)
            {
                if (item != null && !item.IsSelected)
                {
                    firstUnselected = item;
                    break;
                }
            }

            if (firstUnselected == null) { FinishFlow(); yield break; }

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseTransform(firstUnselected.transform, _scaleMin, _scaleMaxInTab, _pulsePeriod));

            firstUnselected.OnSelectedCallback += OnSpellSelected;
        }

        private void OnSpellSelected(string spellId)
        {
            StopPulse();
            FinishFlow();
        }

        // =================================================================
        // STEP 4: launch the sandboxed tutorial run
        // =================================================================

        private void FinishFlow()
        {
            MarkSeen();
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnWindowOpened);
            EventBus.Unsubscribe<SpellsViewToggledEvent>(OnSpellsViewToggled);
            StartCoroutine(LaunchAfterDelay());
        }

        private IEnumerator LaunchAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_launchDelay);
            GameManager.Instance?.LoadTutorial();
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void MarkSeen()
        {
            _flowActive = false;

            var save = SaveSystem.Load();
            save.HubTutorialSeen = true;
            SaveSystem.Save(save);
            RefreshBadge();
        }

        private void StopPulse()
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        private IEnumerator PulseTransform(Transform t, float min, float max, float period)
        {
            if (t == null) yield break;
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < period)
                {
                    float s = Mathf.Lerp(min, max,
                        (Mathf.Sin(elapsed / period * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f);
                    t.localScale = Vector3.one * s;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnWindowOpened);
            EventBus.Unsubscribe<SpellsViewToggledEvent>(OnSpellsViewToggled);
        }
    }
}
