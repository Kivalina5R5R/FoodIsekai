using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Builds one minigame view prefab: a root with the view component, a Wall child laid out on the
    // 1536 x 435 wall and a Floor child laid out on the 1100 x 500 floor canvas (100 units a metre,
    // up = towards the wall). Floor lies flat in front of the upright Wall, placed like the arena floor
    // sits in front of the SideDisplay in the scene, so the prefab reads like the real room in 3D;
    // the game moves it onto the floor at run time. Positions are from the centre of each part.
    public sealed class MinigamePrefabKit
    {
        public const string PrefabFolder = "Assets/Prefab/Minigame";
        public static readonly Vector2 WallSize = new Vector2(1536f, 435f);
        public static readonly Vector2 FloorSize = new Vector2(1100f, 500f);
        // Minigame art: one folder per game named like its prefab, plus Shared and Unused.
        public const string ArtFolder = "Assets/Art/Minigame";
        private const string FontName = "Poppins-Bold SDF";
        public const string WallGuideName = "Wall Guide (editor only)";
        public const string FloorGuideName = "Floor Guide (editor only)";
        public static readonly Color WallGuideColour = new Color(0.12f, 0.07f, 0.04f, 1f);
        public static readonly Color FloorGuideColour = new Color(0.16f, 0.16f, 0.17f, 1f);

        private readonly GameObject root;
        private readonly TMP_FontAsset font;

        private MinigamePrefabKit(GameObject root)
        {
            this.root = root;
            font = FindFont();
        }

        public RectTransform Wall { get; private set; }
        public RectTransform Floor { get; private set; }

        // Starts a prefab whose root carries TView, with empty Wall and Floor parts wired to it.
        public static MinigamePrefabKit Begin<TView>(out TView view) where TView : MinigameView
        {
            var rootObject = new GameObject(typeof(TView).Name, typeof(RectTransform));
            rootObject.layer = 5;
            var kit = new MinigamePrefabKit(rootObject);
            var rect = (RectTransform)rootObject.transform;
            rect.sizeDelta = WallSize;
            view = rootObject.AddComponent<TView>();
            kit.Wall = kit.Group("Wall", rect, Vector2.zero, WallSize);
            kit.Floor = kit.Group("Floor", rect, Vector2.zero, FloorSize);
            LayFloor(kit.Floor);
            AddGuide(kit.Wall, WallGuideName, WallGuideColour);
            AddGuide(kit.Floor, FloorGuideName, FloorGuideColour);
            kit.Bind(view, "wall", kit.Wall);
            kit.Bind(view, "floor", kit.Floor);
            return kit;
        }

        // A view with only a wall part (the HUD).
        public static MinigamePrefabKit BeginWallOnly<TView>(out TView view) where TView : MinigameView
        {
            MinigamePrefabKit kit = Begin(out view);
            Object.DestroyImmediate(kit.Floor.gameObject);
            kit.Floor = null;
            kit.Bind(view, "floor", null);
            return kit;
        }

        // Lays the floor part flat in front of the wall as in the scene: the SideDisplay is as wide as
        // the 11 m floor (its 1536 units span 11 m, the floor's 1100 units do too) and stands on the
        // floor's far edge. So the floor is scaled by 1536 / 1100, turned face up, and moved so its
        // far edge meets the wall's bottom edge.
        public static void LayFloor(RectTransform floor)
        {
            float scale = WallSize.x / FloorSize.x;
            floor.localScale = Vector3.one * scale;
            floor.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floor.anchoredPosition3D = new Vector3(0f, -WallSize.y * 0.5f, -FloorSize.y * 0.5f * scale);
        }

        public static string PathOf(string name)
        {
            return $"{PrefabFolder}/{name}.prefab";
        }

        // Writes the prefab to Assets/Prefab/Minigame/<name>.prefab and clears the scratch objects.
        public void Save(string name)
        {
            Directory.CreateDirectory(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PathOf(name));
            Object.DestroyImmediate(root);
        }

        // Sets a serialized field (private ones too) on a component in this prefab.
        public void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Minigames] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Sets a serialized number field.
        public void BindFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Minigames] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Sets a serialized colour field.
        public void BindColour(Object target, string field, Color value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Minigames] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The picture called name from any folder of Assets/Art/Minigame, or null.
        public static Sprite Picture(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return null;
        }

        // The picture when it exists, otherwise the fallback (often one of the plain shapes).
        public static Sprite PictureOr(string name, Sprite fallback)
        {
            Sprite picture = Picture(name);
            return picture != null ? picture : fallback;
        }

        // An empty RectTransform at position with size under parent.
        public RectTransform Group(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.layer = 5;
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // A group filling its parent.
        public RectTransform Fill(string name, RectTransform parent)
        {
            RectTransform rect = Group(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            return rect;
        }

        // An Image; a null sprite draws a plain rectangle. Pictures keep their aspect.
        public Image Shape(string name, RectTransform parent, Sprite sprite, Color colour, Vector2 position, Vector2 size)
        {
            RectTransform rect = Group(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        // A centred, bold, auto-sizing label; size is the largest font size, area the box it fits in.
        public TMP_Text Label(string name, RectTransform parent, string text, float size, Color colour, Vector2 position, Vector2 area)
        {
            RectTransform rect = Group(name, parent, position, area);
            var words = rect.gameObject.AddComponent<TextMeshProUGUI>();
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

        // Makes a template: showing in the prefab so it can be edited, switched off when the game
        // starts, and cloned by the view at run time.
        public static T Template<T>(T component) where T : Component
        {
            HideAtStart(component.gameObject);
            return component;
        }

        // Keeps a part showing in the prefab but switches it off when the game starts.
        public static void HideAtStart(GameObject part)
        {
            if (part.GetComponent<MinigameHiddenAtStart>() == null) part.AddComponent<MinigameHiddenAtStart>();
        }

        // A plain backdrop the size of part, behind everything in it, seen only while editing.
        public static void AddGuide(RectTransform part, string name, Color colour)
        {
            if (part.Find(name) != null) return;
            var guide = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(MinigameEditorGuide));
            guide.layer = part.gameObject.layer;
            var rect = (RectTransform)guide.transform;
            rect.SetParent(part, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var image = guide.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }

        // Pins a rect's left edge to its parent's left middle, so changing its width grows it rightwards.
        public static void PinLeft(RectTransform rect, float inset)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(inset, 0f);
        }

        private static TMP_FontAsset FindFont()
        {
            foreach (string guid in AssetDatabase.FindAssets($"\"{FontName}\" t:TMP_FontAsset"))
            {
                var found = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null && found.name == FontName) return found;
            }

            Debug.LogWarning($"[Minigames] Font {FontName} not found; labels use the TMP default");
            return null;
        }
    }
}
