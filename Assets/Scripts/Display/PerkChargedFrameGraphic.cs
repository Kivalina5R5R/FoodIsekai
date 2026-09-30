using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A warm gold and blush aura (rainbow for Big) follows the authored paper silhouette, with kira glints running around it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkChargedFrameGraphic : MaskableGraphic
    {
        private const int Glints = 6;
        private Rect frame;
        private float strength;
        private float seconds;
        private bool rainbow;
        public Rect FrameBounds => frame;

        // The Big tier follows the Big card's silhouette and cycles a rainbow around the edge.
        public void SetFrame(Rect bounds, bool rainbowTier = false)
        {
            frame = bounds;
            rainbow = rainbowTier;
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
                Vector2 a = PerkFrameContour.Sample(frame, i / 192f, rainbow);
                Vector2 b = PerkFrameContour.Sample(frame, (i + 1f) / 192f, rainbow);
                Vector2 normalA = (a - frame.center).normalized;
                Vector2 normalB = (b - frame.center).normalized;
                float waveA = 2f + Mathf.Sin(i / 192f * Mathf.PI * 16f - seconds * 9f) * 1.6f;
                float waveB = 2f + Mathf.Sin((i + 1f) / 192f * Mathf.PI * 16f - seconds * 9f) * 1.6f;
                a += normalA * waveA;
                b += normalB * waveB;
                float hue = i / 192f * 2f - seconds * .35f;
                Band(mesh, a, b, a + normalA * (rainbow ? 20f : 14f), b + normalB * (rainbow ? 20f : 14f),
                    rainbow ? PerkEffectMesh.Rainbow(hue + .15f, strength * .5f) : PerkEffectMesh.Tint(PerkEffectMesh.Blush, strength * .45f), 0f);
                Band(mesh, a, b, a + normalA * 8f, b + normalB * 8f,
                    rainbow ? PerkEffectMesh.Rainbow(hue, strength * .85f, .7f) : PerkEffectMesh.Tint(PerkEffectMesh.Gold, strength * .75f), 0f);
                Band(mesh, a - normalA, b - normalB, a + normalA * 2f, b + normalB * 2f,
                    PerkEffectMesh.Tint(PerkEffectMesh.Cream, strength * .95f), strength * .2f);
            }
            int glints = rainbow ? Glints * 2 : Glints;
            for (int i = 0; i < glints; i++)
            {
                float along = Mathf.Repeat(i / (float)glints + seconds * .16f, 1f);
                Vector2 point = PerkFrameContour.Sample(frame, along, rainbow);
                point += (point - frame.center).normalized * 3f;
                float twinkle = .5f + .5f * Mathf.Sin(seconds * 7f + i * 2.1f);
                PerkEffectMesh.Sparkle(mesh, point, frame.height * (.035f + .03f * twinkle) * strength, 0f,
                    PerkEffectMesh.Tint(PerkEffectMesh.Cream, strength * (.55f + .45f * twinkle)));
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
