using System;
using System.Collections;
using UnityEngine;

namespace Hermes.GameEngine.Monetization
{
    /// <summary>
    /// Google AdMob standalone manager.
    /// Ad Unit IDs read from environment variables (ADMOB_REWARDED_ID, ADMOB_INTERSTITIAL_ID, ADMOB_BANNER_ID).
    /// Falls back to Google test IDs if not set.
    /// </summary>
    public class AdMobManager : MonoBehaviour
    {
        private static AdMobManager _instance;
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

        private const string TEST_REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
        private const string TEST_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/1033173712";
        private const string TEST_BANNER_ID = "ca-app-pub-3940256099942544/6300978111";

        private string _rewardedVideoAdUnitId;
        private string _interstitialAdUnitId;
        private string _bannerAdUnitId;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveAdUnitIds();
        }

        private void Start() { InitializeAdMobSdk(); }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void ResolveAdUnitIds()
        {
            _rewardedVideoAdUnitId = ResolveId("ADMOB_REWARDED_ID", "AdMobRewardedId", TEST_REWARDED_ID);
            _interstitialAdUnitId = ResolveId("ADMOB_INTERSTITIAL_ID", "AdMobInterstitialId", TEST_INTERSTITIAL_ID);
            _bannerAdUnitId = ResolveId("ADMOB_BANNER_ID", "AdMobBannerId", TEST_BANNER_ID);
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

        public void InitializeAdMobSdk()
        {
            if (IsInitialized) return;
            Debug.Log("[AdMobManager] Initializing AdMob SDK...");
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

        public bool ShowRewardedVideo()
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

        public bool ShowInterstitial()
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

        public void ShowBanner()
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