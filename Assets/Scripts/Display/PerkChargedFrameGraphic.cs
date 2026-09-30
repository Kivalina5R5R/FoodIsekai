using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Gold ripples follow the authored paper silhouette while energy charges the card.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkChargedFrameGraphic : MaskableGraphic
    {
        private Rect frame;
        private float strength;
        private float seconds;

        public void SetFrame(Rect bounds)
        {
            frame = bounds;
            raycastTarget = false;
        }

        public void SetCharge(float amount, float time)
        {
            strength = Mathf.Clamp01(amount);
            seconds = time;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (strength <= 0f) return;
            for (int i = 0; i < 192; i++)
            {
                Vector2 a = PerkFrameContour.Sample(frame, i / 192f);
                Vector2 b = PerkFrameContour.Sample(frame, (i + 1f) / 192f);
                Vector2 normalA = (a - frame.center).normalized;
                Vector2 normalB = (b - frame.center).normalized;
                float waveA = 2f + Mathf.Sin(i / 192f * Mathf.PI * 16f - seconds * 9f) * 1.6f;
                float waveB = 2f + Mathf.Sin((i + 1f) / 192f * Mathf.PI * 16f - seconds * 9f) * 1.6f;
                a += normalA * waveA;
                b += normalB * waveB;
                Band(mesh, a, b, a + normalA * 11f, b + normalB * 11f,
                    new Color(1f, .62f, .1f, strength * .7f), 0f);
                Band(mesh, a - normalA, b - normalB, a + normalA * 2f, b + normalB * 2f,
                    new Color(1f, .94f, .55f, strength * .95f), strength * .2f);
            }
        }

        private static void Band(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float outerAlpha)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            color.a = outerAlpha;
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
