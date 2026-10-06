using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Renders the wall through the open scene's camera with an NPC in every slot and the order cards shown as
    // one, two and three dishes in turn, so card sizes can be compared against the characters.
    // The request file may list layouts, one per line, as "NPC12-Fox,NPC03-Kwang,...;1,2,3,1,2,3;Temp/out.png";
    // an empty request renders the default line-up to Temp/OrderCardPreview.png.
    // Works on a copy in an isolated preview scene; the open scene is never edited or saved.
    [InitializeOnLoad]
    internal static class OrderCardPreview
    {
        private const string RequestPath = "Temp/OrderCardPreview.request";
        private const string OutputPath = "Temp/OrderCardPreview.png";
        private static readonly string[] NpcNames =
            { "NPC12-Fox", "NPC03-Kwang", "NPC02-Rabbit", "NPC05-Maow", "NPC11-Fairy-1", "NPC13-Orc-1" };
        // Dish counts shown in CustomerPanel1 through CustomerPanel6.
        private static readonly int[] DishCounts = { 1, 2, 3, 1, 2, 3 };

        private static readonly string[] SingleCard = { "BG Order", "Status", "OrderTimer" };
        private static readonly string[] PairCards = { "BG Order Pair First", "BG Order Pair Second" };
        private static readonly string[] TripleCards = { "BG Order Triple 1", "BG Order Triple 2", "BG Order Triple 3" };
        private static readonly string[] HiddenCards = { "Status Omakase", "BG Order Triple" };

        static OrderCardPreview() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            string[] lines = File.ReadAllLines(RequestPath);
            File.Delete(RequestPath);
            bool any = false;
            foreach (string line in lines)
            {
                string[] parts = line.Split(';');
                if (parts.Length != 3) continue;
                any = true;
                Render(parts[0].Split(','), System.Array.ConvertAll(parts[1].Split(','), int.Parse), parts[2].Trim());
            }
            if (!any) Capture();
        }

        [MenuItem("Food Isekai/Render Order Card Preview")]
        private static void Capture() => Render(NpcNames, DishCounts, OutputPath);

        private static void Render(string[] npcNames, int[] dishCounts, string outputPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Canvas source = null;
                Camera sourceCamera = null;
                foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                    if (canvas.name == "SideDisplay" && canvas.gameObject.scene.path == "Assets/Scenes/FoodIsekai.unity") source = canvas;
                foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
                    if (camera.name == "SideCamera" && camera.gameObject.scene.path == "Assets/Scenes/FoodIsekai.unity") sourceCamera = camera;
                if (source == null || sourceCamera == null) throw new InvalidOperationException("Open FoodIsekai before capturing.");

                GameObject copy = UnityEngine.Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, preview);
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && !(behaviour is Graphic)) behaviour.enabled = false;
                Canvas wall = copy.GetComponent<Canvas>();
                var cameraObject = new GameObject("Order Card Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera captureCamera = cameraObject.GetComponent<Camera>();
                captureCamera.CopyFrom(sourceCamera);
                captureCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                captureCamera.scene = preview;
                captureCamera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
                wall.worldCamera = captureCamera;

                Transform menu = copy.transform.Find("Menu");
                if (menu != null) menu.gameObject.SetActive(true);
                foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Wall Intro Overlay" || child.name == "NPC Guide Player") child.gameObject.SetActive(false);
                // NPCs dropped into the open scene while editing prefabs would otherwise appear behind the line-up.
                for (int i = copy.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = copy.transform.GetChild(i);
                    if (child.name.StartsWith("NPC") && child.name != "NPC Guide Player")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var spawner = copy.GetComponent<FoodIsekaiZNpcWaveSpawner>();
                typeof(FoodIsekaiZNpcWaveSpawner).GetField("sideCanvas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, wall);
                MethodInfo align = typeof(FoodIsekaiZNpcWaveSpawner).GetMethod("AlignNpcToDisplaySlot", BindingFlags.Instance | BindingFlags.NonPublic);
                int firstMenu = menu != null ? menu.GetSiblingIndex() : copy.transform.childCount;
                Sprite[] foods = LoadFoodSprites();
                for (int i = 0; i < npcNames.Length && i < 6; i++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/Charactor/{npcNames[i].Trim()}.prefab");
                    if (prefab == null) continue;
                    GameObject npc = UnityEngine.Object.Instantiate(prefab, copy.transform, false);
                    npc.transform.SetSiblingIndex(firstMenu + i);
                    align.Invoke(spawner, new object[] { npc, i });
                    Transform emoji = npc.transform.Find("Emoji");
                    if (emoji != null) emoji.gameObject.SetActive(false);
                    ShowCards(copy.transform, i, i < dishCounts.Length ? dishCounts[i] : 1, foods);
                }

                copy.SetActive(true);
                Canvas.ForceUpdateCanvases();
                target = RenderTexture.GetTemporary(1368, 361, 24, RenderTextureFormat.ARGB32);
                captureCamera.aspect = 1368f / 361f;
                RenderPipeline.SubmitRenderRequest(captureCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels = new Texture2D(1368, 361, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1368, 361), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                Debug.Log("[OrderCardPreview] Saved " + Path.GetFullPath(outputPath));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // Shows only the card set for the given dish count in this panel, with real food icons.
        private static void ShowCards(Transform wall, int index, int dishes, Sprite[] foods)
        {
            Transform panel = FindDeep(wall, $"CustomerPanel{index + 1}");
            if (panel == null) return;
            panel.gameObject.SetActive(true);
            foreach (CanvasGroup group in panel.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
            foreach (string name in HiddenCards) SetActive(panel, name, false);
            foreach (string name in SingleCard) SetActive(panel, name, dishes == 1);
            foreach (string name in PairCards) SetActive(panel, name, dishes == 2);
            foreach (string name in TripleCards) SetActive(panel, name, dishes == 3);
            // Panels without the three-card set fall back to the older single triple card.
            if (dishes == 3 && panel.Find(TripleCards[0]) == null) SetActive(panel, "BG Order Triple", true);

            int food = index;
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                image.enabled = true;
                if (image.name.StartsWith("Status") && foods.Length > 0) image.sprite = foods[food++ % foods.Length];
            }
        }

        private static Sprite[] LoadFoodSprites()
        {
            string[] names = { "F1-Meat", "F2-Seafood", "F3-Hors d'oeuvre", "F4-Dessert", "F5-Drink" };
            var sprites = new System.Collections.Generic.List<Sprite>();
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/Food/{name}.prefab");
                Image image = prefab != null ? prefab.GetComponentInChildren<Image>(true) : null;
                SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
                Sprite sprite = image != null ? image.sprite : renderer != null ? renderer.sprite : null;
                if (sprite != null) sprites.Add(sprite);
            }
            return sprites.ToArray();
        }

        private static void SetActive(Transform panel, string name, bool active)
        {
            Transform child = panel.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
