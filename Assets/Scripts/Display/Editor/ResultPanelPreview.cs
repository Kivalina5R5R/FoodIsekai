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
    // Renders the Result prefab with sample numbers to Temp/ResultPanelPreview.png at the wall canvas size,
    // inside an isolated preview scene so the open scene is never touched.
    [InitializeOnLoad]
    public static class ResultPanelPreview
    {
        private const string RequestPath = "Temp/ResultPanelPreview.request";
        private const string OutputPath = "Temp/ResultPanelPreview.png";
        // Frames taken through the reveal sequence; the last one is the finished screen.
        private static readonly float[] AnimationSeconds = { 0.9f, 1.9f, 2.7f, 3.75f, 6f };
        private const string ResultPrefabPath = "Assets/Prefab/UI/Result.prefab";
        private const string BackgroundPath = "Assets/Art/BG/BGFoodDay.png";
        private const int Width = 1536;
        private const int Height = 435;

        static ResultPanelPreview() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Render();
        }

        [MenuItem("Food Isekai/Render Result Panel Preview")]
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
                camera.backgroundColor = new Color(0.2f, 0.16f, 0.12f, 1f);
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

                Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
                if (background != null)
                {
                    var backgroundObject = new GameObject("Background", typeof(RectTransform));
                    backgroundObject.transform.SetParent(canvasObject.transform, false);
                    var rect = (RectTransform)backgroundObject.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    backgroundObject.AddComponent<Image>().sprite = background;
                }

                // Mirrors the scene: Result sits at (0, -44) under the centered Menu object.
                var menu = new GameObject("Menu", typeof(RectTransform));
                menu.transform.SetParent(canvasObject.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPrefabPath);
                int[] allScores = { 420, 360, 270, 184, 150, 96 };

                // One finished screen per player count (Temp/ResultPanelPreview_1p.png to _6p.png); the four-player
                // screen also keeps the frames through the reveal sequence.
                for (int players = 1; players <= 6; players++)
                {
                    GameObject result = Object.Instantiate(prefab, menu.transform, false);
                    result.name = "Result";
                    ((RectTransform)result.transform).anchoredPosition = new Vector2(0f, -44f);
                    result.SetActive(true);
                    foreach (CanvasGroup group in result.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;

                    Transform content = result.transform.Find("Content") ?? result.transform;
                    var scores = new int[players];
                    for (int i = 0; i < 6; i++)
                    {
                        Transform row = content.Find($"Player{i + 1}");
                        if (row != null) row.gameObject.SetActive(i < players);
                        SetText(content, $"Player{i + 1}/Playr{i + 1}", $"Player{i + 1}");
                        if (i < players) scores[i] = allScores[i];
                    }
                    content.GetComponent<ResultRowLayout>()?.Apply(players);

                    // Edit mode skips these components' enable step, so start them the way play mode would.
                    var reveal = content.GetComponent<ResultRevealSequence>();
                    var graphics = result.GetComponentsInChildren<MaskableGraphic>(true);
                    foreach (MaskableGraphic graphic in graphics)
                        if (IsAnimated(graphic)) Call(graphic, "OnEnable");
                    if (reveal != null) reveal.Play(1200, 60, 1260, scores);

                    float[] frames = players == 4 ? AnimationSeconds : new[] { AnimationSeconds[AnimationSeconds.Length - 1] };
                    for (int i = 0; i < frames.Length; i++)
                    {
                        if (reveal != null)
                        {
                            SetElapsed(reveal, frames[i]);
                            Call(reveal, "Apply");
                        }
                        foreach (MaskableGraphic graphic in graphics)
                        {
                            if (!IsAnimated(graphic)) continue;
                            // The shine is caught partway across the first bar and the MVP badge mid-beat.
                            SetElapsed(graphic, graphic is ResultRowShine ? 0.45f :
                                graphic is ResultMvpEffect ? 0.24f : frames[i]);
                            if (graphic is ResultMvpEffect) Call(graphic, "Update");
                            graphic.SetVerticesDirty();
                        }
                        bool finished = i == frames.Length - 1;
                        string path = finished ? OutputPath.Replace(".png", $"_{players}p.png")
                            : OutputPath.Replace(".png", $"_t{frames[i]:0.0}.png");
                        Capture(camera, target, result, path);
                        if (finished && players == 4) Capture(camera, target, result, OutputPath);
                    }
                    if (reveal != null) Call(reveal, "OnDisable");
                    Object.DestroyImmediate(result);
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

        private static bool IsAnimated(MaskableGraphic graphic) =>
            graphic is ResultPlayerRowSparkles || graphic is ResultRowShine || graphic is ResultMvpEffect;

        private static void Capture(Camera camera, RenderTexture target, GameObject result, string path)
        {
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in result.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
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
            Debug.Log("[ResultPanelPreview] Saved " + Path.GetFullPath(path));
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(target, null);

        private static void SetElapsed(object target, float seconds) =>
            target.GetType().GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, seconds);

        private static void SetText(Transform root, string path, string value)
        {
            TMP_Text text = root.Find(path)?.GetComponent<TMP_Text>();
            if (text != null) text.text = value;
        }
    }
}
