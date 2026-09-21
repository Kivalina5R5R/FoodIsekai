using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A parchment menu sweeps over the wall between services, separate from the intro curtain.
    public sealed class MealMenuTransition : MaskableGraphic
    {
        [SerializeField] private TMP_Text heading;
        [Header("Transition Text")]
        [SerializeField] private string breakfastTitle = "BREAKFAST";
        [SerializeField] private string lunchTitle = "LUNCH";
        [SerializeField] private string dinnerTitle = "DINNER";
        [SerializeField] private string breakTitle = "SERVICE BREAK";
        [SerializeField] private string resultsTitle = "SERVICE RESULTS";
        [Header("Artwork")]
        [Tooltip("Optional full-page sprite. Leave empty to keep the original parchment design. Use Full Rect mesh when importing the sprite.")]
        [SerializeField] private Sprite pageArtwork;
        [SerializeField] private Color trim = new Color(0.77f, 0.57f, 0.3f, 1f);
        [SerializeField] private Color ribbon = new Color(0.46f, 0.195f, 0.17f, 1f);
        [SerializeField, Min(0.05f)] private float coverSeconds = 0.4f;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.5f;
        [SerializeField, Min(0f)] private float titleHoldSeconds = 0.55f;
        private float offset = -1f;
        private bool visible;
        private Vector2 headingRestPosition;
        private bool headingPositionCached;
        public bool IsCovered => visible && Mathf.Approximately(offset, 0f);
        public override Texture mainTexture => pageArtwork != null ? pageArtwork.texture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying && heading != null && !headingPositionCached)
            {
                headingRestPosition = heading.rectTransform.anchoredPosition;
                headingPositionCached = true;
            }
            if (Application.isPlaying)
            {
                visible = false;
                offset = -1f;
                if (heading != null) heading.gameObject.SetActive(false);
            }
            else
            {
                visible = true;
                offset = 0f;
                SetVerticesDirty();
            }
        }

        public IEnumerator Cover(string title)
        {
            gameObject.SetActive(true);
            title = ResolveTitle(title);
            if (IsCovered)
            {
                if (heading != null) heading.text = title;
                yield break;
            }
            if (heading != null)
            {
                heading.text = title;
                heading.gameObject.SetActive(true);
            }
            if (!visible) offset = -1f;
            visible = true;
            MoveHeadingWithPage();
            yield return Slide(0f, coverSeconds);
        }

        private string ResolveTitle(string title)
        {
            switch (title)
            {
                case "BREAKFAST": return breakfastTitle;
                case "LUNCH": return lunchTitle;
                case "DINNER": return dinnerTitle;
                case "SERVICE BREAK": return breakTitle;
                case "SERVICE RESULTS": return resultsTitle;
                default: return title;
            }
        }

        public IEnumerator Reveal()
        {
            if (titleHoldSeconds > 0f) yield return new WaitForSecondsRealtime(titleHoldSeconds);
            yield return Slide(1f, revealSeconds);
            Hide();
        }

        public void Hide()
        {
            visible = false;
            offset = -1f;
            if (heading != null) heading.gameObject.SetActive(false);
            if (heading != null && headingPositionCached)
                heading.rectTransform.anchoredPosition = headingRestPosition;
            SetVerticesDirty();
            gameObject.SetActive(false);
        }

        private IEnumerator Slide(float target, float seconds)
        {
            float from = offset;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
                offset = Mathf.Lerp(from, target, t);
                MoveHeadingWithPage();
                SetVerticesDirty();
                yield return null;
            }
            offset = target;
            MoveHeadingWithPage();
            SetVerticesDirty();
        }

        private void MoveHeadingWithPage()
        {
            if (heading == null) return;
            heading.rectTransform.anchoredPosition = headingRestPosition +
                Vector2.right * (offset * (rectTransform.rect.width + 24f));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible) return;
            Rect area = rectTransform.rect;
            float shift = offset * (area.width + 24f);
            Rect page = new Rect(area.xMin + shift, area.yMin, area.width, area.height);
            if (pageArtwork != null)
            {
                DrawArtwork(mesh, area, page);
                return;
            }
            Color shadow = ribbon;
            shadow.a = 0.16f;
            DrawRect(mesh, area, new Rect(page.x + 12f, page.y, page.width, page.height), shadow);
            DrawRect(mesh, area, page, color);
            float inset = area.height * 0.065f;
            DrawFrame(mesh, area, page, inset, 2f, trim);
            DrawFrame(mesh, area, page, inset + 6f, 0.8f, trim);
            float centerX = page.center.x;
            float centerY = page.center.y;
            // Burgundy chapter ribbons and small gold diamonds echo the existing menu panels.
            DrawRect(mesh, area, new Rect(centerX - area.width * 0.11f, centerY + 48f, area.width * 0.22f, 3f), ribbon);
            DrawRect(mesh, area, new Rect(centerX - area.width * 0.11f, centerY - 51f, area.width * 0.22f, 3f), ribbon);
            for (int i = -1; i <= 1; i++)
            {
                DrawDiamond(mesh, area, new Vector2(centerX + i * 22f, centerY + 77f), i == 0 ? 8f : 4f, trim);
                DrawDiamond(mesh, area, new Vector2(centerX + i * 22f, centerY - 77f), i == 0 ? 8f : 4f, trim);
            }
        }

        private void DrawArtwork(VertexHelper mesh, Rect clip, Rect page)
        {
            float left = Mathf.Max(clip.xMin, page.xMin);
            float right = Mathf.Min(clip.xMax, page.xMax);
            if (left >= right) return;
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(pageArtwork);
            float uLeft = Mathf.Lerp(uv.x, uv.z, (left - page.xMin) / page.width);
            float uRight = Mathf.Lerp(uv.x, uv.z, (right - page.xMin) / page.width);
            mesh.AddVert(new Vector2(left, page.yMin), Color.white, new Vector2(uLeft, uv.y));
            mesh.AddVert(new Vector2(left, page.yMax), Color.white, new Vector2(uLeft, uv.w));
            mesh.AddVert(new Vector2(right, page.yMax), Color.white, new Vector2(uRight, uv.w));
            mesh.AddVert(new Vector2(right, page.yMin), Color.white, new Vector2(uRight, uv.y));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
        }

        private static void DrawFrame(VertexHelper mesh, Rect clip, Rect page, float inset, float thickness, Color tint)
        {
            DrawRect(mesh, clip, new Rect(page.xMin + inset, page.yMin + inset, page.width - inset * 2f, thickness), tint);
            DrawRect(mesh, clip, new Rect(page.xMin + inset, page.yMax - inset - thickness, page.width - inset * 2f, thickness), tint);
            DrawRect(mesh, clip, new Rect(page.xMin + inset, page.yMin + inset, thickness, page.height - inset * 2f), tint);
            DrawRect(mesh, clip, new Rect(page.xMax - inset - thickness, page.yMin + inset, thickness, page.height - inset * 2f), tint);
        }

        private static void DrawRect(VertexHelper mesh, Rect clip, Rect shape, Color tint)
        {
            float left = Mathf.Max(clip.xMin, shape.xMin);
            float right = Mathf.Min(clip.xMax, shape.xMax);
            float bottom = Mathf.Max(clip.yMin, shape.yMin);
            float top = Mathf.Min(clip.yMax, shape.yMax);
            if (left >= right || bottom >= top) return;
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector2(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private static void DrawDiamond(VertexHelper mesh, Rect clip, Vector2 center, float radius, Color tint)
        {
            // Decorative details disappear at the edge instead of leaking outside the wall.
            if (center.x - radius < clip.xMin || center.x + radius > clip.xMax) return;
            int first = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * radius, tint, Vector2.zero);
            mesh.AddVert(center + Vector2.right * radius, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.up * radius, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.right * radius, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
