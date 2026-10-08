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
    // Renders normal and automatic-team-bonus results, including detail crops, at the wall canvas size,
    // inside an isolated preview scene so the open scene is never touched.
    [InitializeOnLoad]
    public static class ResultPanelPreview
    {
        private const string RequestPath = "Temp/ResultPanelPreview.request";
        private const string OutputPath = "Temp/ResultPanelPreview.png";
        private const string ResultPrefabPath = "Assets/Prefab/UI/Result.prefab";
        private const string BackgroundPath = "Assets/Art/BG/BGFoodDay.png";
        private const int Width = 1536;
        private const int Height = 435;

        static ResultPanelPreview() => EditorApplication.update += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
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

                // Both samples use the same four players so the optional team bonus is easy to compare.
                for (int variant = 0; variant < 2; variant++)
                {
                    const int players = 4;
                    int playerTotal = 0;
                    for (int i = 0; i < players; i++) playerTotal += allScores[i];
                    int teamBonus = variant == 0 ? 0 : 40;
                    const int penalty = 25;
                    const int coins = 60;
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
                    if (reveal != null) reveal.Play(playerTotal, coins, playerTotal + teamBonus - penalty + coins,
                        scores, penalty, teamBonus);

                    float[] frames = { 8f };
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
                        string path = OutputPath.Replace(".png", variant == 0 ? "_Normal.png" : "_Team.png");
                        Capture(camera, target, result, path);
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
            var total = (RectTransform)result.transform.Find("Content/TotalScore");
            var corners = new Vector3[4];
            total.GetWorldCorners(corners);
            Vector3 center = camera.WorldToScreenPoint(total.position);
            float scale = (camera.WorldToScreenPoint(corners[2]).x - camera.WorldToScreenPoint(corners[0]).x) / total.rect.width;
            int left = Mathf.Max(0, Mathf.FloorToInt(center.x - 115f * scale));
            int bottom = Mathf.Max(0, Mathf.FloorToInt(center.y - 65f * scale));
            int width = Mathf.Min(target.width - left, Mathf.CeilToInt(280f * scale));
            int height = Mathf.Min(target.height - bottom, Mathf.CeilToInt(90f * scale));
            RenderTexture.active = target;
            var detail = new Texture2D(width, height, TextureFormat.RGBA32, false);
            detail.ReadPixels(new Rect(left, bottom, width, height), 0, 0);
            detail.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path.Replace(".png", "_Detail.png"), detail.EncodeToPNG());
            Object.DestroyImmediate(detail);
            TMP_Text heading = total.GetComponent<TMP_Text>();
            TMP_Text breakdown = total.Find("Text_Bonus").GetComponent<TMP_Text>();
            float headingLeft = camera.WorldToScreenPoint(heading.transform.TransformPoint(
                heading.textInfo.characterInfo[0].bottomLeft)).x;
            float breakdownLeft = camera.WorldToScreenPoint(breakdown.transform.TransformPoint(
                breakdown.textInfo.characterInfo[0].bottomLeft)).x;
            File.WriteAllText(path.Replace(".png", ".txt"),
                $"Heading left: {headingLeft}\nBreakdown left: {breakdownLeft}\nPixels per unit: {scale}\n" +
                $"Font size: {breakdown.fontSize}\nLines: {breakdown.textInfo.lineCount}\nText: {breakdown.text}");
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
