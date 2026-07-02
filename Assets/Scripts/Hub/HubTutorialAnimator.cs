// ============================================================================
// ETD.Hub - HubTutorialAnimator.cs  [NEW]
// First-ever hub visit:
//   1. Pulses Planning button scale until player clicks it
//   2. After Planning opens, pulses first unlocked trait's checkmark until selected
//   3. Done — marks hub tutorial seen
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

        [Header("Pulse Settings")]
        [SerializeField] private float _scaleMin    = 0.92f;
        [SerializeField] private float _scaleMax    = 1.10f;
        [SerializeField] private float _scaleMaxInTab    = 1.10f;
        [SerializeField] private float _pulsePeriod = 0.65f;

        private Coroutine _pulseCoroutine;
        private bool      _hubTutorialDone;

        private void Start()
        {
            var save = SaveSystem.Load();
            if (save.HubTutorialSeen) return;

            EventBus.Subscribe<HubWindowOpenedEvent>(OnHubWindowOpened);
            StartPlanningPulse();
        }

        // =================================================================
        // STEP 1: Pulse Planning button
        // =================================================================

        private void StartPlanningPulse()
        {
            if (_planningButton == null) return;
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseTransform(
                _planningButton.transform, _scaleMin, _scaleMax, _pulsePeriod));
        }

        // =================================================================
        // STEP 2: Planning opened → pulse first unlocked trait item
        // =================================================================

        private void OnHubWindowOpened(HubWindowOpenedEvent evt)
        {
            if (evt.WindowType != HubWindowType.Planning) return;

            // Stop planning button pulse
            StopPulse(_planningButton?.transform);

            // Wait one frame so PlanningWindowUI generates items
            StartCoroutine(WaitAndPulseFirstTrait());
        }

        private IEnumerator WaitAndPulseFirstTrait()
        {
            yield return null; // let OnEnable generate list

            if (_planningWindow == null) yield break;

            // Find first unlocked, unselected trait item
            PlanningTabItem firstUnlocked = null;
            foreach (var item in _planningWindow.TabItems)
            {
                if (item != null && item.IsUnlocked && !item.IsSelected)
                {
                    firstUnlocked = item;
                    break;
                }
            }

            if (firstUnlocked == null) { MarkDone(); yield break; }

            // Pulse the checkmark button specifically
            Transform target =  firstUnlocked.transform; //firstUnlocked.CheckmarkButtonTransform ??

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseTransform(target, _scaleMin, _scaleMaxInTab, _pulsePeriod));

            // Wait for trait to be selected
            firstUnlocked.OnSelectedCallback += OnTraitSelected;
        }

        private void OnTraitSelected(string traitId)
        {
            StopPulse(null);
            MarkDone();
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void MarkDone()
        {
            _hubTutorialDone = true;
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnHubWindowOpened);

            var save = SaveSystem.Load();
            save.HubTutorialSeen = true;
            SaveSystem.Save(save);
        }

        private void StopPulse(Transform t)
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;

            if (t != null) t.localScale = Vector3.one;
            // Also reset planning button
            if (_planningButton != null)
                _planningButton.transform.localScale = Vector3.one;
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
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnHubWindowOpened);
        }
    }
}
