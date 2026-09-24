using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Repairs only remnants of the earlier background clipping edit when explicitly requested.
    [InitializeOnLoad]
    internal static class BackgroundDisplayVerification
    {
        private const string RequestPath = "Library/BackgroundDisplayVerification.request";
        private const string PreviewRequestPath = "Library/BackgroundSceneCapture.v3.request";
        private const string OutputPath = "Library/BackgroundDisplayVerification.txt";

        static BackgroundDisplayVerification()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(PreviewRequestPath))
            {
                File.Delete(PreviewRequestPath);
                var previewReport = new StringBuilder();
                try
                {
                    SceneView view = SceneView.lastActiveSceneView;
                    if (view == null) throw new InvalidOperationException("No Scene View is open.");
                    int width = 960;
                    int height = Mathf.RoundToInt(width / view.camera.aspect);
                    Capture(view.camera, width, height, previewReport, "WallClipVerified");
                    foreach (RectMask2D mask in Resources.FindObjectsOfTypeAll<RectMask2D>())
                        if (mask.gameObject.scene.path == "Assets/Scenes/FoodIsekai.unity" && mask.name == "Background")
                            previewReport.AppendLine($"Loaded wall mask: enabled={mask.enabled}, padding={mask.padding}, rect={mask.canvasRect}");
                }
                catch (Exception error)
                {
                    previewReport.AppendLine(error.ToString());
                }
                File.WriteAllText("Library/BackgroundSceneCapture.v3.txt", previewReport.ToString());
            }
            if (!File.Exists(RequestPath)) return;
            bool clipWall = File.ReadAllText(RequestPath).Trim() == "clip-wall";
            File.Delete(RequestPath);
            var report = new StringBuilder();
            try
            {
                Verify(report, clipWall);
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
            }
            File.WriteAllText(OutputPath, report.ToString());
        }

        private static void Verify(StringBuilder report, bool clipWall)
        {
            foreach (SpriteRenderer sprite in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (sprite.gameObject.scene.path != "Assets/Scenes/FoodIsekai.unity" || sprite.name != "Background") continue;
                if (sprite.transform.parent == null || sprite.transform.parent.name != "Arena") continue;
                Material material = sprite.sharedMaterial;
                report.AppendLine($"Floor before: material={material}, shader={material?.shader}, mask={sprite.maskInteraction}");
                if (material == null || material.shader == null || material.name == "FloorBackgroundClip" ||
                    material.shader.name == "Hidden/InternalErrorShader" ||
                    material.shader.name == "FoodIsekaiZ/FloorBackgroundClip")
                {
                    Undo.RecordObject(sprite, "Restore original floor background material");
                    sprite.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                    sprite.maskInteraction = SpriteMaskInteraction.None;
                    EditorUtility.SetDirty(sprite);
                }
                report.AppendLine($"Floor after: material={sprite.sharedMaterial}, shader={sprite.sharedMaterial?.shader}");
            }
            if (clipWall) ClipWall(report);
            Canvas.ForceUpdateCanvases();
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera.gameObject.scene.path != "Assets/Scenes/FoodIsekai.unity") continue;
                if (camera.name != "FloorCamera" && camera.name != "SideCamera") continue;
                report.AppendLine($"{camera.name}: aspect={camera.aspect}, pixelRect={camera.pixelRect}, rect={camera.rect}, ortho={camera.orthographicSize}");
                bool floor = camera.name == "FloorCamera";
                Capture(camera, floor ? 704 : 1024, floor ? 320 : 270, report);
            }
            Shader preview = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Display/Editor/BackgroundScenePreview.shader");
            if (preview != null)
            {
                foreach (var message in ShaderUtil.GetShaderMessages(preview)) report.AppendLine($"Preview shader: {message.severity}: {message.message}");
            }
            SceneView.RepaintAll();
            report.AppendLine("Finished. No scene was saved or reloaded.");
        }

        private static void ClipWall(StringBuilder report)
        {
            Camera wall = null;
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera.gameObject.scene.path == "Assets/Scenes/FoodIsekai.unity" && camera.name == "SideCamera") wall = camera;
            if (wall == null) throw new InvalidOperationException("Wall camera is not loaded.");
            Color32[] before = Capture(wall, 1024, 270, report, "BeforeClip");
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.scene != wall.gameObject.scene || canvas.name != "SideDisplay") continue;
                Transform background = canvas.transform.Find("Background");
                if (background == null) continue;
                RectMask2D mask = background.GetComponent<RectMask2D>();
                if (mask == null) mask = Undo.AddComponent<RectMask2D>(background.gameObject);
                Undo.RecordObject(mask, "Clip wall background to camera frame");
                mask.padding = new Vector4(-56.88889f, 0f, -56.88889f, 0f);
                mask.softness = Vector2Int.zero;
                mask.enabled = true;
                EditorUtility.SetDirty(mask);
                report.AppendLine($"Wall clip: padding={mask.padding}; image transforms unchanged.");
            }
            Canvas.ForceUpdateCanvases();
            Color32[] after = Capture(wall, 1024, 270, report, "AfterClip");
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
                if (!before[i].Equals(after[i])) changed++;
            report.AppendLine($"Wall Game render comparison: {changed}/{before.Length} pixels changed; playing={Application.isPlaying}.");
        }

        private static Color32[] Capture(Camera camera, int width, int height, StringBuilder report, string suffix = "BackgroundCheck")
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes($"Library/{camera.name}-{suffix}.png", pixels.EncodeToPNG());
                Color32[] colors = pixels.GetPixels32();
                int magenta = 0;
                foreach (Color32 color in colors)
                    if (color.r > 240 && color.g < 20 && color.b > 240) magenta++;
                report.AppendLine($"{camera.name}: capture {width}x{height}, magenta pixels={magenta}/{width * height}");
                return colors;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
