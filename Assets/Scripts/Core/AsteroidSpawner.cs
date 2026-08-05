using UnityEngine;

namespace Hermes.SpaceDodger.Core
{
    public class AsteroidSpawner : MonoBehaviour
    {
        public static AsteroidSpawner Instance { get; private set; }

        [SerializeField] private GameObject asteroidPrefab;
        [SerializeField] private float baseSpawnInterval = 1.5f;
        [SerializeField] private float minSpawnInterval = 0.3f;
        [SerializeField] private float spawnXRange = 8f;
        [SerializeField] private float spawnY = 10f;

        private float timer = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            enabled = state == GameManager.GameState.Playing;
            if (state == GameManager.GameState.Playing)
                timer = 0f;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            float interval = Mathf.Max(baseSpawnInterval / GameManager.Instance.CurrentSpeedMultiplier, minSpawnInterval);
            timer += Time.deltaTime;

            if (timer >= interval)
            {
                SpawnAsteroid();
                timer = 0f;
            }
        }

        private void SpawnAsteroid()
        {
            if (asteroidPrefab == null) return;

            float x = Random.Range(-spawnXRange, spawnXRange);
            Vector3 pos = new Vector3(x, spawnY, 0f);
            GameObject obj = Instantiate(asteroidPrefab, pos, Quaternion.identity);
            obj.SetActive(true);

            var asteroid = obj.GetComponent<Asteroid>();
            if (asteroid != null)
                asteroid.SetSize(Random.Range(0.6f, 1.5f));
        }
    }
}