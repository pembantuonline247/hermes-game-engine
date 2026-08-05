using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Analytics
{
    /// <summary>
    /// Simple analytics manager. Tracks game events and logs them.
    /// In production, POSTs to ANALYTICS_URL (defaults to games.pembantu.online/api/analytics).
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        private static AnalyticsManager _instance;
        public static AnalyticsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AnalyticsManager]");
                    _instance = go.AddComponent<AnalyticsManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private string _apiUrl;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _apiUrl = Environment.GetEnvironmentVariable("ANALYTICS_URL") ?? "https://games.pembantu.online/api/analytics";
        }

        public void TrackEvent(string eventType, string game = "space-dodger", float value = 0f, string metadata = "")
        {
            Debug.Log($"[Analytics] {eventType} | game={game} | value={value} | meta={metadata}");
            // In production: POST JSON to _apiUrl
        }

        public void TrackGameStart(string game = "space-dodger") => TrackEvent("game_start", game);
        public void TrackGameOver(string game, int score, float survivalTime) => TrackEvent("game_over", game, score, $"time:{survivalTime:F1}");
        public void TrackAdImpression(string adType, string game = "space-dodger") => TrackEvent("ad_impression", game, metadata: $"type:{adType}");
        public void TrackPurchase(string productId, float price, string game = "space-dodger") => TrackEvent("purchase", game, price, $"product:{productId}");
    }
}