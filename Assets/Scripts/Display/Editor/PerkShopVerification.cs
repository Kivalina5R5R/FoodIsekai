using System;
using System.IO;
using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display.Editor
{
    // Renders the saved layout in an isolated preview scene; never saves or reloads the user's scene.
    public static class PerkShopVerification
    {
        [MenuItem("Food Isekai/Verify Perk Shop Layout")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene preview = default;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/FoodIsekai.unity");
                PerkShopPresentation shop = null;
                Camera camera = null;
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    foreach (PerkShopPresentation item in root.GetComponentsInChildren<PerkShopPresentation>(true)) shop = item;
                    foreach (Camera item in root.GetComponentsInChildren<Camera>(true))
                        if (item.name == "SideCamera") camera = item;
                }
                if (shop == null || camera == null) throw new InvalidOperationException("Shop or wall camera missing.");
                var settings = new SerializedObject(shop);
                Canvas canvas = shop.GetComponentInParent<Canvas>();
                Transform background = canvas.transform.Find("Background");
                Transform branch = shop.transform;
                while (branch != canvas.transform)
                {
                    Transform parent = branch.parent;
                    foreach (Transform sibling in parent)
                        if (sibling != branch && sibling != background) sibling.gameObject.SetActive(false);
                    parent.gameObject.SetActive(true);
                    branch = parent;
                }
                shop.gameObject.SetActive(true);
                ((GameObject)settings.FindProperty("offersRoot").objectReferenceValue).SetActive(true);
                PerkCatalog catalog = (PerkCatalog)settings.FindProperty("catalog").objectReferenceValue;
                var offers = catalog.GetOffers(false);
                SerializedProperty slots = settings.FindProperty("slots");
                for (int i = 0; i < 4; i++)
                {
                    var slot = new SerializedObject(slots.GetArrayElementAtIndex(i).objectReferenceValue);
                    Transform parent = (Transform)slot.FindProperty("artworkParent").objectReferenceValue;
                    PrefabUtility.InstantiatePrefab(catalog.GetPrefab(offers[i].Id), parent);
                }
                var guide = (NpcGuidePresentation)settings.FindProperty("guide").objectReferenceValue;
                var destination = (RectTransform)settings.FindProperty("guideDestination").objectReferenceValue;
                guide.gameObject.SetActive(true);
                ((RectTransform)guide.transform).anchoredPosition = destination.anchoredPosition;
                guide.transform.Find("TextIntro").gameObject.SetActive(false);
                guide.transform.Find("TextPerk").gameObject.SetActive(true);
                guide.transform.Find("Lunar").gameObject.SetActive(true);
                guide.transform.Find("Lunar2").gameObject.SetActive(false);
                Canvas.ForceUpdateCanvases();
                target = new RenderTexture(1536, 405, 24);
                camera.targetTexture = target;
                camera.scene = preview;
                camera.aspect = 8192f / 2160f;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("Library/PerkShopPreview.png", image.EncodeToPNG());
                File.WriteAllText("Library/PerkShopVerification.txt", "PASS: saved scene loaded in isolation; 4 cards instantiated; Lunar TextPerk shown; preview rendered. Live scene unchanged.");
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/PerkShopVerification.txt", error.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
