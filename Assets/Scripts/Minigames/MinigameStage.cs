using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // Where minigames draw: a layer over the wall (the side display, 1536 x 435 canvas units) and a
    // canvas lying on the LED floor, both built at run time and drawn over the game but under the
    // meal transition (sorting order 50), so the transitions still cover them.
    // Floor positions use the minigame design's arena metres: x 0 -> 11 from left to right and
    // z 0 -> 5 measured from the edge beside the wall (FoodIsekaiZArenaLayout is 11 x 5 m).
    // The floor canvas has 100 units per metre, so a 1 m circle is 100 units across.
    public sealed class MinigameStage
    {
        public const float UnitsPerMetre = 100f;
        private const int WallSortingOrder = 45;
        private const int FloorSortingOrder = 45;

        private readonly Rect arenaBounds;
        private readonly TMP_FontAsset font;
        private MinigameViewLibrary library;

        // The floor canvas is kept under floorParent (the scene's Map) so it sits with the rest of the arena.
        public MinigameStage(RectTransform wallCanvas, Camera floorCamera, Rect arenaBounds, int floorLayer, TMP_FontAsset font,
            Transform floorParent)
        {
            this.arenaBounds = arenaBounds;
            this.font = font;
            Wall = CreateWallLayer(wallCanvas);
            Floor = CreateFloorLayer(floorCamera, floorLayer);
            if (floorParent != null) Floor.SetParent(floorParent, true);
            SetVisible(false);
        }

        // The whole wall; children are placed in wall canvas units from its centre.
        public RectTransform Wall { get; }

        // The whole floor; children are placed with FloorPoint / FloorSize.
        public RectTransform Floor { get; }

        // Arena size in metres (11 x 5).
        public Vector2 ArenaSize => arenaBounds.size;

        public TMP_FontAsset Font => font;

        public void SetVisible(bool visible)
        {
            Wall.gameObject.SetActive(visible);
            Floor.gameObject.SetActive(visible);
        }

        // A world position on the floor as arena metres (x from the left, z from the wall).
        public Vector2 ToArena(Vector3 world)
        {
            return new Vector2(world.x - arenaBounds.xMin, arenaBounds.yMax - world.z);
        }

        // Arena metres as a position on the floor canvas.
        public Vector2 FloorPoint(Vector2 arena)
        {
            return new Vector2((arena.x - arenaBounds.width * 0.5f) * UnitsPerMetre,
                (arenaBounds.height * 0.5f - arena.y) * UnitsPerMetre);
        }

        // A position on the floor canvas as arena metres; the reverse of FloorPoint.
        public Vector2 ArenaPoint(Vector2 floorPoint)
        {
            return new Vector2(floorPoint.x / UnitsPerMetre + arenaBounds.width * 0.5f,
                arenaBounds.height * 0.5f - floorPoint.y / UnitsPerMetre);
        }

        public Vector2 FloorSize(Vector2 metres)
        {
            return metres * UnitsPerMetre;
        }

        private static RectTransform CreateWallLayer(RectTransform wallCanvas)
        {
            var layer = new GameObject("Minigame Wall", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            layer.layer = wallCanvas.gameObject.layer;
            var rect = (RectTransform)layer.transform;
            rect.SetParent(wallCanvas, false);
            MinigameArt.Stretch(rect);
            var canvas = layer.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = WallSortingOrder;
            var group = layer.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return rect;
        }

        private RectTransform CreateFloorLayer(Camera floorCamera, int floorLayer)
        {
            var layer = new GameObject("Minigame Floor", typeof(RectTransform), typeof(Canvas));
            layer.layer = floorLayer;
            var rect = (RectTransform)layer.transform;
            rect.position = new Vector3(arenaBounds.center.x, 0.06f, arenaBounds.center.y);
            rect.rotation = Quaternion.Euler(90f, 0f, 0f);
            rect.localScale = Vector3.one / UnitsPerMetre;
            rect.sizeDelta = arenaBounds.size * UnitsPerMetre;
            var canvas = layer.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = floorCamera;
            canvas.sortingOrder = FloorSortingOrder;
            return rect;
        }

        // A plain coloured shape (Image) under parent, centred at position with this size.
        public Image Shape(string name, RectTransform parent, Sprite sprite, Color colour, Vector2 position, Vector2 size)
        {
            var shape = new GameObject(name, typeof(RectTransform), typeof(Image));
            shape.layer = parent.gameObject.layer;
            var rect = (RectTransform)shape.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = shape.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = sprite != null && sprite != MinigameArt.Square;
            image.raycastTarget = false;
            return image;
        }

        // A centred text label under parent; size is the font size, area the box it wraps in.
        public TMP_Text Label(string name, RectTransform parent, string text, float size, Color colour, Vector2 position, Vector2 area)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = parent.gameObject.layer;
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = area;
            var words = label.GetComponent<TextMeshProUGUI>();
            if (font != null) words.font = font;
            words.text = text;
            words.fontSize = size;
            words.enableAutoSizing = true;
            words.fontSizeMin = size * 0.4f;
            words.fontSizeMax = size;
            words.color = colour;
            words.fontStyle = FontStyles.Bold;
            words.alignment = TextAlignmentOptions.Center;
            words.textWrappingMode = TextWrappingModes.Normal;
            words.raycastTarget = false;
            return words;
        }

        // Puts a copy of the view prefab named name (listed in Resources/MinigameViews, kept in
        // Assets/Prefab/Minigame) on the stage: its Wall under wallLayer and its Floor under
        // floorLayer, each on its layer's render layer. Null (with an error) when the prefab is
        // missing; build it with FoodIsekaiZ > Minigames > Build Missing Prefabs.
        public T LoadView<T>(string name, RectTransform wallLayer, RectTransform floorLayer) where T : MinigameView
        {
            if (library == null) library = Resources.Load<MinigameViewLibrary>(MinigameViewLibrary.ResourceName);
            var prefab = library != null ? library.Find(name) as T : null;
            if (prefab == null)
            {
                Debug.LogError($"[Minigames] No view prefab '{name}' with {typeof(T).Name} in Resources/{MinigameViewLibrary.ResourceName}; run FoodIsekaiZ > Minigames > Build Missing Prefabs");
                return null;
            }

            T view = Object.Instantiate(prefab, wallLayer, false);
            view.name = name;
            var root = (RectTransform)view.transform;
            MinigameArt.Stretch(root);
            SetLayer(view.transform, wallLayer.gameObject.layer);
            if (view.Floor != null)
            {
                view.Floor.SetParent(floorLayer, false);
                // In the prefab the floor lies in front of the wall like the room; on the floor canvas
                // it sits flat at the centre.
                view.Floor.anchoredPosition3D = Vector3.zero;
                view.Floor.localRotation = Quaternion.identity;
                view.Floor.localScale = Vector3.one;
                SetLayer(view.Floor, floorLayer.gameObject.layer);
            }

            view.Bind(this);
            return view;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }

        // An empty group under parent, filling it, so a minigame can drop all its drawings at once.
        public RectTransform Group(string name, RectTransform parent)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.layer = parent.gameObject.layer;
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            MinigameArt.Stretch(rect);
            return rect;
        }
    }
}
