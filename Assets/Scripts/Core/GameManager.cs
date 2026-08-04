using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hermes.GameEngine.Core
{
    /// <summary>
    /// Identifiers for the core game lifecycle states.
    /// </summary>
    public enum GameStateId
    {
        /// <summary>Game is initializing — loading configs, connecting services, etc.</summary>
        Init,
        /// <summary>Active gameplay — the player is playing.</summary>
        Gameplay,
        /// <summary>Game is paused — timescale frozen, menu visible.</summary>
        Pause,
        /// <summary>Game has ended — showing results/score screen.</summary>
        GameOver
    }

    /// <summary>
    /// Central game manager implementing the singleton pattern.
    /// Drives the core game state machine (Init → Gameplay → Pause/GameOver → Init).
    /// Handles application focus, pause, and quit events.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static GameManager _instance;

        /// <summary>
        /// Gets the singleton instance of GameManager.
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GameManager]");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // State machine
        // ------------------------------------------------------------------

        private StateMachine<GameStateId> _stateMachine;

        /// <summary>
        /// The currently active game state identifier.
        /// </summary>
        public GameStateId CurrentState => _stateMachine != null && _stateMachine.IsInitialized
            ? _stateMachine.CurrentStateId
            : GameStateId.Init;

        /// <summary>
        /// Fired whenever the game state changes. Parameters: (fromState, toState).
        /// </summary>
        public event Action<GameStateId, GameStateId> OnGameStateChanged;

        // ------------------------------------------------------------------
        // Callbacks
        // ------------------------------------------------------------------

        /// <summary>
        /// Fired after initialization completes (state moves out of Init).
        /// </summary>
        public event Action OnGameInitialized;

        /// <summary>
        /// Fired when gameplay starts (state becomes Gameplay).
        /// </summary>
        public event Action OnGameplayStarted;

        /// <summary>
        /// Fired when the game is paused.
        /// </summary>
        public event Action OnGamePaused;

        /// <summary>
        /// Fired when the game is resumed from pause.
        /// </summary>
        public event Action OnGameResumed;

        /// <summary>
        /// Fired when the game ends (state becomes GameOver).
        /// </summary>
        public event Action OnGameOver;

        /// <summary>
        /// Fired just before the application quits. Use for final save / analytics flush.
        /// </summary>
        public event Action OnApplicationQuitting;

        // ------------------------------------------------------------------
        // Settings
        // ------------------------------------------------------------------

        [Header("Scene Names")]
        [Tooltip("Name of the initial/boot scene (loaded on Init).")]
        [SerializeField] private string _bootSceneName = "Boot";

        [Tooltip("Name of the main menu scene.")]
        [SerializeField] private string _menuSceneName = "MainMenu";

        [Tooltip("Name of the gameplay scene.")]
        [SerializeField] private string _gameplaySceneName = "Gameplay";

        private bool _isQuitting;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeStateMachine();
        }

        private void Start()
        {
            // Begin the init state automatically.
            // The Init state itself will transition to Gameplay when ready.
            if (_stateMachine != null && !_stateMachine.IsInitialized)
            {
                _stateMachine.Initialize(GameStateId.Init);
            }
        }

        private void Update()
        {
            _stateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.Shutdown();
                _stateMachine = null;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !_isQuitting && CurrentState == GameStateId.Gameplay)
            {
                // Auto-pause when app loses focus during gameplay
                PauseGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Handled on mobile; same logic as focus loss
            if (pauseStatus && !_isQuitting && CurrentState == GameStateId.Gameplay)
            {
                PauseGame();
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            OnApplicationQuitting?.Invoke();
        }

        // ------------------------------------------------------------------
        // State machine setup
        // ------------------------------------------------------------------

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<GameStateId>();

            _stateMachine.RegisterState(GameStateId.Init, new InitState(this));
            _stateMachine.RegisterState(GameStateId.Gameplay, new GameplayState(this));
            _stateMachine.RegisterState(GameStateId.Pause, new PauseState(this));
            _stateMachine.RegisterState(GameStateId.GameOver, new GameOverState(this));

            _stateMachine.OnStateChanged += (from, to) =>
            {
                Debug.Log($"[GameManager] State changed: {from} → {to}");
                OnGameStateChanged?.Invoke(from, to);
            };
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Transitions the game from Init → Gameplay.
        /// Call this once your initialization routine (config loading, ads init, etc.) is complete.
        /// </summary>
        public void CompleteInitialization()
        {
            if (CurrentState != GameStateId.Init)
            {
                Debug.LogWarning("[GameManager] CompleteInitialization() called outside of Init state. Ignoring.");
                return;
            }

            _stateMachine?.TransitionTo(GameStateId.Gameplay);
            OnGameInitialized?.Invoke();
            OnGameplayStarted?.Invoke();
        }

        /// <summary>
        /// Pauses the game. Only valid in Gameplay state.
        /// Sets Time.timeScale to 0.
        /// </summary>
        public void PauseGame()
        {
            if (CurrentState != GameStateId.Gameplay)
                return;

            _stateMachine?.TransitionTo(GameStateId.Pause);
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
        }

        /// <summary>
        /// Resumes the game from pause. Only valid in Pause state.
        /// Restores Time.timeScale to 1.
        /// </summary>
        public void ResumeGame()
        {
            if (CurrentState != GameStateId.Pause)
                return;

            _stateMachine?.TransitionTo(GameStateId.Gameplay);
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }

        /// <summary>
        /// Ends the current game. Only valid in Gameplay or Pause state.
        /// </summary>
        public void EndGame()
        {
            if (CurrentState != GameStateId.Gameplay && CurrentState != GameStateId.Pause)
                return;

            Time.timeScale = 1f; // Ensure time is normalised
            _stateMachine?.TransitionTo(GameStateId.GameOver);
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// Restarts the game by transitioning back to Gameplay from GameOver.
        /// Optionally reloads the gameplay scene.
        /// </summary>
        /// <param name="reloadScene">If true, reloads the gameplay scene before entering Gameplay.</param>
        public void RestartGame(bool reloadScene = true)
        {
            if (CurrentState != GameStateId.GameOver)
            {
                Debug.LogWarning("[GameManager] RestartGame() called outside of GameOver state. Ignoring.");
                return;
            }

            if (reloadScene && !string.IsNullOrEmpty(_gameplaySceneName))
            {
                SceneLoader.Instance.LoadScene(_gameplaySceneName, onComplete: () =>
                {
                    _stateMachine?.TransitionTo(GameStateId.Gameplay, forceTransition: true);
                    OnGameplayStarted?.Invoke();
                });
            }
            else
            {
                _stateMachine?.TransitionTo(GameStateId.Gameplay, forceTransition: true);
                OnGameplayStarted?.Invoke();
            }
        }

        /// <summary>
        /// Returns to the main menu (boot scene) from any state.
        /// </summary>
        public void ReturnToMenu()
        {
            Time.timeScale = 1f;

            if (!string.IsNullOrEmpty(_menuSceneName))
            {
                SceneLoader.Instance.LoadScene(_menuSceneName, onComplete: () =>
                {
                    _stateMachine?.TransitionTo(GameStateId.Init, forceTransition: true);
                });
            }
        }

        /// <summary>
        /// Quits the application. Fires <see cref="OnApplicationQuitting"/> beforehand.
        /// </summary>
        public void QuitGame()
        {
            OnApplicationQuitting?.Invoke();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // State implementations
        // ------------------------------------------------------------------

        private sealed class InitState : StateMachine<GameStateId>.IState
        {
            private readonly GameManager _gm;
            public InitState(GameManager gm) => _gm = gm;

            public void Enter()
            {
                Debug.Log("[GameManager] Entering Init state. Perform pre-game initialization here.");
            }

            public void Update() { }

            public void Exit()
            {
                Debug.Log("[GameManager] Exiting Init state.");
            }
        }

        private sealed class GameplayState : StateMachine<GameStateId>.IState
        {
            private readonly GameManager _gm;
            public GameplayState(GameManager gm) => _gm = gm;

            public void Enter()
            {
                Debug.Log("[GameManager] Entering Gameplay state.");
            }

            public void Update() { }

            public void Exit()
            {
                Debug.Log("[GameManager] Exiting Gameplay state.");
            }
        }

        private sealed class PauseState : StateMachine<GameStateId>.IState
        {
            private readonly GameManager _gm;
            public PauseState(GameManager gm) => _gm = gm;

            public void Enter()
            {
                Debug.Log("[GameManager] Entering Pause state.");
            }

            public void Update() { }

            public void Exit()
            {
                Debug.Log("[GameManager] Exiting Pause state.");
            }
        }

        private sealed class GameOverState : StateMachine<GameStateId>.IState
        {
            private readonly GameManager _gm;
            public GameOverState(GameManager gm) => _gm = gm;

            public void Enter()
            {
                Debug.Log("[GameManager] Entering GameOver state.");
            }

            public void Update() { }

            public void Exit()
            {
                Debug.Log("[GameManager] Exiting GameOver state.");
            }
        }
    }
}
