using System.Collections;
using FoodIsekaiZ.Audio;
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
        [Header("Phase Artwork")]
        // Each phase has its own artwork object in the prefab, so each can be dragged and sized on its own.
        // Only the one for the announced phase is shown; they slide with the page over a plain fill.
        // The meal artwork covers breakfast, lunch and dinner, and stands in when break or results art is empty.
        [SerializeField] private RectTransform mealArtwork;
        [SerializeField] private RectTransform breakArtwork;
        [SerializeField] private RectTransform resultsArtwork;
        [SerializeField] private RectTransform banner;
        // Other page children, such as the particle layers, that slide with the page without extra motion.
        [SerializeField] private RectTransform[] slidingExtras = new RectTransform[0];

        [Header("Artwork Motion")]
        // The artwork pops in with a little overshoot, then bobs and breathes while the title is held,
        // and swells slightly as the page leaves.
        [SerializeField, Min(0.05f)] private float artworkPopSeconds = 0.55f;
        [SerializeField, Range(0.3f, 1f)] private float artworkStartScale = 0.82f;

        [Header("Title Banner Motion")]
        // Once the page has arrived, the banner unrolls sideways while fading in, then the title
        // drifts up into it and fades in. The banner then floats gently with the artwork.
        [SerializeField, Min(0f)] private float bannerDelaySeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float bannerUnfoldSeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float bannerStartWidth = 0.15f;
        [SerializeField, Min(0f)] private float titleDelaySeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float titleFadeSeconds = 0.45f;
        [SerializeField, Min(0f)] private float titleRise = 8f;
        // The title appears oversized and bounces down to its normal size.
        [SerializeField, Range(1f, 2f)] private float titleStartScale = 1.4f;
        [SerializeField, Min(0.05f)] private float titlePopSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float bannerFloatShare = 0.5f;
        [SerializeField, Min(0f)] private float idleBob = 3f;
        [SerializeField, Min(0.5f)] private float idleCycleSeconds = 2.6f;
        [SerializeField, Range(0f, 0.05f)] private float idleBreathe = 0.015f;
        [SerializeField, Range(0f, 0.2f)] private float exitGrow = 0.06f;
        [SerializeField] private Color trim = new Color(0.77f, 0.57f, 0.3f, 1f);
        [SerializeField] private Color ribbon = new Color(0.46f, 0.195f, 0.17f, 1f);
        [SerializeField, Min(0.05f)] private float coverSeconds = 0.4f;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.5f;
        // How long the page rests at the center with its title before sliding away.
        // Time spent changing the scene behind the page counts toward this hold.
        [SerializeField, Min(0f)] private float titleHoldSeconds = 2f;
        private float offset = -1f;
        private float coveredAt;
        private bool visible;
        private Vector2 headingRestPosition;
        private bool headingPositionCached;
        private RectTransform[] slidingDetails = new RectTransform[0];
        private Vector2[] slidingRestPositions = new Vector2[0];
        private Vector3[] slidingRestScales = new Vector3[0];
        private Vector3 headingRestScale = Vector3.one;
        private RectTransform currentArtwork;
        private float coverStartedAt;
        private Graphic bannerGraphic;
        private float revealStartedAt = -1f;
        private bool UsesPhaseArtwork => mealArtwork != null;
        public bool IsCovered => visible && Mathf.Approximately(offset, 0f);
        // Includes the cover, title hold, and reveal until the page is fully hidden.
        public bool IsVisible => visible && gameObject.activeInHierarchy;
        // A completed floor entrance can flow straight into food animation during the page reveal.
        public bool RevealFoodImmediately { get; private set; }
        // Raised while the page still covers the scene, before every phase reveal.
        public event System.Action RevealStarting;
        public override Texture mainTexture => pageArtwork != null ? pageArtwork.texture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying && !headingPositionCached)
            {
                if (heading != null)
                {
                    headingRestPosition = heading.rectTransform.anchoredPosition;
                    headingRestScale = heading.rectTransform.localScale;
                }
                var details = new System.Collections.Generic.List<RectTransform> { mealArtwork, breakArtwork, resultsArtwork, banner };
                if (slidingExtras != null) details.AddRange(slidingExtras);
                slidingDetails = details.ToArray();
                slidingRestPositions = new Vector2[slidingDetails.Length];
                slidingRestScales = new Vector3[slidingDetails.Length];
                for (int i = 0; i < slidingDetails.Length; i++)
                {
                    if (slidingDetails[i] == null) continue;
                    slidingRestPositions[i] = slidingDetails[i].anchoredPosition;
                    slidingRestScales[i] = slidingDetails[i].localScale;
                }
                if (banner != null) bannerGraphic = banner.GetComponent<Graphic>();
                headingPositionCached = true;
            }
            if (Application.isPlaying)
            {
                visible = false;
                offset = -1f;
                SetPageDetailsVisible(false);
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
            // The music dips while the page covers the scene change, instead of carrying on at full level.
            FoodIsekaiZBgmPlayer.SetSceneTransition(true);
            ApplyArtwork(title);
            title = ResolveTitle(title);
            if (IsCovered)
            {
                // A new phase announced while the page is already up swaps to its artwork in place.
                if (heading != null) heading.text = title;
                SetPageDetailsVisible(true);
                yield break;
            }
            if (heading != null) heading.text = title;
            SetPageDetailsVisible(true);
            if (!visible)
            {
                offset = -1f;
                coverStartedAt = Time.unscaledTime;
            }
            revealStartedAt = -1f;
            visible = true;
            MoveHeadingWithPage();
            yield return Slide(0f, coverSeconds);
            coveredAt = Time.unscaledTime;
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

        // Picks the artwork object for the phase being announced, using the untranslated phase key.
        private void ApplyArtwork(string phase)
        {
            RectTransform artwork = phase == "SERVICE BREAK" ? breakArtwork :
                phase == "SERVICE RESULTS" ? resultsArtwork : mealArtwork;
            currentArtwork = artwork != null ? artwork : mealArtwork;
        }

        private void SetPageDetailsVisible(bool show)
        {
            if (heading != null) heading.gameObject.SetActive(show);
            if (banner != null) banner.gameObject.SetActive(show);
            foreach (RectTransform artwork in new[] { mealArtwork, breakArtwork, resultsArtwork })
                if (artwork != null) artwork.gameObject.SetActive(show && artwork == currentArtwork);
            if (slidingExtras != null)
                foreach (RectTransform extra in slidingExtras)
                    if (extra != null) extra.gameObject.SetActive(show);
        }

        // Continue from a floor slide without adding another title hold or delaying the food reveal.
        public IEnumerator Reveal(bool continueFromFloorSlide = false)
        {
            RevealFoodImmediately = continueFromFloorSlide;
            RevealStarting?.Invoke();
            // The music rises back as the page lifts on the new scene.
            FoodIsekaiZBgmPlayer.SetSceneTransition(false);
            // The page stays centered until the title has been readable for the full hold,
            // including a floor slide that already ran behind it.
            while (Time.unscaledTime < coveredAt + titleHoldSeconds) yield return null;
            revealStartedAt = Time.unscaledTime;
            yield return Slide(1f, revealSeconds);
            Hide();
        }

        public void Hide()
        {
            FoodIsekaiZBgmPlayer.SetSceneTransition(false);
            RevealFoodImmediately = false;
            visible = false;
            offset = -1f;
            SetPageDetailsVisible(false);
            revealStartedAt = -1f;
            if (headingPositionCached)
            {
                if (heading != null)
                {
                    heading.rectTransform.anchoredPosition = headingRestPosition;
                    heading.rectTransform.localScale = headingRestScale;
                    heading.canvasRenderer.SetAlpha(1f);
                }
                if (bannerGraphic != null) bannerGraphic.canvasRenderer.SetAlpha(1f);
                for (int i = 0; i < slidingDetails.Length; i++)
                {
                    if (slidingDetails[i] == null) continue;
                    slidingDetails[i].anchoredPosition = slidingRestPositions[i];
                    slidingDetails[i].localScale = slidingRestScales[i];
                }
            }
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

        // Keeps the idle bob and breathing running between the slide-in and the slide-out.
        private void LateUpdate()
        {
            if (Application.isPlaying && visible) MoveHeadingWithPage();
        }

        // The title, artwork, banner and particles travel with the page so they enter and leave together,
        // with the artwork and banner motion layered on top of the slide.
        private void MoveHeadingWithPage()
        {
            // Edit mode never caches rest positions, so dragging the objects in the prefab is left alone.
            if (!headingPositionCached) return;
            Vector2 shift = Vector2.right * (offset * (rectTransform.rect.width + 24f));
            float sinceCover = Time.unscaledTime - coverStartedAt;

            float artworkScale = Mathf.LerpUnclamped(artworkStartScale, 1f, EaseOutBack(sinceCover / artworkPopSeconds));
            // The idle motion eases in once the pop has settled, so the two never fight.
            float idle = Mathf.Clamp01((sinceCover - artworkPopSeconds) / 0.4f);
            float wave = Mathf.Sin(sinceCover / idleCycleSeconds * Mathf.PI * 2f);
            artworkScale *= 1f + wave * idleBreathe * idle;
            if (revealStartedAt >= 0f)
                artworkScale *= 1f + exitGrow * Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - revealStartedAt) / revealSeconds);
            Vector2 bob = Vector2.up * (wave * idleBob * idle);

            // The banner unrolls from a narrow strip to full width with a smooth slow-down, no overshoot.
            float unfold = EaseOutCubic((sinceCover - bannerDelaySeconds) / bannerUnfoldSeconds);
            Vector3 bannerScale = new Vector3(Mathf.Lerp(bannerStartWidth, 1f, unfold), Mathf.Lerp(0.8f, 1f, unfold), 1f);
            if (bannerGraphic != null)
                bannerGraphic.canvasRenderer.SetAlpha(Mathf.Clamp01((sinceCover - bannerDelaySeconds) / (bannerUnfoldSeconds * 0.4f)));
            Vector2 bannerBob = bob * bannerFloatShare;

            // The title waits for the banner to open, then rises into place while fading in,
            // starting large and bouncing just past its normal size before it settles.
            float titleTime = sinceCover - bannerDelaySeconds - titleDelaySeconds;
            float titleIn = EaseOutCubic(titleTime / titleFadeSeconds);
            float titleScale = Mathf.LerpUnclamped(titleStartScale, 1f, EaseOutBack(titleTime / titlePopSeconds));
            if (heading != null)
            {
                heading.rectTransform.anchoredPosition = headingRestPosition + shift + bannerBob +
                    Vector2.down * ((1f - titleIn) * titleRise);
                heading.rectTransform.localScale = headingRestScale * titleScale;
                heading.canvasRenderer.SetAlpha(titleIn);
            }
            for (int i = 0; i < slidingDetails.Length; i++)
            {
                RectTransform detail = slidingDetails[i];
                if (detail == null) continue;
                bool isArtwork = detail == mealArtwork || detail == breakArtwork || detail == resultsArtwork;
                bool isBanner = detail == banner;
                Vector2 motion = isArtwork ? bob : isBanner ? bannerBob : Vector2.zero;
                detail.anchoredPosition = slidingRestPositions[i] + shift + motion;
                Vector3 scale = isArtwork ? Vector3.one * artworkScale : isBanner ? bannerScale : Vector3.one;
                detail.localScale = Vector3.Scale(slidingRestScales[i], scale);
            }
        }

        private static float EaseOutCubic(float progress)
        {
            float inverse = 1f - Mathf.Clamp01(progress);
            return 1f - inverse * inverse * inverse;
        }

        // Rises a little past the target before settling, which reads as a soft pop.
        private static float EaseOutBack(float progress)
        {
            float t = Mathf.Clamp01(progress) - 1f;
            const float overshoot = 1.6f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
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
            // With phase artwork, the page is a plain fill behind the artwork and banner children.
            if (UsesPhaseArtwork)
            {
                DrawRect(mesh, area, page, color);
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
