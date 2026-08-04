using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hermes.GameEngine.Core
{
    /// <summary>
    /// Handles asynchronous scene loading with configurable loading screen callbacks.
    /// Attach to a persistent GameObject (e.g., a root "App" object) for global access.
    /// </summary>
    [RequireComponent(typeof(MonoBehaviour))]
    public class SceneLoader : MonoBehaviour
    {
        /// <summary>
        /// Signature for callbacks invoked during the loading progress.
        /// </summary>
        /// <param name="progress">Normalized progress value between 0.0 and 1.0.</param>
        public delegate void ProgressCallback(float progress);

        /// <summary>
        /// Signature for callbacks invoked when loading completes.
        /// </summary>
        public delegate void CompleteCallback();

        private static SceneLoader _instance;

        /// <summary>
        /// Gets the singleton instance of the SceneLoader.
        /// </summary>
        public static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    _instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Global loading state — true while a scene is being loaded asynchronously.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// The current normalized load progress (0.0 to 1.0).
        /// </summary>
        public float LoadProgress { get; private set; }

        /// <summary>
        /// The name of the scene currently being loaded. Empty string when idle.
        /// </summary>
        public string LoadingSceneName { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[SceneLoader] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Loads a scene by name asynchronously.
        /// </summary>
        /// <param name="sceneName">The name or path of the scene to load.</param>
        /// <param name="loadMode">Specifies whether to load additive or single mode.</param>
        /// <param name="allowSceneActivation">
        /// If true, the scene activates automatically when loading reaches 0.9.
        /// If false, you must call <see cref="AllowSceneActivation"/> to complete loading.
        /// </param>
        /// <param name="onProgress">Optional callback invoked with normalized progress [0..1].</param>
        /// <param name="onComplete">Optional callback invoked when loading finishes.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sceneName"/> is null or empty.</exception>
        public void LoadScene(
            string sceneName,
            LoadSceneMode loadMode = LoadSceneMode.Single,
            bool allowSceneActivation = true,
            ProgressCallback onProgress = null,
            CompleteCallback onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("[SceneLoader] Scene name cannot be null or empty.", nameof(sceneName));

            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading '{LoadingSceneName}'. Ignoring request to load '{sceneName}'.");
                return;
            }

            StartCoroutine(LoadSceneAsyncRoutine(sceneName, loadMode, allowSceneActivation, onProgress, onComplete));
        }

        /// <summary>
        /// Loads a scene by build index asynchronously.
        /// </summary>
        /// <param name="buildIndex">The build index of the scene to load.</param>
        /// <param name="loadMode">Specifies whether to load additive or single mode.</param>
        /// <param name="allowSceneActivation">
        /// If true, the scene activates automatically when loading reaches 0.9.
        /// If false, you must call <see cref="AllowSceneActivation"/> to complete loading.
        /// </param>
        /// <param name="onProgress">Optional callback invoked with normalized progress [0..1].</param>
        /// <param name="onComplete">Optional callback invoked when loading finishes.</param>
        public void LoadScene(
            int buildIndex,
            LoadSceneMode loadMode = LoadSceneMode.Single,
            bool allowSceneActivation = true,
            ProgressCallback onProgress = null,
            CompleteCallback onComplete = null)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[SceneLoader] Build index {buildIndex} is out of range.");
                return;
            }

            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading '{LoadingSceneName}'. Ignoring request to load index {buildIndex}.");
                return;
            }

            StartCoroutine(LoadSceneAsyncRoutine(buildIndex, loadMode, allowSceneActivation, onProgress, onComplete));
        }

        /// <summary>
        /// Allows a scene that was loaded with <paramref name="allowSceneActivation"/> = false to complete activation.
        /// </summary>
        public void AllowSceneActivation()
        {
            if (_activeOperation != null)
            {
                _activeOperation.allowSceneActivation = true;
            }
        }

        /// <summary>
        /// Unloads a scene that was loaded additively.
        /// </summary>
        /// <param name="sceneName">The name of the scene to unload.</param>
        /// <param name="onComplete">Optional callback invoked when the scene has been unloaded.</param>
        public void UnloadScene(string sceneName, CompleteCallback onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] Cannot unload: scene name is null or empty.");
                return;
            }

            StartCoroutine(UnloadSceneRoutine(sceneName, onComplete));
        }

        // ------------------------------------------------------------------
        // Internal
        // ------------------------------------------------------------------

        private AsyncOperation _activeOperation;

        private IEnumerator LoadSceneAsyncRoutine(
            string sceneName,
            LoadSceneMode loadMode,
            bool allowSceneActivation,
            ProgressCallback onProgress,
            CompleteCallback onComplete)
        {
            IsLoading = true;
            LoadingSceneName = sceneName;
            LoadProgress = 0f;

            Debug.Log($"[SceneLoader] Starting async load: '{sceneName}' (mode: {loadMode}).");

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for '{sceneName}'. Scene may not be in build settings.");
                IsLoading = false;
                LoadingSceneName = string.Empty;
                yield break;
            }

            operation.allowSceneActivation = allowSceneActivation;
            _activeOperation = operation;

            while (!operation.isDone)
            {
                // AsyncOperation.progress goes from 0.0 to 0.9 during loading, then jumps to 1.0 on activation.
                float rawProgress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadProgress = rawProgress;

                onProgress?.Invoke(rawProgress);

                // If we're at 0.9 (real progress) and the scene is waiting for activation, consider it "ready."
                if (operation.progress >= 0.9f && !operation.allowSceneActivation)
                {
                    // Loading is essentially complete; just waiting for activation trigger.
                }

                yield return null;
            }

            _activeOperation = null;
            LoadProgress = 1f;
            LoadingSceneName = string.Empty;
            IsLoading = false;

            Debug.Log($"[SceneLoader] Finished loading '{sceneName}'.");
            onComplete?.Invoke();
        }

        private IEnumerator LoadSceneAsyncRoutine(
            int buildIndex,
            LoadSceneMode loadMode,
            bool allowSceneActivation,
            ProgressCallback onProgress,
            CompleteCallback onComplete)
        {
            IsLoading = true;
            LoadingSceneName = $"BuildIndex:{buildIndex}";
            LoadProgress = 0f;

            Debug.Log($"[SceneLoader] Starting async load: build index {buildIndex} (mode: {loadMode}).");

            AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, loadMode);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync returned null for build index {buildIndex}.");
                IsLoading = false;
                LoadingSceneName = string.Empty;
                yield break;
            }

            operation.allowSceneActivation = allowSceneActivation;
            _activeOperation = operation;

            while (!operation.isDone)
            {
                float rawProgress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadProgress = rawProgress;
                onProgress?.Invoke(rawProgress);
                yield return null;
            }

            _activeOperation = null;
            LoadProgress = 1f;
            LoadingSceneName = string.Empty;
            IsLoading = false;

            Debug.Log($"[SceneLoader] Finished loading build index {buildIndex}.");
            onComplete?.Invoke();
        }

        private IEnumerator UnloadSceneRoutine(string sceneName, CompleteCallback onComplete)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] UnloadSceneAsync returned null for '{sceneName}'.");
                yield break;
            }

            yield return operation;

            Debug.Log($"[SceneLoader] Unloaded scene '{sceneName}'.");
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            _activeOperation = null;
        }
    }
}
