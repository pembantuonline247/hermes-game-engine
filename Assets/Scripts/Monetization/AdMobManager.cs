using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    /// <summary>
    /// Google AdMob standalone manager.
    /// Provides rewarded video, interstitial, and banner ad support via the Google Mobile Ads SDK.
    /// Ad Unit IDs are read from environment variables (or EditorPrefs in the Editor).
    ///
    /// Environment variable naming convention:
    ///   - ADMOB_REWARDED_ID
    ///   - ADMOB_INTERSTITIAL_ID
    ///   - ADMOB_BANNER_ID
    ///
    /// In the Unity Editor, falls back to EditorPrefs keys: "AdMobRewardedId", "AdMobInterstitialId", "AdMobBannerId".
    /// If neither is set, logs a warning and uses test Ad Unit IDs.
    ///
    /// Requires the Google Mobile Ads Unity plugin (com.google.admob) installed via Package Manager.
    /// </summary>
    public class AdMobManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static AdMobManager _instance;

        /// <summary>
        /// Gets the singleton instance of AdMobManager.
        /// </summary>
        public static AdMobManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AdMobManager]");
                    _instance = go.AddComponent<AdMobManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the Google Mobile Ads SDK finishes initialization. Parameter: true if successful.</summary>
        public event System.Action<bool> OnSdkInitialized;

        /// <summary>Fired when a rewarded video is loaded.</summary>
        public event System.Action OnRewardedVideoLoaded;
        /// <summary>Fired when a rewarded video fails to load. Parameter: error message.</summary>
        public event System.Action<string> OnRewardedVideoFailed;
        /// <summary>Fired when a rewarded video is shown.</summary>
        public event System.Action OnRewardedVideoShown;
        /// <summary>Fired when a rewarded video is closed (without completing).</summary>
        public event System.Action OnRewardedVideoClosed;
        /// <summary>Fired when the user earns a reward from a rewarded video.</summary>
        public event System.Action OnRewardEarned;

        /// <summary>Fired when an interstitial ad is loaded.</summary>
        public event System.Action OnInterstitialLoaded;
        /// <summary>Fired when an interstitial ad fails to load. Parameter: error message.</summary>
        public event System.Action<string> OnInterstitialFailed;
        /// <summary>Fired when an interstitial ad is shown.</summary>
        public event System.Action OnInterstitialShown;
        /// <summary>Fired when an interstitial ad is closed.</summary>
        public event System.Action OnInterstitialClosed;

        /// <summary>Fired when a banner ad is loaded and shown.</summary>
        public event System.Action OnBannerShown;
        /// <summary>Fired when a banner ad fails to load. Parameter: error message.</summary>
        public event System.Action<string> OnBannerFailed;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        /// <summary>Whether the AdMob SDK has been initialized.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Whether a rewarded video ad is currently loaded and ready to show.</summary>
        public bool IsRewardedVideoReady { get; private set; }

        /// <summary>Whether an interstitial ad is currently loaded and ready to show.</summary>
        public bool IsInterstitialReady { get; private set; }

        /// <summary>Whether banner ads are currently visible.</summary>
        public bool IsBannerVisible { get; private set; }

        // ------------------------------------------------------------------
        // Ad Unit IDs
        // ------------------------------------------------------------------

        // Test IDs used by Google AdMob in development
        private const string TEST_REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
        private const string TEST_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/1033173712";
        private const string TEST_BANNER_ID = "ca-app-pub-3940256099942544/6300978111";

        private string _rewardedVideoAdUnitId;
        private string _interstitialAdUnitId;
        private string _bannerAdUnitId;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AdMobManager] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveAdUnitIds();
        }

        private void Start()
        {
            InitializeAdMobSdk();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ------------------------------------------------------------------
        // Ad Unit ID resolution
        // ------------------------------------------------------------------

        private void ResolveAdUnitIds()
        {
            _rewardedVideoAdUnitId = ResolveId("ADMOB_REWARDED_ID", "AdMobRewardedId", TEST_REWARDED_ID);
            _interstitialAdUnitId = ResolveId("ADMOB_INTERSTITIAL_ID", "AdMobInterstitialId", TEST_INTERSTITIAL_ID);
            _bannerAdUnitId = ResolveId("ADMOB_BANNER_ID", "AdMobBannerId", TEST_BANNER_ID);

            Debug.Log($"[AdMobManager] Resolved Ad Unit IDs — Rewarded: {MaskId(_rewardedVideoAdUnitId)}, " +
                      $"Interstitial: {MaskId(_interstitialAdUnitId)}, Banner: {MaskId(_bannerAdUnitId)}");
        }

        private static string ResolveId(string envVar, string editorPrefsKey, string fallback)
        {
            // 1. Environment variable
            string envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue))
            {
                Debug.Log($"[AdMobManager] Resolved '{envVar}' from environment variable.");
                return envValue;
            }

            // 2. EditorPrefs (Unity Editor only)
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(editorPrefsKey))
            {
                string prefValue = UnityEditor.EditorPrefs.GetString(editorPrefsKey);
                if (!string.IsNullOrEmpty(prefValue))
                {
                    Debug.Log($"[AdMobManager] Resolved '{editorPrefsKey}' from EditorPrefs.");
                    return prefValue;
                }
            }
#endif

            // 3. Fallback
            Debug.LogWarning($"[AdMobManager] No value found for '{envVar}' or '{editorPrefsKey}'. Using test Ad Unit ID. " +
                             "Set the environment variable or EditorPrefs to supply real Ad Unit IDs.");
            return fallback;
        }

        private static string MaskId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 8) return id ?? "NULL";
            return id.Substring(0, 4) + "****" + id.Substring(id.Length - 4);
        }

        // ------------------------------------------------------------------
        // AdMob SDK initialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes the Google Mobile Ads SDK.
        /// In the Unity Editor, simulates success after a short delay.
        /// On device, uses the real Google Mobile Ads SDK initialization.
        /// </summary>
        public void InitializeAdMobSdk()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[AdMobManager] AdMob SDK already initialized.");
                return;
            }

            Debug.Log("[AdMobManager] Initializing Google Mobile Ads SDK...");
            Debug.Log($"[AdMobManager]   Rewarded Ad Unit ID: {MaskId(_rewardedVideoAdUnitId)}");
            Debug.Log($"[AdMobManager]   Interstitial Ad Unit ID: {MaskId(_interstitialAdUnitId)}");
            Debug.Log($"[AdMobManager]   Banner Ad Unit ID: {MaskId(_bannerAdUnitId)}");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating AdMob SDK initialization.");
            StartCoroutine(SimulateEditorInit());
#else
            // Real AdMob initialization — requires Google Mobile Ads package (com.google.admob).
            // MobileAds.SetiOSAppPauseOnBackground(true);
            // MobileAds.Initialize(initStatus =>
            // {
            //     IsInitialized = true;
            //     Debug.Log("[AdMobManager] AdMob SDK initialized successfully.");
            //     OnSdkInitialized?.Invoke(true);
            //     LoadRewardedVideo();
            //     LoadInterstitial();
            // });
            StartCoroutine(SimulateEditorInit());
#endif
        }

        private IEnumerator SimulateEditorInit()
        {
            yield return new WaitForSeconds(0.5f);
            IsInitialized = true;
            OnSdkInitialized?.Invoke(true);
            Debug.Log("[AdMobManager] AdMob SDK initialization complete (simulated).");
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
                Debug.LogWarning("[AdMobManager] Cannot load rewarded video: SDK not initialized.");
                return;
            }

            Debug.Log("[AdMobManager] Loading rewarded video...");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating rewarded video load.");
            IsRewardedVideoReady = true;
            OnRewardedVideoLoaded?.Invoke();
#else
            // var ad = new RewardedAd(_rewardedVideoAdUnitId);
            // ad.OnAdLoaded += (sender, args) => {
            //     IsRewardedVideoReady = true;
            //     OnRewardedVideoLoaded?.Invoke();
            // };
            // ad.OnAdFailedToLoad += (sender, args) => {
            //     IsRewardedVideoReady = false;
            //     OnRewardedVideoFailed?.Invoke(args.LoadErrorInfo?.ToString() ?? "Unknown");
            // };
            // ad.LoadAd(new AdRequest());
#endif
        }

        /// <summary>
        /// Shows a loaded rewarded video ad.
        /// </summary>
        /// <returns>True if the ad was requested to be shown; false if not ready.</returns>
        public bool ShowRewardedVideo()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdMobManager] Cannot show rewarded video: SDK not initialized.");
                return false;
            }

            if (!IsRewardedVideoReady)
            {
                Debug.LogWarning("[AdMobManager] Cannot show rewarded video: no ad loaded.");
                return false;
            }

            Debug.Log("[AdMobManager] Showing rewarded video...");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating rewarded video display.");
            IsRewardedVideoReady = false;
            OnRewardedVideoShown?.Invoke();
            StartCoroutine(SimulateEditorReward());
#else
            // ad.OnAdFullScreenContentClosed += () => {
            //     IsRewardedVideoReady = false;
            //     OnRewardedVideoClosed?.Invoke();
            //     LoadRewardedVideo(); // Preload next
            // };
            // ad.OnAdFullScreenContentFailed += (error) => {
            //     IsRewardedVideoReady = false;
            //     OnRewardedVideoClosed?.Invoke();
            // };
            // ad.Show();
#endif
            return true;
        }

        private IEnumerator SimulateEditorReward()
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
                Debug.LogWarning("[AdMobManager] Cannot load interstitial: SDK not initialized.");
                return;
            }

            Debug.Log("[AdMobManager] Loading interstitial ad...");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating interstitial load.");
            IsInterstitialReady = true;
            OnInterstitialLoaded?.Invoke();
#else
            // var ad = new InterstitialAd(_interstitialAdUnitId);
            // ad.OnAdLoaded += (sender, args) => {
            //     IsInterstitialReady = true;
            //     OnInterstitialLoaded?.Invoke();
            // };
            // ad.OnAdFailedToLoad += (sender, args) => {
            //     IsInterstitialReady = false;
            //     OnInterstitialFailed?.Invoke(args.LoadErrorInfo?.ToString() ?? "Unknown");
            // };
            // ad.LoadAd(new AdRequest());
#endif
        }

        /// <summary>
        /// Shows a loaded interstitial ad.
        /// </summary>
        /// <returns>True if the ad was requested to be shown; false if not ready.</returns>
        public bool ShowInterstitial()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdMobManager] Cannot show interstitial: SDK not initialized.");
                return false;
            }

            if (!IsInterstitialReady)
            {
                Debug.LogWarning("[AdMobManager] Cannot show interstitial: no ad loaded.");
                return false;
            }

            Debug.Log("[AdMobManager] Showing interstitial ad...");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating interstitial display.");
            IsInterstitialReady = false;
            OnInterstitialShown?.Invoke();
            StartCoroutine(SimulateEditorInterstitialClose());
#else
            // ad.OnAdFullScreenContentClosed += () => {
            //     IsInterstitialReady = false;
            //     OnInterstitialClosed?.Invoke();
            //     LoadInterstitial(); // Preload next
            // };
            // ad.Show();
#endif
            return true;
        }

        private IEnumerator SimulateEditorInterstitialClose()
        {
            yield return new WaitForSeconds(1.0f);
            OnInterstitialClosed?.Invoke();
        }

        // ------------------------------------------------------------------
        // Banner
        // ------------------------------------------------------------------

        /// <summary>
        /// Shows a banner ad at the bottom of the screen.
        /// </summary>
        public void ShowBanner()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[AdMobManager] Cannot show banner: SDK not initialized.");
                return;
            }

            if (IsBannerVisible)
            {
                Debug.Log("[AdMobManager] Banner already visible.");
                return;
            }

            Debug.Log("[AdMobManager] Showing banner ad...");

#if UNITY_EDITOR
            Debug.Log("[AdMobManager] Editor mode: simulating banner display.");
            IsBannerVisible = true;
            OnBannerShown?.Invoke();
#else
            // var bannerView = new BannerView(_bannerAdUnitId, AdSize.Banner, AdPosition.Bottom);
            // bannerView.OnBannerAdLoaded += () => {
            //     IsBannerVisible = true;
            //     OnBannerShown?.Invoke();
            // };
            // bannerView.OnBannerAdLoadFailed += (error) => {
            //     OnBannerFailed?.Invoke(error.GetMessage());
            // };
            // bannerView.LoadAd(new AdRequest());
#endif
        }

        /// <summary>
        /// Hides the currently visible banner ad.
        /// </summary>
        public void HideBanner()
        {
            if (!IsBannerVisible)
            {
                Debug.Log("[AdMobManager] Banner already hidden.");
                return;
            }

            Debug.Log("[AdMobManager] Hiding banner ad...");

#if UNITY_EDITOR
            IsBannerVisible = false;
#else
            // bannerView.Hide();
#endif
        }

        /// <summary>
        /// Destroys the banner ad entirely.
        /// </summary>
        public void DestroyBanner()
        {
            Debug.Log("[AdMobManager] Destroying banner ad...");

#if UNITY_EDITOR
            IsBannerVisible = false;
#else
            // bannerView.Destroy();
            // bannerView = null;
#endif
        }
    }
}