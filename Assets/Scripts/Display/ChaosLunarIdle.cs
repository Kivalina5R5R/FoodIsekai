using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Animates only Lunar's rendered vertices, leaving the authored transform untouched.
    [DisallowMultipleComponent]
    public sealed class ChaosLunarIdle : BaseMeshEffect
    {
        [SerializeField, Min(0.1f)] private float breathSeconds = 3.8f;
        [SerializeField, Range(0f, 0.03f)] private float breathAmount = 0.017f;
        [SerializeField, Min(0.1f)] private float floatSeconds = 5.5f;
        [SerializeField, Min(0f)] private float floatPixels = 22f;
        [SerializeField, Range(0f, 1f)] private float swayDegrees = 0.35f;
        [SerializeField, Min(0.1f)] private float swaySeconds = 7.3f;
        private float elapsed;

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || !Application.isPlaying) return;
            float inhale = 0.5f - 0.5f * Mathf.Cos(elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds));
            float rise = Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, floatSeconds)) * floatPixels;
            float angle = Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, swaySeconds)) * swayDegrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(angle);
            float cosine = Mathf.Cos(angle);
            Vector2 center = graphic.rectTransform.rect.center;
            var vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 point = vertex.position;
                float x = (point.x - center.x) * (1f + inhale * breathAmount * 0.35f);
                float y = (point.y - center.y) * (1f + inhale * breathAmount);
                point.x = center.x + x * cosine - y * sine;
                point.y = center.y + x * sine + y * cosine + rise;
                vertex.position = point;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
