using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Animates rendered vertices while preserving the authored image layout and tint.
    [DisallowMultipleComponent]
    public sealed class InventoryAppearEffect : BaseMeshEffect
    {
        [SerializeField, Min(0.05f)] private float duration = 0.32f;

        private float elapsed;
        private bool playing;

        // Restarts the appearance when an item is received, including additional money.
        public void Play()
        {
            elapsed = 0f;
            playing = true;
            graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            playing = false;
            base.OnDisable();
        }

        private void Update()
        {
            if (!playing || Time.deltaTime <= 0f)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= Mathf.Max(0.05f, duration))
            {
                playing = false;
            }

            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || !playing)
            {
                return;
            }

            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float back = progress - 1f;
            float scale = 1f + 0.35f * (2.7f * back * back * back + 1.7f * back * back);
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.3f));
            Vector3 center = graphic.rectTransform.rect.center;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position = center + (vertex.position - center) * scale;
                Color32 tint = vertex.color;
                tint.a = (byte)Mathf.RoundToInt(tint.a * opacity);
                vertex.color = tint;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
