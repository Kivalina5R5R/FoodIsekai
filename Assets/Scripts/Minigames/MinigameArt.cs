using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // Shapes and colours the minigames draw with. The plain shapes are made in code once; pictures
    // come from the minigame art library (Resources/MinigameArt, listing Assets/Art/Minigame) and fall
    // back to a plain shape until the art is added.
    public static class MinigameArt
    {

        private static Sprite square;
        private static Sprite circle;
        private static Sprite ring;
        private static Sprite hexagon;
        private static Sprite pill;
        private static Sprite chevron;
        private static Sprite glow;
        private static Sprite star;
        private static readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();
        private static MinigameArtLibrary library;

        // The game's warm tavern palette.
        public static readonly Color Cream = new Color(0.99f, 0.91f, 0.78f, 1f);
        public static readonly Color Gold = new Color(0.88f, 0.72f, 0.47f, 1f);
        public static readonly Color Copper = new Color(0.78f, 0.45f, 0.2f, 1f);
        public static readonly Color Brown = new Color(0.36f, 0.2f, 0.11f, 1f);
        public static readonly Color WallShade = new Color(0.1f, 0.05f, 0.02f, 0.82f);
        public static readonly Color Good = new Color(0.36f, 0.78f, 0.36f, 1f);
        public static readonly Color Bad = new Color(0.9f, 0.27f, 0.2f, 1f);

        // Words on the game's cream panels, as on the end-of-game results: amber-brown text, a deep red
        // for misses (the results' TIME'S UP) and a deep green for passes.
        public static readonly Color PanelInk = new Color(0.6f, 0.34f, 0f, 1f);
        public static readonly Color PanelRed = new Color(0.59f, 0.12f, 0.15f, 1f);
        public static readonly Color PanelGreen = new Color(0.24f, 0.52f, 0.2f, 1f);

        // Player colours: P1 cyan, P2 magenta, P3 yellow, P4 green, P5 orange, P6 violet.
        private static readonly Color[] PlayerColours =
        {
            new Color(0.2f, 0.85f, 0.95f, 1f),
            new Color(0.95f, 0.3f, 0.8f, 1f),
            new Color(1f, 0.88f, 0.2f, 1f),
            new Color(0.4f, 0.9f, 0.35f, 1f),
            new Color(1f, 0.55f, 0.15f, 1f),
            new Color(0.62f, 0.45f, 1f, 1f),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pictures.Clear();
            library = null;
        }

        public static Color PlayerColour(int playerId)
        {
            return PlayerColours[Mathf.Abs(playerId - 1) % PlayerColours.Length];
        }

        public static Sprite Square => square != null ? square : square = Make(4, (x, y) => 1f);

        public static Sprite Circle => circle != null ? circle : circle = Make(128, (x, y) =>
            Edge(1f - Mathf.Sqrt(x * x + y * y), 128));

        public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            return Mathf.Min(Edge(1f - distance, 128), Edge(distance - 0.86f, 128));
        });

        // Pointy-top hexagon filling the square from top to bottom.
        public static Sprite Hexagon => hexagon != null ? hexagon : hexagon = Make(128, (x, y) =>
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float inside = Mathf.Min(0.866f - ax, 1f - (ay + ax * 0.57735f));
            return Edge(inside, 128);
        });

        // A capsule that 9-slices to any width: round ends, flat middle.
        public static Sprite Pill => pill != null ? pill : pill = Make(64, (x, y) =>
            Edge(1f - Mathf.Sqrt(x * x + y * y), 64), new Vector4(31f, 31f, 31f, 31f));

        // A ">" chevron pointing right, for floor step hints.
        public static Sprite Chevron => chevron != null ? chevron : chevron = Make(128, (x, y) =>
        {
            float bend = x + 0.9f * Mathf.Abs(y);
            float inside = Mathf.Min(Mathf.Min(bend + 0.3f, 0.3f - bend), 0.9f - Mathf.Abs(y));
            return Edge(inside, 128);
        });

        // A soft round glow: full in the middle, fading smoothly to nothing at the rim.
        public static Sprite Glow => glow != null ? glow : glow = Make(128, (x, y) =>
        {
            float fade = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
            return fade * fade;
        });

        // A four-pointed sparkle star with curved, pinched sides and a soft halo at its heart.
        public static Sprite Star => star != null ? star : star = Make(128, (x, y) =>
        {
            float inside = 1f - (Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)));
            float halo = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.4f);
            return Mathf.Max(Edge(inside, 128), halo * halo);
        });

        // The minigame picture called name (its file name in Assets/Art/Minigame), or null when it has
        // not been added yet.
        public static Sprite Picture(string name)
        {
            if (pictures.TryGetValue(name, out Sprite found)) return found;
            if (library == null) library = Resources.Load<MinigameArtLibrary>(MinigameArtLibrary.ResourceName);
            found = library != null ? library.Find(name) : null;
            pictures[name] = found;
            return found;
        }

        // A font asset the scene has already loaded, by name; null when it is not loaded.
        public static TMP_FontAsset FindFont(string fontName)
        {
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font != null && font.name == fontName) return font;
            }

            return null;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Shows picture on image when it exists, otherwise keeps the image's plain shape.
        public static void UsePicture(Image image, string name)
        {
            Sprite picture = Picture(name);
            if (picture == null) return;
            image.sprite = picture;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        // A tint for a picture painted in baseColour, so it looks like colour: white while colour is
        // baseColour, then each channel scaled by colour / baseColour (cooking food darkens this way).
        public static Color RelativeTint(Color colour, Color baseColour)
        {
            return new Color(
                Mathf.Clamp01(colour.r / Mathf.Max(0.01f, baseColour.r)),
                Mathf.Clamp01(colour.g / Mathf.Max(0.01f, baseColour.g)),
                Mathf.Clamp01(colour.b / Mathf.Max(0.01f, baseColour.b)),
                colour.a);
        }

        private static float Edge(float inside, int size)
        {
            return Mathf.Clamp01(inside * size * 0.5f);
        }

        // A white sprite whose alpha is coverage(x, y), with x and y running -1 -> 1 across it.
        private static Sprite Make(int size, System.Func<float, float, float> coverage)
        {
            return Make(size, coverage, Vector4.zero);
        }

        // As above, with a 9-slice border in pixels (left, bottom, right, top).
        private static Sprite Make(int size, System.Func<float, float, float> coverage, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage(u, v) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
