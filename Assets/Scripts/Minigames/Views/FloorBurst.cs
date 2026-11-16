using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FoodIsekaiZ.Minigames.Views
{
    // The plain shapes a floor burst piece can be drawn as; all are made in code by MinigameArt.
    public enum FloorBurstShape
    {
        Square,
        Circle,
        Ring,
        Glow,
        Star,
        Hexagon,
    }

    // How one layer of a floor burst looks and moves. A full effect stacks a few layers, such as a
    // ring, some stars and confetti. Positions, speeds and sizes are in floor units (100 units = 1 m),
    // measured from the centre of the parent rect; times are in seconds.
    [Serializable]
    public sealed class FloorBurstStyle
    {
        [SerializeField] private string label = "Layer";
        [SerializeField] private FloorBurstShape shape = FloorBurstShape.Circle;
        [SerializeField] private Color[] colours = { Color.white };
        [SerializeField] private int count = 16;
        // Where the pieces start, and how far out from there along their flight direction.
        [SerializeField] private Vector2 origin;
        [SerializeField] private float startRadius = 12f;
        [SerializeField] private Vector2 speed = new Vector2(180f, 380f);
        // Pull applied every second along the floor's up axis; negative pulls towards the player's back.
        [SerializeField] private float gravity;
        // Share of speed kept after one second; lower stops the pieces sooner.
        [SerializeField, Range(0.01f, 1f)] private float drag = 0.15f;
        // Width of a piece at scale 1; its height is width times aspect.
        [SerializeField] private Vector2 size = new Vector2(10f, 18f);
        [SerializeField] private float aspect = 1f;
        [SerializeField] private Vector2 lifetime = new Vector2(0.6f, 1f);
        // Wait before each piece appears, picked between the two values, so a burst can ripple out.
        [SerializeField] private Vector2 delay;
        // Scale over a piece's life: it eases from startScale up to peakScale by peakShare of its
        // life, then eases down to endScale. A peakShare of 1 only grows; 0 only shrinks.
        [SerializeField] private float startScale = 1f;
        [SerializeField] private float peakScale = 1f;
        [SerializeField, Range(0f, 1f)] private float peakShare;
        [SerializeField] private float endScale;
        // Share of its life after which a piece fades out; 0 fades all the way.
        [SerializeField, Range(0f, 0.99f)] private float fadeFrom = 0.6f;
        // Most degrees per second a piece spins.
        [SerializeField] private float spin = 540f;
        // Where pieces are flung: an arc centred on this angle (degrees, 90 = towards the wall).
        [SerializeField] private float aimAngle = 90f;
        [SerializeField, Range(0f, 360f)] private float spreadAngle = 360f;
        // Draws this layer under the result badge instead of on top of it.
        [SerializeField] private bool behind;

        public string Label => label;
        public FloorBurstShape Shape => shape;
        public IReadOnlyList<Color> Colours => colours;
        public int Count => count;
        public Vector2 Origin => origin;
        public float StartRadius => startRadius;
        public Vector2 Speed => speed;
        public float Gravity => gravity;
        public float Drag => drag;
        public Vector2 Size => size;
        public float Aspect => aspect;
        public Vector2 Lifetime => lifetime;
        public Vector2 Delay => delay;
        public float StartScale => startScale;
        public float PeakScale => peakScale;
        public float PeakShare => peakShare;
        public float EndScale => endScale;
        public float FadeFrom => fadeFrom;
        public float Spin => spin;
        public float AimAngle => aimAngle;
        public float SpreadAngle => spreadAngle;
        public bool Behind => behind;

        public FloorBurstStyle()
        {
        }

        // Every setting at once; call it with named arguments so each value reads clearly.
        public FloorBurstStyle(string label, FloorBurstShape shape, Color[] colours, int count, Vector2 origin,
            float startRadius, Vector2 speed, float gravity, float drag, Vector2 size, float aspect, Vector2 lifetime,
            Vector2 delay, float startScale, float peakScale, float peakShare, float endScale, float fadeFrom,
            float spin, float aimAngle, float spreadAngle, bool behind)
        {
            this.label = label;
            this.shape = shape;
            this.colours = colours;
            this.count = count;
            this.origin = origin;
            this.startRadius = startRadius;
            this.speed = speed;
            this.gravity = gravity;
            this.drag = drag;
            this.size = size;
            this.aspect = aspect;
            this.lifetime = lifetime;
            this.delay = delay;
            this.startScale = startScale;
            this.peakScale = peakScale;
            this.peakShare = peakShare;
            this.endScale = endScale;
            this.fadeFrom = fadeFrom;
            this.spin = spin;
            this.aimAngle = aimAngle;
            this.spreadAngle = spreadAngle;
            this.behind = behind;
        }
    }

    // UI particles on the floor canvas: pieces flung out from a point in a parent rect, spinning,
    // slowing, growing or shrinking and fading as each layer's style says. Pieces are pooled and
    // reused between bursts.
    public sealed class FloorBurst
    {
        private readonly RectTransform parent;
        private readonly List<Piece> pieces = new List<Piece>();

        private sealed class Piece
        {
            public Image Image;
            public FloorBurstStyle Style;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Life;
            public float Size;
            public Color Colour;

            public bool Alive => Age < Life;
        }

        public FloorBurst(RectTransform parent)
        {
            this.parent = parent;
        }

        public void Play(IReadOnlyList<FloorBurstStyle> layers)
        {
            foreach (FloorBurstStyle layer in layers)
            {
                if (layer != null) Play(layer);
            }
        }

        public void Play(FloorBurstStyle style)
        {
            for (int i = 0; i < style.Count; i++) Launch(Free(), style);
        }

        // Moves every live piece on by deltaTime seconds; dead pieces are hidden.
        public void Tick(float deltaTime)
        {
            foreach (Piece piece in pieces)
            {
                if (!piece.Alive) continue;
                piece.Age += deltaTime;
                if (!piece.Alive)
                {
                    piece.Image.enabled = false;
                    continue;
                }

                Move(piece, deltaTime);
            }
        }

        public void Clear()
        {
            foreach (Piece piece in pieces)
            {
                piece.Age = piece.Life;
                piece.Image.enabled = false;
            }
        }

        private static void Move(Piece piece, float deltaTime)
        {
            // A piece still waiting out its delay has a negative age and stays hidden.
            if (piece.Age < 0f)
            {
                piece.Image.enabled = false;
                return;
            }

            FloorBurstStyle style = piece.Style;
            piece.Image.enabled = true;
            piece.Velocity *= Mathf.Pow(style.Drag, deltaTime);
            piece.Velocity.y += style.Gravity * deltaTime;
            RectTransform rect = piece.Image.rectTransform;
            rect.anchoredPosition += piece.Velocity * deltaTime;
            rect.localRotation *= Quaternion.Euler(0f, 0f, piece.Spin * deltaTime);

            float share = piece.Age / piece.Life;
            float scale = Scale(style, share);
            rect.sizeDelta = new Vector2(piece.Size, piece.Size * style.Aspect) * scale;
            Color colour = piece.Colour;
            float fade = Mathf.Clamp01((1f - share) / (1f - style.FadeFrom));
            colour.a *= fade * fade * (3f - 2f * fade);
            piece.Image.color = colour;
        }

        // Eases out from the start scale to the peak, then eases in down to the end scale.
        private static float Scale(FloorBurstStyle style, float share)
        {
            if (share < style.PeakShare)
            {
                float grow = share / style.PeakShare;
                return Mathf.Lerp(style.StartScale, style.PeakScale, 1f - (1f - grow) * (1f - grow));
            }

            if (style.PeakShare >= 1f) return style.PeakScale;
            float shrink = (share - style.PeakShare) / (1f - style.PeakShare);
            return Mathf.Lerp(style.PeakScale, style.EndScale, shrink * shrink);
        }

        private void Launch(Piece piece, FloorBurstStyle style)
        {
            float angle = (style.AimAngle + Random.Range(-0.5f, 0.5f) * style.SpreadAngle) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            piece.Style = style;
            piece.Velocity = direction * Random.Range(style.Speed.x, style.Speed.y);
            piece.Spin = Random.Range(-style.Spin, style.Spin);
            piece.Age = -Random.Range(style.Delay.x, style.Delay.y);
            piece.Life = Mathf.Max(0.01f, Random.Range(style.Lifetime.x, style.Lifetime.y));
            piece.Size = Random.Range(style.Size.x, style.Size.y);
            piece.Colour = style.Colours.Count > 0 ? style.Colours[Random.Range(0, style.Colours.Count)] : Color.white;

            piece.Image.sprite = SpriteFor(style.Shape);
            RectTransform rect = piece.Image.rectTransform;
            rect.anchoredPosition = style.Origin + direction * style.StartRadius;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            if (style.Behind) rect.SetAsFirstSibling();
            else rect.SetAsLastSibling();
            Move(piece, 0f);
        }

        private static Sprite SpriteFor(FloorBurstShape shape)
        {
            switch (shape)
            {
                case FloorBurstShape.Circle: return MinigameArt.Circle;
                case FloorBurstShape.Ring: return MinigameArt.Ring;
                case FloorBurstShape.Glow: return MinigameArt.Glow;
                case FloorBurstShape.Star: return MinigameArt.Star;
                case FloorBurstShape.Hexagon: return MinigameArt.Hexagon;
                default: return MinigameArt.Square;
            }
        }

        private Piece Free()
        {
            foreach (Piece piece in pieces)
            {
                if (!piece.Alive) return piece;
            }

            var made = new Piece { Image = MakeImage() };
            pieces.Add(made);
            return made;
        }

        private Image MakeImage()
        {
            var part = new GameObject("Burst Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            part.layer = parent.gameObject.layer;
            var rect = (RectTransform)part.transform;
            rect.SetParent(parent, false);
            var image = part.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }
    }
}
