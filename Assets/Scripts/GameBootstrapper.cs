using UnityEngine;
using UnityEngine.UI;
using Hermes.SpaceDodger.Core;
using Hermes.GameEngine.Monetization;
using Hermes.GameEngine.Analytics;

namespace Hermes.SpaceDodger
{
    /// <summary>
    /// Builds the entire Space Dodger game at runtime.
    /// This avoids fragile scene/prefab GUID references so the project builds
    /// cleanly in CI without manual scene wiring.
    /// Attach this to the Main Camera in Main.unity.
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure monetization managers exist
            if (AdManager.Instance != null) { }
            if (AdMobManager.Instance != null) { }
            if (IAPManager.Instance != null) { }
            if (AnalyticsManager.Instance != null) { }

            // Create the game
            CreateCameraSetup();
            CreateGameManager();
            CreatePlayer();
            CreateAsteroidSpawner();
            CreateUI();
        }

        private void CreateCameraSetup()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0, -10);
        }

        private void CreateGameManager()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }

        private void CreatePlayer()
        {
            var go = new GameObject("Player");
            go.tag = "Player";

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateShipSprite();
            sr.color = Color.cyan;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 0.8f);
            col.isTrigger = true;

            var pc = go.AddComponent<PlayerController>();

            // Create death effect prefab-less object
            var deathGo = new GameObject("DeathEffect");
            deathGo.AddComponent<DeathEffect>();
            deathGo.SetActive(false);
            deathGo.transform.SetParent(go.transform);
            // Reference via serialized field is not possible at runtime; use Find
            // We'll assign in PlayerController via a static reference instead.
            DontDestroyOnLoad(deathGo);
        }

        private void CreateAsteroidSpawner()
        {
            var go = new GameObject("AsteroidSpawner");
            var spawner = go.AddComponent<AsteroidSpawner>();

            // Create asteroid prefab
            var asteroidGo = new GameObject("Asteroid");
            asteroidGo.tag = "Asteroid";
            var asr = asteroidGo.AddComponent<SpriteRenderer>();
            asr.sprite = CreateAsteroidSprite();
            asr.color = new Color(0.6f, 0.4f, 0.8f);
            asr.sortingOrder = 3;

            var acol = asteroidGo.AddComponent<PolygonCollider2D>();
            acol.isTrigger = true;

            asteroidGo.AddComponent<Asteroid>();

            // Assign via reflection since field is private
            var field = typeof(AsteroidSpawner).GetField("asteroidPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(spawner, asteroidGo);

            asteroidGo.SetActive(false);
        }

        private void CreateUI()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Score text (top center)
            var scoreGo = new GameObject("ScoreText");
            scoreGo.transform.SetParent(canvasGo.transform, false);
            var scoreText = scoreGo.AddComponent<Text>();
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreText.fontSize = 48;
            scoreText.alignment = TextAnchor.UpperCenter;
            scoreText.color = Color.white;
            var scoreRect = scoreGo.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0, -20);
            scoreRect.sizeDelta = new Vector2(300, 60);

            // High score text
            var hsGo = new GameObject("HighScoreText");
            hsGo.transform.SetParent(canvasGo.transform, false);
            var hsText = hsGo.AddComponent<Text>();
            hsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hsText.fontSize = 24;
            hsText.alignment = TextAnchor.MiddleCenter;
            hsText.color = new Color(0.8f, 0.8f, 0.8f);
            var hsRect = hsGo.GetComponent<RectTransform>();
            hsRect.anchorMin = new Vector2(0.5f, 1f);
            hsRect.anchorMax = new Vector2(0.5f, 1f);
            hsRect.pivot = new Vector2(0.5f, 1f);
            hsRect.anchoredPosition = new Vector2(0, -80);
            hsRect.sizeDelta = new Vector2(300, 30);

            // Start panel
            var startPanel = CreatePanel(canvasGo.transform, "StartPanel");
            var startText = CreateText(startPanel.transform, "StartText", "SPACE DODGER\n\nAvoid the asteroids!\n\n[Start]",
                40, TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(600, 200));
            var startBtn = CreateButton(startPanel.transform, "StartButton", "▶ START",
                new Vector2(0, -80), new Vector2(240, 60), () => {
                    if (GameManager.Instance != null) GameManager.Instance.StartGame();
                });

            // Game over panel
            var overPanel = CreatePanel(canvasGo.transform, "GameOverPanel");
            var overText = CreateText(overPanel.transform, "FinalScoreText", "Score: 0",
                44, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(500, 60));
            var newHigh = CreateText(overPanel.transform, "NewHighScoreText", "★ NEW HIGH SCORE! ★",
                28, TextAnchor.MiddleCenter, new Vector2(0, -10), new Vector2(500, 40));
            newHigh.gameObject.SetActive(false);
            var retryBtn = CreateButton(overPanel.transform, "RestartButton", "↻ RESTART",
                new Vector2(0, -80), new Vector2(240, 60), () => {
                    if (GameManager.Instance != null) GameManager.Instance.RestartGame();
                });
            var adBtn = CreateButton(overPanel.transform, "AdButton", "👁 WATCH AD (2x coins)",
                new Vector2(0, -160), new Vector2(300, 50), () => {
                    bool shown = false;
                    if (AdManager.Instance != null && AdManager.Instance.IsRewardedVideoReady)
                        shown = AdManager.Instance.ShowRewardedVideo("continue");
                    if (!shown && AdMobManager.Instance != null && AdMobManager.Instance.IsRewardedVideoReady)
                        shown = AdMobManager.Instance.ShowRewardedVideo();
                    if (!shown) Debug.Log("Rewarded ad not ready");
                });

            overPanel.SetActive(false);

            // Wire ScoreManager
            var smGo = new GameObject("ScoreManager");
            smGo.transform.SetParent(canvasGo.transform, false);
            var sm = smGo.AddComponent<ScoreManager>();
            SetPrivate(sm, "scoreText", scoreText);
            SetPrivate(sm, "highScoreText", hsText);
            SetPrivate(sm, "gameOverPanel", overPanel);
            SetPrivate(sm, "finalScoreText", overText);
            SetPrivate(sm, "newHighScoreText", newHigh);
            SetPrivate(sm, "startPanel", startPanel);
        }

        private static void SetPrivate(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(obj, value);
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.8f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 400);
            return go;
        }

        private static Text CreateText(Transform parent, string name, string text, int size,
            TextAnchor align, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            return t;
        }

        private static Button CreateButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 sizeDelta, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.3f, 0.8f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            t.raycastTarget = false;
            var trect = textGo.GetComponent<RectTransform>();
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            return btn;
        }

        private static Sprite CreateShipSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                {
                    float nx = (x - 16f) / 16f;
                    float ny = (y - 16f) / 16f;
                    // Triangle pointing up
                    bool inTriangle = ny > Mathf.Abs(nx) * 1.2f - 0.3f && ny < 1f;
                    tex.SetPixel(x, y, inTriangle ? Color.white : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite CreateAsteroidSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                {
                    float dx = (x - 16f) / 16f;
                    float dy = (y - 16f) / 16f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inCircle = dist <= 0.9f;
                    // Rough rocky edge
                    inCircle = inCircle && ((x + y) % 3 != 0);
                    tex.SetPixel(x, y, inCircle ? Color.white : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}