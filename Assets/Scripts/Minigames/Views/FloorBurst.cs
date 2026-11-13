using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace FoodIsekaiZ.Minigames.Views
{
    // How one burst of floor pieces looks and moves. Speeds and sizes are in floor units
    // (100 units = 1 m); times are in seconds.
    [Serializable]
    public sealed class FloorBurstStyle
    {
        [SerializeField] private Color[] colours = { Color.white };
        [SerializeField] private int count = 16;
        [SerializeField] private Vector2 speed = new Vector2(180f, 380f);
        // Pull applied every second along the floor's up axis; negative pulls towards the player's back.
        [SerializeField] private float gravity;
        // Share of speed kept after one second; lower stops the pieces sooner.
        [SerializeField, Range(0.01f, 1f)] private float drag = 0.15f;
        [SerializeField] private Vector2 size = new Vector2(10f, 18f);
        [SerializeField] private Vector2 lifetime = new Vector2(0.6f, 1f);
        // Most degrees per second a piece spins.
        [SerializeField] private float spin = 540f;
        // Where pieces are flung: an arc centred on this angle (degrees, 90 = towards the wall).
        [SerializeField] private float aimAngle = 90f;
        [SerializeField, Range(0f, 360f)] private float spreadAngle = 360f;

        public IReadOnlyList<Color> Colours => colours;
        public int Count => count;
        public Vector2 Speed => speed;
        public float Gravity => gravity;
        public float Drag => drag;
        public Vector2 Size => size;
        public Vector2 Lifetime => lifetime;
        public float Spin => spin;
        public float AimAngle => aimAngle;
        public float SpreadAngle => spreadAngle;

        public FloorBurstStyle()
        {
        }

        public FloorBurstStyle(Color[] colours, int count, Vector2 speed, float gravity, float drag, Vector2 size,
            Vector2 lifetime, float spin, float aimAngle, float spreadAngle)
        {
            this.colours = colours;
            this.count = count;
            this.speed = speed;
            this.gravity = gravity;
            this.drag = drag;
            this.size = size;
            this.lifetime = lifetime;
            this.spin = spin;
            this.aimAngle = aimAngle;
            this.spreadAngle = spreadAngle;
        }
    }

    // UI particles on the floor canvas: small square pieces flung out from the centre of a parent
    // rect, spinning, slowing, shrinking and fading. Pieces are pooled and reused between bursts.
    public sealed class FloorBurst
    {
        private readonly RectTransform parent;
        private readonly List<Piece> pieces = new List<Piece>();

        private sealed class Piece
        {
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Life;
            public float Size;
            public float Drag;
            public float Gravity;
            public Color Colour;

            public bool Alive => Age < Life;
        }

        public FloorBurst(RectTransform parent)
        {
            this.parent = parent;
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
            piece.Velocity *= Mathf.Pow(piece.Drag, deltaTime);
            piece.Velocity.y += piece.Gravity * deltaTime;
            RectTransform rect = piece.Image.rectTransform;
            rect.anchoredPosition += piece.Velocity * deltaTime;
            rect.localRotation *= Quaternion.Euler(0f, 0f, piece.Spin * deltaTime);
            float share = piece.Age / piece.Life;
            float scale = 1f - share * share;
            rect.sizeDelta = new Vector2(piece.Size, piece.Size * 0.6f) * scale;
            Color colour = piece.Colour;
            colour.a *= Mathf.Clamp01((1f - share) * 2.5f);
            piece.Image.color = colour;
        }

        private void Launch(Piece piece, FloorBurstStyle style)
        {
            float angle = (style.AimAngle + Random.Range(-0.5f, 0.5f) * style.SpreadAngle) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            piece.Velocity = direction * Random.Range(style.Speed.x, style.Speed.y);
            piece.Spin = Random.Range(-style.Spin, style.Spin);
            piece.Age = 0f;
            piece.Life = Random.Range(style.Lifetime.x, style.Lifetime.y);
            piece.Size = Random.Range(style.Size.x, style.Size.y);
            piece.Colour = style.Colours.Count > 0 ? style.Colours[Random.Range(0, style.Colours.Count)] : Color.white;
            piece.Drag = style.Drag;
            piece.Gravity = style.Gravity;

            RectTransform rect = piece.Image.rectTransform;
            rect.anchoredPosition = direction * 12f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            rect.SetAsLastSibling();
            piece.Image.enabled = true;
            Move(piece, 0f);
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
