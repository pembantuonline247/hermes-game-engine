using System.Collections;
using UnityEngine;

namespace Hermes.SpaceDodger.Core
{
    public class DeathEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private int particleCount = 20;
        [SerializeField] private float speed = 5f;

        private void Start()
        {
            for (int i = 0; i < particleCount; i++)
            {
                GameObject p = new GameObject("Particle");
                p.transform.SetParent(transform);
                p.transform.localPosition = Vector3.zero;

                var sr = p.AddComponent<SpriteRenderer>();
                sr.color = Color.yellow;
                sr.sprite = CreateCircleSprite();
                sr.sortingOrder = 10;

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float dist = Random.Range(1f, 3f);

                StartCoroutine(AnimateParticle(p, dir * dist, duration * Random.Range(0.5f, 1f)));
            }

            Destroy(gameObject, duration + 0.1f);
        }

        private Sprite CreateCircleSprite()
        {
            Texture2D tex = new Texture2D(8, 8);
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                {
                    float dx = x - 3.5f, dy = y - 3.5f;
                    tex.SetPixel(x, y, (dx * dx + dy * dy) <= 16f ? Color.white : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }

        private IEnumerator AnimateParticle(GameObject p, Vector2 target, float time)
        {
            Vector3 start = p.transform.localPosition;
            float elapsed = 0f;
            while (elapsed < time && p != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / time;
                p.transform.localPosition = Vector3.Lerp(start, target, t);
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1, 1, 0, 1 - t);
                yield return null;
            }
            if (p != null) Destroy(p);
        }
    }
}