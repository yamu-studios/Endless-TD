// Temporary debug probe for Endless Defense GameOver retry bug.
// Add this to an always-active object in the Game scene, e.g. GameManagers.
// Remove it after the bug is found.

using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ETD.Core;
using ETD.UI;

namespace ETD.Debugging
{
    public sealed class GameOverDebugProbe : MonoBehaviour
    {
        [SerializeField] private bool logEverySceneLoad = true;
        [SerializeField] private bool logEveryStateChange = true;
        [SerializeField] private bool logUiDetailsOnGameOver = true;

        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo PanelField = typeof(GameOverUI).GetField("_panel", Flags);
        private static readonly FieldInfo SuppressField = typeof(GameOverUI).GetField("_suppressForDemoComplete", Flags);
        private static readonly FieldInfo SubscribedField = typeof(GameOverUI).GetField("_subscribed", Flags);
        private static readonly FieldInfo ShownSeqField = typeof(GameOverUI).GetField("_shownGameOverSequence", Flags);

        private int _lastLoggedGameOverSequence = -1;

        private void Awake()
        {
            Debug.Log($"[GO-PROBE] Awake on '{name}' scene='{gameObject.scene.name}' active={gameObject.activeInHierarchy} timeScale={Time.timeScale}");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStartedEvent>(OnGameStarted);
            EventBus.Subscribe<RetryEvent>(OnRetry);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log($"[GO-PROBE] OnEnable scene='{SceneManager.GetActiveScene().name}'");
            DumpGameManager("OnEnable");
            DumpGameOverUis("OnEnable");
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStartedEvent>(OnGameStarted);
            EventBus.Unsubscribe<RetryEvent>(OnRetry);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Debug.Log($"[GO-PROBE] OnDisable scene='{SceneManager.GetActiveScene().name}'");
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.GameOver)
                return;

            if (_lastLoggedGameOverSequence == gm.LastGameOverSequence)
                return;

            _lastLoggedGameOverSequence = gm.LastGameOverSequence;
            Debug.Log($"[GO-PROBE] LateUpdate detected GameOver sequence={gm.LastGameOverSequence}. Dumping UI now.");
            DumpGameManager("LateUpdateGameOver");
            DumpGameOverUis("LateUpdateGameOver");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!logEverySceneLoad)
                return;

            Debug.Log($"[GO-PROBE] SceneLoaded scene='{scene.name}' mode={mode} timeScale={Time.timeScale}");
            DumpGameManager("SceneLoaded");
            DumpGameOverUis("SceneLoaded");
        }

        private void OnGameStarted(GameStartedEvent evt)
        {
            Debug.Log("[GO-PROBE] Event: GameStartedEvent");
            DumpGameManager("GameStartedEvent");
            DumpGameOverUis("GameStartedEvent");
        }

        private void OnRetry(RetryEvent evt)
        {
            Debug.Log("[GO-PROBE] Event: RetryEvent");
            DumpGameManager("RetryEvent");
            DumpGameOverUis("RetryEvent");
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (!logEveryStateChange)
                return;

            Debug.Log($"[GO-PROBE] Event: GameStateChangedEvent newState={(GameState)evt.NewState}");
            DumpGameManager("StateChanged");

            if ((GameState)evt.NewState == GameState.GameOver)
                StartCoroutine(DumpAfterFrames("StateChangedGameOver"));
        }

        private void OnGameOver(GameOverEvent evt)
        {
            Debug.Log($"[GO-PROBE] Event: GameOverEvent score={evt.Score} waves={evt.WavesCompleted}");
            DumpGameManager("GameOverEvent");
            if (logUiDetailsOnGameOver)
                StartCoroutine(DumpAfterFrames("GameOverEvent"));
        }

        private IEnumerator DumpAfterFrames(string reason)
        {
            DumpGameOverUis(reason + " immediate");
            yield return null;
            DumpGameOverUis(reason + " +1 frame");
            yield return null;
            DumpGameOverUis(reason + " +2 frames");
        }

        private static void DumpGameManager(string reason)
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.Log($"[GO-PROBE] {reason}: GameManager.Instance=NULL timeScale={Time.timeScale}");
                return;
            }

            Debug.Log($"[GO-PROBE] {reason}: GM state={gm.CurrentState} prev={gm.PreviousState} runSession={gm.RunSessionId} hasLastGO={gm.HasLastGameOverResult} goSeq={gm.LastGameOverSequence} score={gm.LastGameOverScore} waves={gm.LastGameOverWavesCompleted} timeScale={Time.timeScale}");
        }

        private static void DumpGameOverUis(string reason)
        {
#if UNITY_2023_1_OR_NEWER
            var uis = Object.FindObjectsByType<GameOverUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var uis = Object.FindObjectsOfType<GameOverUI>(true);
#endif
            Debug.Log($"[GO-PROBE] {reason}: GameOverUI count={uis.Length}");

            for (int i = 0; i < uis.Length; i++)
            {
                var ui = uis[i];
                if (ui == null)
                {
                    Debug.Log($"[GO-PROBE]   UI[{i}] NULL");
                    continue;
                }

                GameObject panel = null;
                if (PanelField != null)
                    panel = PanelField.GetValue(ui) as GameObject;

                bool suppress = SuppressField != null && SuppressField.GetValue(ui) is bool s && s;
                bool subscribed = SubscribedField != null && SubscribedField.GetValue(ui) is bool sub && sub;
                int shownSeq = ShownSeqField != null && ShownSeqField.GetValue(ui) is int seq ? seq : -999;

                var parentCanvas = ui.GetComponentInParent<Canvas>(true);
                var panelCanvasGroup = panel != null ? panel.GetComponent<CanvasGroup>() : null;

                Debug.Log(
                    $"[GO-PROBE]   UI[{i}] name='{ui.name}' scene='{ui.gameObject.scene.name}' " +
                    $"uiActiveSelf={ui.gameObject.activeSelf} uiActiveHierarchy={ui.gameObject.activeInHierarchy} enabled={ui.enabled} " +
                    $"subscribed={subscribed} suppressDemo={suppress} shownSeq={shownSeq} " +
                    $"panel={(panel != null ? panel.name : "NULL")} " +
                    $"panelActiveSelf={(panel != null ? panel.activeSelf.ToString() : "n/a")} " +
                    $"panelActiveHierarchy={(panel != null ? panel.activeInHierarchy.ToString() : "n/a")} " +
                    $"panelCanvasGroupAlpha={(panelCanvasGroup != null ? panelCanvasGroup.alpha.ToString("0.###") : "n/a")} " +
                    $"canvas={(parentCanvas != null ? parentCanvas.name : "NULL")} " +
                    $"canvasActive={(parentCanvas != null ? parentCanvas.gameObject.activeInHierarchy.ToString() : "n/a")} " +
                    $"canvasSorting={(parentCanvas != null ? parentCanvas.sortingOrder.ToString() : "n/a")}");
            }
        }
    }
}
