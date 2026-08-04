using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Hermes.GameEngine.Analytics
{
    /// <summary>
    /// Central analytics event tracking manager.
    /// Supports custom events, revenue tracking, and REST API posting to a configurable endpoint.
    ///
    /// Events are queued and flushed periodically or when the queue reaches a threshold.
    /// Configure the endpoint via environment variable "ANALYTICS_ENDPOINT" or the inspector field.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static AnalyticsManager _instance;

        /// <summary>
        /// Gets the singleton instance of AnalyticsManager.
        /// </summary>
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

        // ------------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------------

        [Header("Analytics Endpoint")]
        [Tooltip("REST API endpoint for analytics events. Falls back to env var 'ANALYTICS_ENDPOINT' if set.")]
        [SerializeField] private string _apiEndpoint = "https://analytics.hermes-game-engine.local/events";

        [Header("Batching")]
        [Tooltip("Maximum number of events to keep in the queue before force-flushing.")]
        [SerializeField] private int _maxQueueSize = 50;

        [Tooltip("How often (in seconds) the queue is flushed automatically.")]
        [SerializeField] private float _flushIntervalSeconds = 30f;

        [Tooltip("If true, events are also logged to the Unity Console for debugging.")]
        [SerializeField] private bool _verboseLogging = true;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        /// <summary>
        /// Whether the manager is initialized and accepting events.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// The current number of queued events waiting to be flushed.
        /// </summary>
        public int QueueSize => _eventQueue.Count;

        /// <summary>
        /// Total number of events sent since this manager was created.
        /// </summary>
        public int TotalEventsSent { get; private set; }

        /// <summary>
        /// Total number of events that failed to send.
        /// </summary>
        public int TotalEventsFailed { get; private set; }

        private readonly Queue<AnalyticsEvent> _eventQueue = new Queue<AnalyticsEvent>();
        private Coroutine _flushCoroutine;

        // ------------------------------------------------------------------
        // Internal event model
        // ------------------------------------------------------------------

        [Serializable]
        private sealed class AnalyticsEvent
        {
            public string event_name;
            public string event_id;
            public long timestamp_utc;
            public string session_id;
            public Dictionary<string, object> parameters;

            public AnalyticsEvent(string name, Dictionary<string, object> parameters = null)
            {
                event_name = name;
                event_id = Guid.NewGuid().ToString("N");
                timestamp_utc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                session_id = AnalyticsManager._sessionId;
                this.parameters = parameters ?? new Dictionary<string, object>();
            }
        }

        [Serializable]
        private sealed class RevenueEvent
        {
            public string event_name = "revenue";
            public string event_id;
            public long timestamp_utc;
            public string session_id;
            public double revenue;
            public string network;

            public RevenueEvent(double revenue, string network)
            {
                event_id = Guid.NewGuid().ToString("N");
                timestamp_utc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                session_id = AnalyticsManager._sessionId;
                this.revenue = revenue;
                this.network = string.IsNullOrEmpty(network) ? "unknown" : network;
            }
        }

        [Serializable]
        private sealed class BatchPayload
        {
            public List<object> events;
        }

        private static string _sessionId;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AnalyticsManager] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Generate a unique session ID
            _sessionId = Guid.NewGuid().ToString("N");

            // Resolve endpoint from environment variable if available
            string envEndpoint = Environment.GetEnvironmentVariable("ANALYTICS_ENDPOINT");
            if (!string.IsNullOrEmpty(envEndpoint))
            {
                _apiEndpoint = envEndpoint;
                Debug.Log($"[AnalyticsManager] Endpoint overridden by env var: {_apiEndpoint}");
            }
        }

        private void Start()
        {
            IsInitialized = true;

            // Start the periodic flush coroutine
            _flushCoroutine = StartCoroutine(PeriodicFlushRoutine());

            LogEvent("analytics_initialized", new Dictionary<string, object>
            {
                { "session_id", _sessionId },
                { "flush_interval", _flushIntervalSeconds },
                { "max_queue", _maxQueueSize }
            });

            Debug.Log($"[AnalyticsManager] Initialized. Session: {_sessionId}, Endpoint: {_apiEndpoint}");
        }

        private void OnDestroy()
        {
            if (_flushCoroutine != null)
            {
                StopCoroutine(_flushCoroutine);
                _flushCoroutine = null;
            }

            // Flush remaining events on shutdown
            if (_eventQueue.Count > 0)
            {
                Debug.Log($"[AnalyticsManager] Flushing {_eventQueue.Count} remaining event(s) on shutdown.");
                Flush();
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            // Ensure any pending events are flushed before the app terminates
            if (_eventQueue.Count > 0)
            {
                Flush();
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Logs a custom analytics event with optional parameters.
        /// </summary>
        /// <param name="name">The event name (e.g., "level_start", "purchase_initiated").</param>
        /// <param name="parameters">
        /// Optional dictionary of key-value pairs to attach to the event.
        /// Values should be primitives (string, int, float, bool) for JSON serialization.
        /// </param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
        public void LogEvent(string name, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("[AnalyticsManager] Event name cannot be null or empty.", nameof(name));

            if (!IsInitialized)
            {
                Debug.LogWarning("[AnalyticsManager] Not initialized. Event discarded.");
                return;
            }

            var analyticsEvent = new AnalyticsEvent(name, parameters);

            lock (_eventQueue)
            {
                _eventQueue.Enqueue(analyticsEvent);
            }

            if (_verboseLogging)
            {
                Debug.Log($"[AnalyticsManager] Event queued: '{name}' (queue: {_eventQueue.Count})");
            }

            // Flush immediately if queue is full
            if (_eventQueue.Count >= _maxQueueSize)
            {
                Flush();
            }
        }

        /// <summary>
        /// Logs a revenue event (ad revenue, IAP revenue, etc.).
        /// </summary>
        /// <param name="revenue">Monetary amount earned.</param>
        /// <param name="network">The ad network or source (e.g., "AppLovin", "UnityAds", "iap").</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="revenue"/> is negative.</exception>
        public void LogRevenue(double revenue, string network)
        {
            if (revenue < 0)
                throw new ArgumentOutOfRangeException(nameof(revenue), "[AnalyticsManager] Revenue cannot be negative.");

            if (!IsInitialized)
            {
                Debug.LogWarning("[AnalyticsManager] Not initialized. Revenue event discarded.");
                return;
            }

            var revEvent = new RevenueEvent(revenue, network);

            lock (_eventQueue)
            {
                _eventQueue.Enqueue(revEvent);
            }

            if (_verboseLogging)
            {
                Debug.Log($"[AnalyticsManager] Revenue event queued: ${revenue:F2} from '{network}' (queue: {_eventQueue.Count})");
            }

            if (_eventQueue.Count >= _maxQueueSize)
            {
                Flush();
            }
        }

        /// <summary>
        /// Forces an immediate flush of all queued events to the analytics endpoint.
        /// </summary>
        public void Flush()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AnalyticsManager] Cannot flush: not initialized.");
                return;
            }

            List<object> batch;

            lock (_eventQueue)
            {
                if (_eventQueue.Count == 0)
                    return;

                batch = new List<object>(_eventQueue.Count);
                while (_eventQueue.Count > 0)
                {
                    batch.Add(_eventQueue.Dequeue());
                }
            }

            if (batch.Count == 0)
                return;

            if (string.IsNullOrEmpty(_apiEndpoint))
            {
                Debug.LogWarning("[AnalyticsManager] No API endpoint configured. Events discarded.");
                return;
            }

#if UNITY_EDITOR
            // In the editor, just log the batch instead of making HTTP requests.
            Debug.Log($"[AnalyticsManager] [EDITOR] Would send {batch.Count} event(s) to {_apiEndpoint}.");
            if (_verboseLogging)
            {
                foreach (var evt in batch)
                {
                    string json = JsonUtility.ToJson(evt);
                    Debug.Log($"[AnalyticsManager] [EDITOR] Event: {json}");
                }
            }
            TotalEventsSent += batch.Count;
#else
            // Send asynchronously via UnityWebRequest
            StartCoroutine(SendBatchRoutine(batch));
#endif
        }

        /// <summary>
        /// Sets the analytics API endpoint at runtime.
        /// </summary>
        /// <param name="endpoint">The full URL of the analytics ingestion endpoint.</param>
        public void SetEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                Debug.LogError("[AnalyticsManager] Cannot set endpoint to null or empty.");
                return;
            }

            _apiEndpoint = endpoint;
            Debug.Log($"[AnalyticsManager] Endpoint set to: {_apiEndpoint}");
        }

        /// <summary>
        /// Gets the current session identifier.
        /// </summary>
        public string GetSessionId() => _sessionId;

        // ------------------------------------------------------------------
        // Internal
        // ------------------------------------------------------------------

        private IEnumerator PeriodicFlushRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_flushIntervalSeconds);

                if (_eventQueue.Count > 0)
                {
                    Flush();
                }
            }
        }

        private IEnumerator SendBatchRoutine(List<object> batch)
        {
            var payload = new BatchPayload { events = batch };
            string jsonPayload = JsonUtility.ToJson(payload);

            if (_verboseLogging)
            {
                Debug.Log($"[AnalyticsManager] Sending batch of {batch.Count} event(s) to {_apiEndpoint}.");
            }

            using (var request = new UnityWebRequest(_apiEndpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Session-Id", _sessionId);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    TotalEventsSent += batch.Count;
                    Debug.Log($"[AnalyticsManager] Batch sent successfully ({batch.Count} events). HTTP {request.responseCode}");
                }
                else
                {
                    TotalEventsFailed += batch.Count;
                    Debug.LogError($"[AnalyticsManager] Failed to send batch ({batch.Count} events): {request.error} " +
                                   $"(HTTP {request.responseCode})");

                    // Re-queue events on failure (optional resilience)
                    // In production, you may want a maximum retry count.
                    lock (_eventQueue)
                    {
                        foreach (var evt in batch)
                        {
                            _eventQueue.Enqueue(evt);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Convenience methods
        // ------------------------------------------------------------------

        /// <summary>
        /// Logs a simple event with no parameters.
        /// </summary>
        /// <param name="name">Event name.</param>
        public static void Track(string name)
        {
            if (Instance.IsInitialized)
                Instance.LogEvent(name);
        }

        /// <summary>
        /// Tracks a level start event with the level number.
        /// </summary>
        /// <param name="levelNumber">The level being started.</param>
        /// <param name="difficulty">Optional difficulty label.</param>
        public void TrackLevelStart(int levelNumber, string difficulty = "normal")
        {
            LogEvent("level_start", new Dictionary<string, object>
            {
                { "level", levelNumber },
                { "difficulty", difficulty }
            });
        }

        /// <summary>
        /// Tracks a level complete event.
        /// </summary>
        /// <param name="levelNumber">The level that was completed.</param>
        /// <param name="score">Optional score achieved.</param>
        /// <param name="timeSeconds">Optional time taken in seconds.</param>
        public void TrackLevelComplete(int levelNumber, int score = 0, float timeSeconds = 0f)
        {
            LogEvent("level_complete", new Dictionary<string, object>
            {
                { "level", levelNumber },
                { "score", score },
                { "time_seconds", timeSeconds }
            });
        }
    }
}
