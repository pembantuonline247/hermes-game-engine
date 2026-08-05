using UnityEngine;
using Hermes.GameEngine.Monetization;

namespace Hermes.SpaceDodger.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Menu, Playing, GameOver }
        public GameState State { get; private set; } = GameState.Menu;

        [SerializeField] private float speedIncreaseRate = 0.1f;
        [SerializeField] private float maxSpeedMultiplier = 5f;

        public float CurrentSpeedMultiplier { get; private set; } = 1f;
        public float SurvivalTime { get; private set; } = 0f;

        public event System.Action<GameState> OnStateChanged;
        public event System.Action<int> OnScoreChanged;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            SetState(GameState.Menu);
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                SurvivalTime += Time.deltaTime;
                CurrentSpeedMultiplier = Mathf.Min(1f + SurvivalTime * speedIncreaseRate, maxSpeedMultiplier);
                OnScoreChanged?.Invoke(Mathf.FloorToInt(SurvivalTime * 10f));
            }
        }

        public void StartGame()
        {
            SurvivalTime = 0f;
            CurrentSpeedMultiplier = 1f;
            SetState(GameState.Playing);
        }

        public void GameOver()
        {
            SetState(GameState.GameOver);
            // Try to show interstitial ad
            if (AdManager.Instance != null && AdManager.Instance.IsInitialized)
            {
                if (AdManager.Instance.IsInterstitialReady)
                    AdManager.Instance.ShowInterstitial("gameover");
            }
            else if (AdMobManager.Instance != null && AdMobManager.Instance.IsInitialized)
            {
                if (AdMobManager.Instance.IsInterstitialReady)
                    AdMobManager.Instance.ShowInterstitial();
            }
        }

        public void RestartGame()
        {
            StartGame();
        }

        private void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}