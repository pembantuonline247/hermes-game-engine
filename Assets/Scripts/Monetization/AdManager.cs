using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    /// <summary>
    /// AppLovin MAX mediation wrapper.
    /// Provides a clean C# API for rewarded videos, interstitials, and banners.
    /// Ad Unit IDs are read from environment variables or EditorPrefs at runtime, not hardcoded.
    ///
    /// Environment variable naming convention:
    ///   - MAX_REWARDED_ID
    ///   - MAX_INTERSTITIAL_ID
    ///   - MAX_BANNER_ID
    ///
    /// In the Unity Editor, falls back to EditorPrefs keys: "MaxRewardedId", "MaxInterstitialId", "MaxBannerId".
    /// If neither is set, logs a warning and uses placeholder IDs.
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static AdManager _instance;

        /// <summary>
        /// Gets the singleton instance of AdManager.
        /// </summary>
        public static AdManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AdManager]");
                    _instance = go.AddComponent<AdManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when MAX SDK finishes initialization. Parameter: true if successful.</summary>
        public event Action<bool> OnSdkInitialized;

        /// <summary>Fired when a rewarded video is loaded.</summary>
        public event Action OnRewardedVideoLoaded;

        /// <summary>Fired when a rewarded video fails to load. Parameter: error message.</summary>
        public event Action<string> OnRewardedVideoFailed;

        /// <summary>Fired when a rewarded video is shown.</summary>
        public event Action OnRewardedVideoShown;

        /// <summary>Fired when a rewarded video is closed (without completing).</summary>
        public event Action OnRewardedVideoClosed;

        /// <summary>Fired when the user earns a reward from a rewarded video.</summary>
        public event Action OnRewardEarned;

        /// <summary>Fired when an interstitial ad is loaded.</summary>
        public event Action OnInterstitialLoaded;

        /// <summary>Fired when an interstitial ad fails to load. Parameter: error message.</summary>
        public event Action<string> OnInterstitialFailed;

        /// <summary>Fired when an interstitial ad is shown.</summary>
        public event Action OnInterstitialShown;

        /// <summary>Fired when an interstitial ad is closed.</summary>
        public event Action OnInterstitialClosed;

        /// <summary>Fired when a banner ad is loaded and shown.</summary>
        public event Action OnBannerShown;

        /// <summary>Fired when a banner ad fails to load. Parameter: error message.</summary>
        public event Action<string> OnBannerFailed;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        /// <summary>
        /// Whether the MAX SDK has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Whether a rewarded video ad is currently loaded and ready to show.
        /// </summary>
        public bool IsRewardedVideoReady { get; private set; }

        /// <summary>
        /// Whether an interstitial ad is currently loaded and ready to show.
        /// </summary>
        public bool IsInterstitialReady { get; private set; }

        /// <summary>
        /// Whether banner ads are currently visible.
        /// </summary>
        public bool IsBannerVisible { get; private set; }

        // ------------------------------------------------------------------
        // Ad Unit IDs (resolved from environment variables / EditorPrefs)
        // ------------------------------------------------------------------

        private string _rewardedVideoAdUnitId;
        private string _interstitialAdUnitId;
        private string _bannerAdUnitId;

        // ------------------------------------------------------------------
        // MAX SDK wrapper (compile-time guard for Unity Editor testing)
        // ------------------------------------------------------------------

        private const string PLACEHOLDER_REWARDED = "REWARDED_VIDEO_PLACEHOLDER";
        private const string PLACEHOLDER_INTERSTITIAL = "INTERSTITIAL_PLACEHOLDER";
        private const string PLACEHOLDER_BANNER = "BANNER_PLACEHOLDER";

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AdManager] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveAdUnitIds();
        }

        private void Start()
        {
            InitializeMaxSdk();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ------------------------------------------------------------------
        // Ad Unit ID resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves Ad Unit IDs from environment variables, falling back to EditorPrefs (Editor only),
        /// then to placeholder strings.
        /// </summary>
        private void ResolveAdUnitIds()
        {
            _rewardedVideoAdUnitId = ResolveId("MAX_REWARDED_ID", "MaxRewardedId", PLACEHOLDER_REWARDED);
            _interstitialAdUnitId = ResolveId("MAX_INTERSTITIAL_ID", "MaxInterstitialId", PLACEHOLDER_INTERSTITIAL);
            _bannerAdUnitId = ResolveId("MAX_BANNER_ID", "MaxBannerId", PLACEHOLDER_BANNER);

            Debug.Log($"[AdManager] Resolved Ad Unit IDs — Rewarded: {MaskId(_rewardedVideoAdUnitId)}, " +
                      $"Interstitial: {MaskId(_interstitialAdUnitId)}, Banner: {MaskId(_bannerAdUnitId)}");
        }

        /// <summary>
        /// Tries to resolve an Ad Unit ID from an environment variable first, then EditorPrefs (in editor),
        /// and finally uses the provided fallback.
        /// </summary>
        private static string ResolveId(string envVar, string editorPrefsKey, string fallback)
        {
            // 1. Environment variable
            string envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue))
            {
                Debug.Log($"[AdManager] Resolved '{envVar}' from environment variable.");
                return envValue;
            }

            // 2. EditorPrefs (only in Unity Editor)
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(editorPrefsKey))
            {
                string prefValue = UnityEditor.EditorPrefs.GetString(editorPrefsKey);
                if (!string.IsNullOrEmpty(prefValue))
                {
                    Debug.Log($"[AdManager] Resolved '{editorPrefsKey}' from EditorPrefs.");
                    return prefValue;
                }
            }
#endif

            // 3. Fallback
            Debug.LogWarning($"[AdManager] No value found for '{envVar}' or '{editorPrefsKey}'. Using placeholder. " +
                             "Set the environment variable or EditorPrefs to supply real Ad Unit IDs.");
            return fallback;
        }

        /// <summary>
        /// Masks an ID for logging (shows first 4 and last 4 characters).
        /// </summary>
        private static string MaskId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 8)
                return id ?? "NULL";

            return id.Substring(0, 4) + "****" + id.Substring(id.Length - 4);
        }

        // ------------------------------------------------------------------
        // MAX SDK initialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes the AppLovin MAX SDK.
        /// In the Unity Editor, this is a no-op that logs the configuration.
        /// On device, calls MaxSdk.Initialize() with the resolved Ad Unit IDs.
        /// </summary>
        public void InitializeMaxSdk()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[AdManager] MAX SDK already initialized.");
                return;
            }

            Debug.Log("[AdManager] Initializing AppLovin MAX SDK...");
            Debug.Log($"[AdManager]   Rewarded Ad Unit ID: {MaskId(_rewardedVideoAdUnitId)}");
            Debug.Log($"[AdManager]   Interstitial Ad Unit ID: {MaskId(_interstitialAdUnitId)}");
            Debug.Log($"[AdManager]   Banner Ad Unit ID: {MaskId(_bannerAdUnitId)}");

#if UNITY_EDITOR
            // In the Editor, simulate success after a short delay.
            Debug.Log("[AdManager] Editor mode: simulating MAX SDK initialization.");
            StartCoroutine(SimulateEditorInit());
#else
            // Real MAX SDK initialization — requires AppLovin MAX Unity package.
            // MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxSdkInitialized;
            // MaxSdk.SetSdkKey(ResolveId("MAX_SDK_KEY", "MaxSdkKey", "SDK_KEY_PLACEHOLDER"));
            // MaxSdk.InitializeSdk();

            // Simulate for template completeness:
            StartCoroutine(SimulateEditorInit());
#endif
        }

        private System.Collections.IEnumerator SimulateEditorInit()
        {
            yield return new WaitForSeconds(0.5f);
            IsInitialized = true;
            OnSdkInitialized?.Invoke(true);
            Debug.Log("[AdManager] MAX SDK initialization complete (simulated).");
        }

        /// <summary>
        /// Callback fired when the real MAX SDK finishes initialization.
        /// </summary>
        private void OnMaxSdkInitialized(EventArgs args)
        {
            IsInitialized = true;
            Debug.Log("[AdManager] MAX SDK initialized successfully.");
            OnSdkInitialized?.Invoke(true);

            // Load initial ads
            LoadRewardedVideo();
            LoadInterstitial();
        }

        // ------------------------------------------------------------------
        // Rewarded Video
        // ------------------------------------------------------------------

        /// <summary>
        /// Loads a rewarded video ad.
        /// </summary>
        public void LoadRewardedVideo()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdManager] Cannot load rewarded video: SDK not initialized.");
                return;
            }

            Debug.Log("[AdManager] Loading rewarded video...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating rewarded video load.");
            IsRewardedVideoReady = true;
            OnRewardedVideoLoaded?.Invoke();
#else
            // MaxSdkCallbacks.Rewarded.OnRewardedAdLoadedEvent += OnRewardedLoaded;
            // MaxSdkCallbacks.Rewarded.OnRewardedAdLoadFailedEvent += OnRewardedLoadFailed;
            // MaxSdkCallbacks.Rewarded.OnRewardedAdDisplayedEvent += OnRewardedDisplayed;
            // MaxSdkCallbacks.Rewarded.OnRewardedAdHiddenEvent += OnRewardedHidden;
            // MaxSdkCallbacks.Rewarded.OnRewardedAdReceivedRewardEvent += OnRewardReceived;
            // MaxSdk.LoadRewardedAd(_rewardedVideoAdUnitId);
#endif
        }

        /// <summary>
        /// Shows a loaded rewarded video ad.
        /// </summary>
        /// <param name="placement">Optional ad placement identifier for reporting.</param>
        /// <returns>True if the ad was requested to be shown; false if no ad is ready.</returns>
        public bool ShowRewardedVideo(string placement = "default")
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdManager] Cannot show rewarded video: SDK not initialized.");
                return false;
            }

            if (!IsRewardedVideoReady)
            {
                Debug.LogWarning("[AdManager] Cannot show rewarded video: no ad loaded.");
                return false;
            }

            Debug.Log($"[AdManager] Showing rewarded video (placement: '{placement}')...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating rewarded video display.");
            IsRewardedVideoReady = false;
            OnRewardedVideoShown?.Invoke();

            // In editor, simulate reward after a short delay
            StartCoroutine(SimulateEditorReward());
#else
            // MaxSdk.ShowRewardedAd(_rewardedVideoAdUnitId, placement);
#endif
            return true;
        }

        private System.Collections.IEnumerator SimulateEditorReward()
        {
            yield return new WaitForSeconds(1.5f);
            OnRewardEarned?.Invoke();
            OnRewardedVideoClosed?.Invoke();
        }

        // ------------------------------------------------------------------
        // Interstitial
        // ------------------------------------------------------------------

        /// <summary>
        /// Loads an interstitial ad.
        /// </summary>
        public void LoadInterstitial()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdManager] Cannot load interstitial: SDK not initialized.");
                return;
            }

            Debug.Log("[AdManager] Loading interstitial ad...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating interstitial load.");
            IsInterstitialReady = true;
            OnInterstitialLoaded?.Invoke();
#else
            // MaxSdkCallbacks.Interstitial.OnInterstitialAdLoadedEvent += OnInterstitialLoaded;
            // MaxSdkCallbacks.Interstitial.OnInterstitialAdLoadFailedEvent += OnInterstitialLoadFailed;
            // MaxSdkCallbacks.Interstitial.OnInterstitialAdDisplayedEvent += OnInterstitialDisplayed;
            // MaxSdkCallbacks.Interstitial.OnInterstitialAdHiddenEvent += OnInterstitialHidden;
            // MaxSdk.LoadInterstitialAd(_interstitialAdUnitId);
#endif
        }

        /// <summary>
        /// Shows a loaded interstitial ad.
        /// </summary>
        /// <param name="placement">Optional ad placement identifier for reporting.</param>
        /// <returns>True if the ad was requested to be shown; false if no ad is ready.</returns>
        public bool ShowInterstitial(string placement = "default")
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdManager] Cannot show interstitial: SDK not initialized.");
                return false;
            }

            if (!IsInterstitialReady)
            {
                Debug.LogWarning("[AdManager] Cannot show interstitial: no ad loaded.");
                return false;
            }

            Debug.Log($"[AdManager] Showing interstitial ad (placement: '{placement}')...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating interstitial display.");
            IsInterstitialReady = false;
            OnInterstitialShown?.Invoke();
            StartCoroutine(SimulateEditorInterstitialClose());
#else
            // MaxSdk.ShowInterstitialAd(_interstitialAdUnitId, placement);
#endif
            return true;
        }

        private System.Collections.IEnumerator SimulateEditorInterstitialClose()
        {
            yield return new WaitForSeconds(1.0f);
            OnInterstitialClosed?.Invoke();
        }

        // ------------------------------------------------------------------
        // Banner
        // ------------------------------------------------------------------

        /// <summary>
        /// Shows a banner ad at the specified position.
        /// </summary>
        /// <param name="position">Screen position. Defaults to BottomCenter.</param>
        public void ShowBanner(string position = "BottomCenter")
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdManager] Cannot show banner: SDK not initialized.");
                return;
            }

            if (IsBannerVisible)
            {
                Debug.Log("[AdManager] Banner already visible.");
                return;
            }

            Debug.Log($"[AdManager] Showing banner ad at '{position}'...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating banner display.");
            IsBannerVisible = true;
            OnBannerShown?.Invoke();
#else
            // MaxSdkCallbacks.Banner.OnBannerAdLoadedEvent += OnBannerLoaded;
            // MaxSdkCallbacks.Banner.OnBannerAdLoadFailedEvent += OnBannerLoadFailed;
            // MaxSdk.CreateBanner(_bannerAdUnitId, BannerPosition.BottomCenter);
            // MaxSdk.ShowBanner(_bannerAdUnitId);
#endif
        }

        /// <summary>
        /// Hides the currently visible banner ad.
        /// </summary>
        public void HideBanner()
        {
            if (!IsBannerVisible)
            {
                Debug.Log("[AdManager] Banner already hidden.");
                return;
            }

            Debug.Log("[AdManager] Hiding banner ad...");

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor mode: simulating banner hide.");
            IsBannerVisible = false;
#else
            // MaxSdk.HideBanner(_bannerAdUnitId);
#endif
        }

        /// <summary>
        /// Destroys the banner ad entirely (removes from view hierarchy).
        /// </summary>
        public void DestroyBanner()
        {
            Debug.Log("[AdManager] Destroying banner ad...");

#if UNITY_EDITOR
            IsBannerVisible = false;
#else
            // MaxSdk.DestroyBanner(_bannerAdUnitId);
#endif
        }

        // ------------------------------------------------------------------
        // Placeholder MAX SDK callbacks (for real device integration, wire these)
        // ------------------------------------------------------------------

        /*
        private void OnRewardedLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            IsRewardedVideoReady = true;
            OnRewardedVideoLoaded?.Invoke();
        }

        private void OnRewardedLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            IsRewardedVideoReady = false;
            OnRewardedVideoFailed?.Invoke(errorInfo.Message);
        }

        private void OnRewardedDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardedVideoShown?.Invoke();
        }

        private void OnRewardedHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            IsRewardedVideoReady = false;
            OnRewardedVideoClosed?.Invoke();
            LoadRewardedVideo(); // Preload next
        }

        private void OnRewardReceived(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardEarned?.Invoke();
        }

        private void OnInterstitialLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            IsInterstitialReady = true;
            OnInterstitialLoaded?.Invoke();
        }

        private void OnInterstitialLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            IsInterstitialReady = false;
            OnInterstitialFailed?.Invoke(errorInfo.Message);
        }

        private void OnInterstitialDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnInterstitialShown?.Invoke();
        }

        private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            IsInterstitialReady = false;
            OnInterstitialClosed?.Invoke();
            LoadInterstitial(); // Preload next
        }

        private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            IsBannerVisible = true;
            OnBannerShown?.Invoke();
        }

        private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            OnBannerFailed?.Invoke(errorInfo.Message);
        }
        */
    }
}
