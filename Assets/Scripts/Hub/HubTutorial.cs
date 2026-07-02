// ============================================================================
// ETD.Hub - HubTutorial.cs  [NEW]
// Shows hub-side tutorial before first run:
//   Step 1 → Open Planning
//   Step 2 → Select a trait
//   Step 3 → Press Play
// Completes when Play is clicked. TutorialManager handles in-game tutorial.
// Add to HubController_GO or a dedicated HubTutorial_GO.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.Hub
{
    public class HubTutorial : MonoBehaviour
    {
        [Header("Popup Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _bodyText;
        [SerializeField] private Button     _gotItButton;
        [SerializeField] private Button     _skipButton;

        [Header("Highlight Frame (optional)")]
        [SerializeField] private RectTransform _highlightFrame;
        [SerializeField] private float         _highlightPadding = 12f;

        [Header("Target UI Elements")]
        [SerializeField] private RectTransform _planningButtonTarget;
        [SerializeField] private RectTransform _traitsListTarget;
        [SerializeField] private RectTransform _playButtonTarget;

        private int  _step;
        private bool _traitSelected;

        private void Start()
        {
            var save = SaveSystem.Load();
            if (save.TutorialCompleted)
            {
                gameObject.SetActive(false);
                return;
            }

            EventBus.Subscribe<HubWindowOpenedEvent>(OnWindowOpened);

            if (_panel != null) _panel.SetActive(false);
            _gotItButton?.onClick.AddListener(OnGotIt);
            _skipButton?.onClick.AddListener(SkipHubTutorial);

            // Slight delay so hub UI finishes loading
            Invoke(nameof(ShowStep0), 0.5f);
        }

        private void ShowStep0()
        {
            ShowStep(
                title:  "Welcome to Endless Tower Defense!",
                body:   "Before your run, visit Planning to select Traits. Traits give permanent bonuses during your run!",
                target: _planningButtonTarget
            );
            _step = 0;
        }

        private void OnWindowOpened(HubWindowOpenedEvent evt)
        {
            if (_step == 0 && evt.WindowType == HubWindowType.Planning)
            {
                ShowStep(
                    title:  "Select Your Traits",
                    body:   "Check the trait list and click the checkmark to select one. Active traits appear in the Selected section below.",
                    target: _traitsListTarget
                );
                _step = 1;
            }
        }

        // Called by HubController when Play is clicked
        public bool CheckAndAdvanceTutorial()
        {
            if (_step < 2)
            {
                ShowStep(
                    title:  "Ready to Play!",
                    body:   "Good. Click Play to start your first run. The in-game tutorial will guide you through the rest.",
                    target: _playButtonTarget
                );
                _step = 2;
                return false; // Block play until they acknowledge
            }
            return true; // Allow play
        }

        private void OnGotIt()
        {
            if (_step == 2)
            {
                // Let play proceed
                Hide();
                HubController.Instance?.OnPlayClicked();
            }
            else
            {
                Hide();
            }
        }

        private void ShowStep(string title, string body, RectTransform target)
        {
            if (_panel != null) _panel.SetActive(true);
            if (_titleText != null) _titleText.text = title;
            if (_bodyText  != null) _bodyText.text  = body;
            PositionHighlight(target);
        }

        private void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_highlightFrame != null) _highlightFrame.gameObject.SetActive(false);
        }

        public void SkipHubTutorial()
        {
            Hide();
            var save = SaveSystem.Load();
            save.TutorialCompleted = true;
            SaveSystem.Save(save);
            gameObject.SetActive(false);
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnWindowOpened);
        }

        private void PositionHighlight(RectTransform target)
        {
            if (_highlightFrame == null || target == null) return;
            _highlightFrame.gameObject.SetActive(target != null);
            if (target == null) return;

            var canvas = _highlightFrame.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var c in corners)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, c);
                if (s.x < minX) minX = s.x;
                if (s.y < minY) minY = s.y;
                if (s.x > maxX) maxX = s.x;
                if (s.y > maxY) maxY = s.y;
            }

            float p = _highlightPadding;
            _highlightFrame.position  = new Vector3((minX+maxX)/2f, (minY+maxY)/2f, 0f);
            _highlightFrame.sizeDelta = new Vector2(maxX-minX+p*2f, maxY-minY+p*2f);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HubWindowOpenedEvent>(OnWindowOpened);
        }
    }
}
