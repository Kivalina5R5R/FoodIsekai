using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Animates tile opacity using the same diagonal timing as paintingGround's WallBlockTransition.
    public sealed class WallBlockTransitionGraphic : MaskableGraphic
    {
        [SerializeField, Min(1)] private int columns = 11;
        [SerializeField, Min(1)] private int rows = 4;
        [SerializeField, Min(0.01f)] private float darkenDuration = 1f;
        [SerializeField, Min(0.01f)] private float tileFadeDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float revealDuration = 1f;
        [SerializeField, Min(0.01f)] private float revealFadeDuration = 0.16f;

        private float elapsed;
        private bool revealing;
        private bool visible;

        public IEnumerator Cover()
        {
            yield return Animate(false, darkenDuration);
        }

        public IEnumerator Reveal()
        {
            yield return Animate(true, revealDuration);
            Hide();
        }

        public void Hide()
        {
            visible = false;
            SetVerticesDirty();
        }

        private IEnumerator Animate(bool reveal, float duration)
        {
            visible = true;
            revealing = reveal;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                SetVerticesDirty();
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            elapsed = duration;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible) return;

            Rect rect = GetPixelAdjustedRect();
            int width = Mathf.Max(1, columns);
            int height = Mathf.Max(1, rows);
            float coverDuration = Mathf.Max(0.01f, darkenDuration);
            float coverFade = Mathf.Clamp(tileFadeDuration, 0.01f, coverDuration);
            float uncoverDuration = Mathf.Max(0.01f, revealDuration);
            float uncoverFade = Mathf.Clamp(revealFadeDuration, 0.01f, uncoverDuration);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float diagonal = (width - 1 - x + height - 1 - y)
                        / (float)Mathf.Max(1, width + height - 2);
                    float delay = diagonal * (coverDuration - coverFade);
                    float fade = coverFade;
                    if (revealing)
                    {
                        delay = delay / coverDuration * (uncoverDuration - uncoverFade);
                        fade = uncoverFade;
                    }
                    float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - delay) / fade));
                    Color tint = color;
                    tint.a *= revealing ? 1f - opacity : opacity;
                    float left = rect.xMin + rect.width * x / width;
                    float right = rect.xMin + rect.width * (x + 1) / width;
                    float bottom = rect.yMin + rect.height * y / height;
                    float top = rect.yMin + rect.height * (y + 1) / height;
                    int first = mesh.currentVertCount;
                    mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
                    mesh.AddTriangle(first, first + 1, first + 2);
                    mesh.AddTriangle(first, first + 2, first + 3);
                }
            }
        }
    }
}
