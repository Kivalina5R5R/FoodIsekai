using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Pearl-silver coating follows the button sprite's alpha and stays below its label.
    public sealed class ChaosButtonSheen : MaskableGraphic
    {
        [SerializeField] private Sprite buttonSprite;
        [SerializeField, Min(0.1f)] private float cycleSeconds = 5.8f;
        [SerializeField, Range(0f, 1f)] private float strength = 0.3f;
        private float elapsed;

        public override Texture mainTexture => buttonSprite != null ? buttonSprite.texture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!Application.isPlaying || buttonSprite == null) return;
            Rect area = rectTransform.rect;
            Vector4 uv = DataUtility.GetOuterUV(buttonSprite);
            float phase = Mathf.Repeat(elapsed / Mathf.Max(0.1f, cycleSeconds), 1f);
            float sweep = Mathf.Lerp(-0.24f, 1.24f, 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI));
            const int columns = 64;
            const int rows = 16;
            for (int row = 0; row <= rows; row++)
            {
                float y = row / (float)rows;
                for (int column = 0; column <= columns; column++)
                {
                    float x = column / (float)columns;
                    float offset = x - sweep;
                    float distance = offset / 0.062f;
                    float core = Mathf.Exp(-0.5f * distance * distance);
                    Color tint = Color.Lerp(new Color(0.67f, 0.72f, 0.79f), new Color(0.98f, 0.99f, 1f), core);
                    float fade = Mathf.Clamp01(1f - Mathf.Abs(offset) / 0.1f);
                    tint.a = fade * fade * fade * (fade * (6f * fade - 15f) + 10f) * strength;
                    mesh.AddVert(new Vector2(Mathf.Lerp(area.xMin, area.xMax, x), Mathf.Lerp(area.yMin, area.yMax, y)),
                        tint, new Vector2(Mathf.Lerp(uv.x, uv.z, x), Mathf.Lerp(uv.y, uv.w, y)));
                    if (row == 0 || column == 0) continue;
                    int top = row * (columns + 1) + column;
                    int bottom = top - columns - 1;
                    mesh.AddTriangle(bottom - 1, top - 1, top);
                    mesh.AddTriangle(bottom - 1, top, bottom);
                }
            }
        }
    }
}
