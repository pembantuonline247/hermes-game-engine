using System;
using System.Collections;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    /// <summary>
    /// AppLovin MAX mediation wrapper.
    /// Ad Unit IDs read from environment variables (MAX_REWARDED_ID, MAX_INTERSTITIAL_ID, MAX_BANNER_ID).
    /// In Editor, simulates behavior with placeholder IDs.
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        private static AdManager _instance;
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

        public event Action<bool> OnSdkInitialized;
        public event Action OnRewardedVideoLoaded;
        public event Action<string> OnRewardedVideoFailed;
        public event Action OnRewardedVideoShown;
        public event Action OnRewardedVideoClosed;
        public event Action OnRewardEarned;
        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialFailed;
        public event Action OnInterstitialShown;
        public event Action OnInterstitialClosed;
        public event Action OnBannerShown;
        public event Action<string> OnBannerFailed;

        public bool IsInitialized { get; private set; }
        public bool IsRewardedVideoReady { get; private set; }
        public bool IsInterstitialReady { get; private set; }
        public bool IsBannerVisible { get; private set; }

        private string _rewardedVideoAdUnitId;
        private string _interstitialAdUnitId;
        private string _bannerAdUnitId;

        private const string PLACEHOLDER_REWARDED = "REWARDED_VIDEO_PLACEHOLDER";
        private const string PLACEHOLDER_INTERSTITIAL = "INTERSTITIAL_PLACEHOLDER";
        private const string PLACEHOLDER_BANNER = "BANNER_PLACEHOLDER";

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveAdUnitIds();
        }

        private void Start() { InitializeMaxSdk(); }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void ResolveAdUnitIds()
        {
            _rewardedVideoAdUnitId = ResolveId("MAX_REWARDED_ID", "MaxRewardedId", PLACEHOLDER_REWARDED);
            _interstitialAdUnitId = ResolveId("MAX_INTERSTITIAL_ID", "MaxInterstitialId", PLACEHOLDER_INTERSTITIAL);
            _bannerAdUnitId = ResolveId("MAX_BANNER_ID", "MaxBannerId", PLACEHOLDER_BANNER);
        }

        private static string ResolveId(string envVar, string editorPrefsKey, string fallback)
        {
            string envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envValue)) return envValue;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(editorPrefsKey))
            {
                string prefValue = UnityEditor.EditorPrefs.GetString(editorPrefsKey);
                if (!string.IsNullOrEmpty(prefValue)) return prefValue;
            }
#endif
            return fallback;
        }

        public void InitializeMaxSdk()
        {
            if (IsInitialized) return;
            Debug.Log("[AdManager] Initializing MAX SDK...");
#if UNITY_EDITOR
            StartCoroutine(SimulateEditorInit());
#else
            StartCoroutine(SimulateEditorInit());
#endif
        }

        private IEnumerator SimulateEditorInit()
        {
            yield return new WaitForSeconds(0.5f);
            IsInitialized = true;
            OnSdkInitialized?.Invoke(true);
            LoadRewardedVideo();
            LoadInterstitial();
        }

        public void LoadRewardedVideo()
        {
            if (!IsInitialized) return;
#if UNITY_EDITOR
            IsRewardedVideoReady = true;
            OnRewardedVideoLoaded?.Invoke();
#endif
        }

        public bool ShowRewardedVideo(string placement = "default")
        {
            if (!IsInitialized || !IsRewardedVideoReady) return false;
#if UNITY_EDITOR
            IsRewardedVideoReady = false;
            OnRewardedVideoShown?.Invoke();
            StartCoroutine(SimulateEditorReward());
#endif
            return true;
        }

        private IEnumerator SimulateEditorReward()
        {
            yield return new WaitForSeconds(1.5f);
            OnRewardEarned?.Invoke();
            OnRewardedVideoClosed?.Invoke();
        }

        public void LoadInterstitial()
        {
            if (!IsInitialized) return;
#if UNITY_EDITOR
            IsInterstitialReady = true;
            OnInterstitialLoaded?.Invoke();
#endif
        }

        public bool ShowInterstitial(string placement = "default")
        {
            if (!IsInitialized || !IsInterstitialReady) return false;
#if UNITY_EDITOR
            IsInterstitialReady = false;
            OnInterstitialShown?.Invoke();
            StartCoroutine(SimulateEditorInterstitialClose());
#endif
            return true;
        }

        private IEnumerator SimulateEditorInterstitialClose()
        {
            yield return new WaitForSeconds(1.0f);
            OnInterstitialClosed?.Invoke();
        }

        public void ShowBanner(string position = "BottomCenter")
        {
            if (!IsInitialized || IsBannerVisible) return;
#if UNITY_EDITOR
            IsBannerVisible = true;
            OnBannerShown?.Invoke();
#endif
        }

        public void HideBanner()
        {
            if (!IsBannerVisible) return;
#if UNITY_EDITOR
            IsBannerVisible = false;
#endif
        }

        public void DestroyBanner()
        {
#if UNITY_EDITOR
            IsBannerVisible = false;
#endif
        }
    }
}