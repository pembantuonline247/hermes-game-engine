using UnityEngine;

namespace Hermes.SpaceDodger.Core
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float boundaryX = 8f;

        private Rigidbody2D rb;
        private bool isDead = false;

        private void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            switch (state)
            {
                case GameManager.GameState.Playing:
                    gameObject.SetActive(true);
                    isDead = false;
                    transform.position = new Vector3(0f, -7f, 0f);
                    break;
                case GameManager.GameState.Menu:
                case GameManager.GameState.GameOver:
                    gameObject.SetActive(false);
                    break;
            }
        }

        private void Update()
        {
            if (isDead || GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing)
                return;

            float move = Input.GetAxisRaw("Horizontal");
            Vector3 pos = transform.position;
            pos.x += move * moveSpeed * Time.deltaTime;
            pos.x = Mathf.Clamp(pos.x, -boundaryX, boundaryX);
            transform.position = pos;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead) return;
            if (other.CompareTag("Asteroid"))
            {
                isDead = true;
                gameObject.SetActive(false);
                GameManager.Instance.GameOver();
            }
        }
    }
}