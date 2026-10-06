using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Renders the meal transition page for a meal and for the perk break to Temp/MealTransitionPreview_*.png
    // at the wall canvas size, inside an isolated preview scene so the open scene is never touched.
    [InitializeOnLoad]
    public static class MealTransitionPreview
    {
        private const string RequestPath = "Temp/MealTransitionPreview.request";
        private const string PrefabPath = "Assets/Prefab/UI/MealMenuTransition.prefab";
        private const int Width = 1536;
        private const int Height = 435;
        private static readonly string[] Phases = { "LUNCH", "SERVICE BREAK" };

        static MealTransitionPreview() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Render();
        }

        [MenuItem("Food Isekai/Render Meal Transition Preview")]
        public static void Render()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Width * 2, Height * 2, 24, RenderTextureFormat.ARGB32);
            Camera camera = null;
            try
            {
                var cameraObject = new GameObject("Preview Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                camera = cameraObject.AddComponent<Camera>();
                camera.scene = preview;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;

                var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasObject, preview);
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(Width, Height);

                // Mirrors the scene, where the page stretches slightly past both wall edges for its slide.
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject page = Object.Instantiate(prefab, canvasObject.transform, false);
                var rect = (RectTransform)page.transform;
                rect.anchorMin = new Vector2(-0.037037037f, 0f);
                rect.anchorMax = new Vector2(1.037037037f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                page.SetActive(true);
                MealMenuTransition transition = page.GetComponent<MealMenuTransition>();
                TMP_Text heading = page.GetComponentInChildren<TMP_Text>(true);
                // Edit mode skips the particle layers' enable step, so start them the way play mode would,
                // and catch them partway through their motion.
                var layers = new System.Collections.Generic.List<MaskableGraphic>();
                layers.AddRange(page.GetComponentsInChildren<TransitionParticles>(true));
                layers.AddRange(page.GetComponentsInChildren<TransitionStreaks>(true));
                foreach (MaskableGraphic layer in layers)
                {
                    layer.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(layer, null);
                    layer.GetType().GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(layer, 0.8f);
                }

                foreach (string phase in Phases)
                {
                    typeof(MealMenuTransition).GetMethod("ApplyArtwork", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(transition, new object[] { phase });
                    // Shows only this phase's artwork object, the same way play mode does.
                    typeof(MealMenuTransition).GetMethod("SetPageDetailsVisible", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(transition, new object[] { true });
                    string title = (string)typeof(MealMenuTransition)
                        .GetMethod("ResolveTitle", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(transition, new object[] { phase });
                    if (heading != null) heading.text = title;
                    transition.SetVerticesDirty();
                    foreach (MaskableGraphic layer in layers) layer.SetVerticesDirty();
                    Capture(camera, target, page, $"Temp/MealTransitionPreview_{phase.Replace(' ', '_')}.png");
                }
            }
            finally
            {
                // Detach the texture from the camera first; releasing a camera's active target logs an error.
                if (camera != null) camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, GameObject root, string path)
        {
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log("[MealTransitionPreview] Saved " + Path.GetFullPath(path));
        }
    }
}
