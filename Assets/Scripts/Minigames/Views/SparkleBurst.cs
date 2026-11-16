using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FoodIsekaiZ.Minigames.Views
{
    // How one burst of star sparkles looks. Positions and sizes are in the parent rect's units,
    // measured from its centre; times are in seconds.
    [Serializable]
    public sealed class SparkleBurstStyle
    {
        [SerializeField] private Color[] colours =
        {
            new Color(1f, 0.93f, 0.55f, 1f),
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 0.78f, 0.32f, 1f),
        };
        [SerializeField] private int count = 14;
        // Where the stars start, and the box around it they are scattered over.
        [SerializeField] private Vector2 origin = new Vector2(0f, -110f);
        [SerializeField] private Vector2 spread = new Vector2(300f, 160f);
        // Widest each star gets at the top of its pop.
        [SerializeField] private Vector2 peakSize = new Vector2(45f, 95f);
        // One larger star at the origin; 0 leaves it out.
        [SerializeField] private float flashSize = 190f;
        [SerializeField] private Vector2 speed = new Vector2(40f, 160f);
        [SerializeField] private Vector2 lifetime = new Vector2(0.45f, 0.8f);
        // Longest a star waits before it appears, so the burst ripples out.
        [SerializeField] private float stagger = 0.15f;
        // Share of its life a star spends growing to full size before it shrinks away.
        [SerializeField, Range(0.05f, 0.9f)] private float growShare = 0.2f;
        // Most degrees per second a star turns.
        [SerializeField] private float spin = 90f;

        public SparkleBurstStyle()
        {
        }

        // A burst made in code; the colours, the share of life spent growing and the spin keep their
        // defaults. Sizes, speeds and times are as described on the fields above.
        public SparkleBurstStyle(int count, Vector2 origin, Vector2 spread, Vector2 peakSize, float flashSize,
            Vector2 speed, Vector2 lifetime, float stagger)
        {
            this.count = count;
            this.origin = origin;
            this.spread = spread;
            this.peakSize = peakSize;
            this.flashSize = flashSize;
            this.speed = speed;
            this.lifetime = lifetime;
            this.stagger = stagger;
        }

        public IReadOnlyList<Color> Colours => colours;
        public int Count => count;
        public Vector2 Origin => origin;
        public Vector2 Spread => spread;
        public Vector2 PeakSize => peakSize;
        public float FlashSize => flashSize;
        public Vector2 Speed => speed;
        public Vector2 Lifetime => lifetime;
        public float Stagger => stagger;
        public float GrowShare => growShare;
        public float Spin => spin;
    }

    // Star sparkles drawn on top of a parent rect: each pops in large, then shrinks to nothing while
    // drifting outwards and turning. Stars are pooled and reused between bursts.
    public sealed class SparkleBurst
    {
        private readonly RectTransform parent;
        private readonly Sprite sprite;
        private readonly List<Star> stars = new List<Star>();

        private sealed class Star
        {
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Life;
            public float Peak;
            public float GrowShare;
            public Color Colour;

            public bool Alive => Age < Life;
        }

        public SparkleBurst(RectTransform parent, Sprite sprite)
        {
            this.parent = parent;
            this.sprite = sprite;
        }

        public void Play(SparkleBurstStyle style)
        {
            if (sprite == null) return;
            if (style.FlashSize > 0f) Launch(Free(), style, style.Origin, Vector2.zero, style.FlashSize, 0f);
            for (int i = 0; i < style.Count; i++)
            {
                Vector2 offset = new Vector2(Random.Range(-0.5f, 0.5f) * style.Spread.x, Random.Range(-0.5f, 0.5f) * style.Spread.y);
                Vector2 outwards = offset.sqrMagnitude > 1f ? offset.normalized : Random.insideUnitCircle.normalized;
                // Drift away from the middle and a little upwards.
                Vector2 velocity = (outwards + Vector2.up * 0.6f).normalized * Random.Range(style.Speed.x, style.Speed.y);
                float size = Random.Range(style.PeakSize.x, style.PeakSize.y);
                Launch(Free(), style, style.Origin + offset, velocity, size, Random.Range(0f, style.Stagger));
            }
        }

        // Moves every live star on by deltaTime seconds; finished stars are hidden.
        public void Tick(float deltaTime)
        {
            foreach (Star star in stars)
            {
                if (!star.Alive) continue;
                star.Age += deltaTime;
                if (!star.Alive)
                {
                    star.Image.enabled = false;
                    continue;
                }

                Draw(star, deltaTime);
            }
        }

        public void Clear()
        {
            foreach (Star star in stars)
            {
                star.Age = star.Life;
                star.Image.enabled = false;
            }
        }

        private static void Draw(Star star, float deltaTime)
        {
            // A star waiting out its stagger has a negative age and stays hidden.
            if (star.Age < 0f)
            {
                star.Image.enabled = false;
                return;
            }

            star.Image.enabled = true;
            RectTransform rect = star.Image.rectTransform;
            rect.anchoredPosition += star.Velocity * deltaTime;
            rect.localRotation *= Quaternion.Euler(0f, 0f, star.Spin * deltaTime);
            float share = star.Age / star.Life;
            float scale;
            if (share < star.GrowShare)
            {
                float grow = share / star.GrowShare;
                scale = 1f - (1f - grow) * (1f - grow);
            }
            else
            {
                float shrink = (share - star.GrowShare) / (1f - star.GrowShare);
                scale = 1f - shrink * shrink;
            }

            rect.sizeDelta = new Vector2(star.Peak, star.Peak) * scale;
            Color colour = star.Colour;
            colour.a *= Mathf.Clamp01((1f - share) * 3f);
            star.Image.color = colour;
        }

        private void Launch(Star star, SparkleBurstStyle style, Vector2 at, Vector2 velocity, float peak, float delay)
        {
            star.Velocity = velocity;
            star.Spin = Random.Range(-style.Spin, style.Spin);
            star.Life = Random.Range(style.Lifetime.x, style.Lifetime.y);
            star.Age = -delay;
            star.Peak = peak;
            star.GrowShare = style.GrowShare;
            star.Colour = style.Colours.Count > 0 ? style.Colours[Random.Range(0, style.Colours.Count)] : Color.white;

            RectTransform rect = star.Image.rectTransform;
            rect.anchoredPosition = at;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling();
            star.Image.enabled = false;
        }

        private Star Free()
        {
            foreach (Star star in stars)
            {
                if (!star.Alive) return star;
            }

            var made = new Star { Image = MakeImage() };
            stars.Add(made);
            return made;
        }

        private Image MakeImage()
        {
            var part = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            part.layer = parent.gameObject.layer;
            var rect = (RectTransform)part.transform;
            rect.SetParent(parent, false);
            var image = part.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }
    }
}
