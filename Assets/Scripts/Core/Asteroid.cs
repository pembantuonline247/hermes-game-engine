using UnityEngine;

namespace Hermes.SpaceDodger.Core
{
    public class Asteroid : MonoBehaviour
    {
        [SerializeField] private float baseSpeed = 3f;
        [SerializeField] private float minRotationSpeed = 30f;
        [SerializeField] private float maxRotationSpeed = 180f;

        private float rotationSpeed;
        private Vector2 direction;

        private void Start()
        {
            direction = Vector2.down;
            rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
            direction.x = Random.Range(-0.3f, 0.3f);
            direction.Normalize();
        }

        private void Update()
        {
            float speed = baseSpeed;
            if (GameManager.Instance != null)
                speed *= GameManager.Instance.CurrentSpeedMultiplier;

            transform.Translate(direction * speed * Time.deltaTime, Space.World);
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (transform.position.y < -12f || Mathf.Abs(transform.position.x) > 12f)
            {
                Destroy(gameObject);
            }
        }

        public void SetSize(float sizeMultiplier)
        {
            transform.localScale = Vector3.one * Mathf.Clamp(sizeMultiplier, 0.5f, 2f);
        }
    }
}