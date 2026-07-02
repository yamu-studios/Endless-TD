
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ETD.Core
{
    public enum GameState
    {
        Boot,
        Hub,
        Preparation,
        WaveActive,
        LevelUp,
        EvolveChoice,
        Paused,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string _bootScene = "Boot";
        [SerializeField] private string _hubScene = "Hub";
        [SerializeField] private string _gameScene = "Game";


        public GameState CurrentState { get; private set; } = GameState.Boot;
        public GameState PreviousState { get; private set; }

        public bool HasLastGameOverResult { get; private set; }
        public int LastGameOverScore { get; private set; }
        public int LastGameOverWavesCompleted { get; private set; }
        public int LastGameOverSequence { get; private set; }
        public int RunSessionId { get; private set; }

        private GameState _stateBeforeModal;


     
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ServiceLocator.Unregister<GameManager>();
                Instance = null;
            }
        }

        public void PushModalState(GameState modal)
        {
            _stateBeforeModal = CurrentState;
            SetState(modal);
        }

        //public void PopModalState()
        //{
        //    SetState(_stateBeforeModal);
        //}
        public void PopModalState()
        {
            // Only pop if actually in a modal state
            if (CurrentState != GameState.EvolveChoice
             && CurrentState != GameState.LevelUp
             && CurrentState != GameState.Paused)
                return;

            SetState(_stateBeforeModal);
        }


        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;
            PreviousState = CurrentState;
            CurrentState = newState;


            switch (newState)
            {
                case GameState.Preparation:
                    Time.timeScale = 1f;
                    break;
                case GameState.WaveActive:
                    Time.timeScale = 1f;
                    break;
                case GameState.LevelUp:
                    Time.timeScale = 0f;
                    break;
                case GameState.EvolveChoice:
                    Time.timeScale = 0f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
                default:
                    Time.timeScale = 1f;
                    break;
            }

            EventBus.Publish(new GameStateChangedEvent { NewState = (int)newState });
            EventBus.Publish(new GamePausedEvent
            {
                IsPaused = newState == GameState.Paused
                        || newState == GameState.LevelUp
                        || newState == GameState.EvolveChoice
            });
        }

        public void LoadHub()
        {
            HasLastGameOverResult = false;
            LastGameOverScore = 0;
            LastGameOverWavesCompleted = 0;

            AudioManager.Instance?.StopAllGameSounds();

            EventBus.Clear();
            ServiceLocator.Clear();
            ServiceLocator.Register(this);

            SetState(GameState.Hub);

            Time.timeScale = 0f;

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(_hubScene, freezeDuringLoad: true, timeScaleAfterLoad: 1f);
            }
            else
            {
                SceneManager.LoadScene(_hubScene);
                Time.timeScale = 1f;
            }
        }

        public void LoadGame()
        {
            RunSessionId++;
            HasLastGameOverResult = false;
            LastGameOverScore = 0;
            LastGameOverWavesCompleted = 0;

            // Retry/new run must clear demo-complete suppression. Otherwise
            // DemoCompletedThisSession stays true after the first demo-complete run
            // and GameOverUI keeps hiding itself on later deaths.
            DemoMode.ResetCompletionForNewRun();

            SetState(GameState.Preparation);

            // Prevent the old scene from continuing while the loading screen is visible.
            Time.timeScale = 0f;

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(_gameScene, freezeDuringLoad: true, timeScaleAfterLoad: 1f);
            }
            else
            {
                SceneManager.LoadScene(_gameScene);
                Time.timeScale = 1f;
            }
        }

        public void TogglePause()
        {
            if (CurrentState == GameState.Paused)
                SetState(PreviousState);
            else if (CurrentState == GameState.WaveActive || CurrentState == GameState.Preparation)
                SetState(GameState.Paused);
        }

        public void TriggerGameOver(int score, int wavesCompleted)
        {
            LastGameOverSequence++;
            HasLastGameOverResult = true;
            LastGameOverScore = score;
            LastGameOverWavesCompleted = wavesCompleted;

            SetState(GameState.GameOver);
            EventBus.Publish(new GameOverEvent { Score = score, WavesCompleted = wavesCompleted });
        
        }

      
    }
}
