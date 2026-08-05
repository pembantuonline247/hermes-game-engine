using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Hermes.SpaceDodger.Core;

namespace Hermes.GameEngine.Online
{
    /// <summary>
    /// Handles all online features for Space Dodger:
    /// - JWT guest login (auto, no sign-up required)
    /// - Leaderboard upload on game over
    /// - Leaderboard fetch for display
    /// - Cloud high score sync
    ///
    /// Uses the game portal API at games.pembantu.online/api/
    /// </summary>
    public class OnlineManager : MonoBehaviour
    {
        private static OnlineManager _instance;
        public static OnlineManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[OnlineManager]");
                    _instance = go.AddComponent<OnlineManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("API Settings")]
        [SerializeField] private string apiBaseUrl = "https://games.pembantu.online/api";
        [SerializeField] private string gameName = "space-dodger";

        // Auth state
        private string _jwtToken;
        private string _username;
        private bool _isOnline;
        private bool _isConnecting;

        // Leaderboard cache
        public List<LeaderboardEntry> LeaderboardEntries { get; private set; } = new List<LeaderboardEntry>();
        public int MyRank { get; private set; } = -1;
        public int MyBestScore { get; private set; } = 0;
        public bool IsOnline => _isOnline;
        public bool IsConnecting => _isConnecting;

        public event Action OnLoginComplete;
        public event Action<List<LeaderboardEntry>> OnLeaderboardFetched;
        public event Action<int> OnScoreUploaded;

        [Serializable]
        public class LeaderboardEntry
        {
            public string username;
            public string display_name;
            public int score;
            public string achieved_at;
        }

        [Serializable]
        private class LoginResponse
        {
            public string token;
            public UserData user;
        }

        [Serializable]
        private class UserData
        {
            public int id;
            public string username;
            public string display_name;
            public int coins;
        }

        [Serializable]
        private class LeaderboardResponse
        {
            public string game;
            public List<LeaderboardEntry> entries;
        }

        [Serializable]
        private class ScoreSubmitResponse
        {
            public bool submitted;
            public int score;
            public bool is_new_best;
        }

        [Serializable]
        private class MyScoreResponse
        {
            public int best_score;
            public int attempts;
            public int rank;
        }

        [Serializable]
        private class ErrorResponse
        {
            public string error;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Read API URL from environment variable if set
            string envUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (!string.IsNullOrEmpty(envUrl))
                apiBaseUrl = envUrl;
        }

        private void Start()
        {
            // Listen for game over events to auto-upload scores
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;

            // Auto login
            StartCoroutine(AutoLogin());
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            if (state == GameManager.GameState.GameOver && _isOnline)
            {
                int score = Mathf.FloorToInt(GameManager.Instance.SurvivalTime * 10f);
                StartCoroutine(UploadScore(score));
            }
        }

        /// <summary>
        /// Auto-login as guest using a device-based username.
        /// Creates account if it doesn't exist, otherwise logs in.
        /// </summary>
        private IEnumerator AutoLogin()
        {
            _isConnecting = true;

            // Generate consistent guest username from device ID
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            _username = "player_" + deviceId.Substring(0, 8);
            string password = "guest_" + deviceId;

            // Try login first
            yield return TryLogin(_username, password);

            // If login failed, try register then login again
            if (!_isOnline)
            {
                yield return TryRegister(_username, password, $"Player {deviceId.Substring(0, 4)}");
                if (!_isOnline)
                    yield return TryLogin(_username, password);
            }

            _isConnecting = false;
            OnLoginComplete?.Invoke();

            if (_isOnline)
            {
                Debug.Log($"[OnlineManager] Online as '{_username}'");
                // Fetch my best score
                StartCoroutine(FetchMyScore());
            }
            else
            {
                Debug.LogWarning("[OnlineManager] Offline mode — server unavailable");
            }
        }

        private IEnumerator TryLogin(string username, string password)
        {
            var form = new WWWForm();
            form.AddField("username", username);
            form.AddField("password", password);

            using var req = UnityWebRequest.Post($"{apiBaseUrl}/auth/login", form);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    if (resp != null && !string.IsNullOrEmpty(resp.token))
                    {
                        _jwtToken = resp.token;
                        _isOnline = true;
                    }
                }
                catch { }
            }
        }

        private IEnumerator TryRegister(string username, string password, string displayName)
        {
            var form = new WWWForm();
            form.AddField("username", username);
            form.AddField("password", password);
            form.AddField("display_name", displayName);

            using var req = UnityWebRequest.Post($"{apiBaseUrl}/auth/register", form);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    if (resp != null && !string.IsNullOrEmpty(resp.token))
                    {
                        _jwtToken = resp.token;
                        _isOnline = true;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Uploads the player's score to the leaderboard.
        /// Called automatically on game over.
        /// </summary>
        public IEnumerator UploadScore(int score)
        {
            if (!_isOnline || string.IsNullOrEmpty(_jwtToken))
                yield break;

            var form = new WWWForm();
            form.AddField("score", score);
            form.AddField("metadata", "{\"platform\":\"" + Application.platform + "\"}");

            using var req = UnityWebRequest.Post($"{apiBaseUrl}/leaderboard/{gameName}", form);
            req.SetRequestHeader("Authorization", $"Bearer {_jwtToken}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<ScoreSubmitResponse>(req.downloadHandler.text);
                    if (resp != null && resp.is_new_best)
                    {
                        Debug.Log($"[OnlineManager] New personal best: {score}!");
                        OnScoreUploaded?.Invoke(score);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Fetch the leaderboard. Results via OnLeaderboardFetched event.
        /// </summary>
        public void FetchLeaderboard(int limit = 50)
        {
            StartCoroutine(FetchLeaderboardCoroutine(limit));
        }

        private IEnumerator FetchLeaderboardCoroutine(int limit)
        {
            using var req = UnityWebRequest.Get($"{apiBaseUrl}/leaderboard/{gameName}?limit={limit}");
            // No auth needed to view leaderboard
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                    if (resp != null)
                    {
                        LeaderboardEntries = resp.entries ?? new List<LeaderboardEntry>();
                        OnLeaderboardFetched?.Invoke(LeaderboardEntries);
                    }
                }
                catch
                {
                    Debug.LogWarning("[OnlineManager] Failed to parse leaderboard");
                }
            }
        }

        /// <summary>
        /// Fetch my personal best score and rank.
        /// </summary>
        public IEnumerator FetchMyScore()
        {
            if (!_isOnline || string.IsNullOrEmpty(_jwtToken))
                yield break;

            using var req = UnityWebRequest.Get($"{apiBaseUrl}/leaderboard/{gameName}/me");
            req.SetRequestHeader("Authorization", $"Bearer {_jwtToken}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<MyScoreResponse>(req.downloadHandler.text);
                    if (resp != null)
                    {
                        MyBestScore = resp.best_score;
                        MyRank = resp.rank;
                    }
                }
                catch { }
            }
        }
    }
}