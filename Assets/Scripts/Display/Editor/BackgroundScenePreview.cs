using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FoodIsekaiZ.Display.Editor
{
    // Temporarily clips background previews for Scene cameras, without saving or changing game materials.
    [InitializeOnLoad]
    internal static class BackgroundScenePreview
    {
        private const string ScenePath = "Assets/Scenes/FoodIsekai.unity";
        private const string ShaderPath = "Assets/Scripts/Display/Editor/BackgroundScenePreview.shader";
        private static readonly Dictionary<SpriteRenderer, Material> spriteMaterials = new Dictionary<SpriteRenderer, Material>();
        private static Material floorPreview;
        private static Camera previewCamera;

        static BackgroundScenePreview()
        {
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;
            Camera.onPreCull += BeginBuiltInCamera;
            Camera.onPostRender += EndBuiltInCamera;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        private static void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            BeginPreview(camera);
        }

        private static void EndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            EndPreview(camera);
        }

        private static void BeginBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) BeginPreview(camera);
        }

        private static void EndBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) EndPreview(camera);
        }

        private static void BeginPreview(Camera camera)
        {
            // A Game camera must never inherit a preview material from an interrupted Scene render.
            if (camera.cameraType != CameraType.SceneView)
            {
                if (previewCamera != null) RestoreMaterials();
                return;
            }
            if (previewCamera != null) return;
            Camera floor = null;
            foreach (Camera candidate in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (candidate.gameObject.scene.path != ScenePath) continue;
                if (candidate.name == "FloorCamera") floor = candidate;
            }
            if (floor == null) return;
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported) return;
            if (floorPreview == null) floorPreview = CreatePreviewMaterial(shader);
            floorPreview.SetMatrix("_BackgroundCameraProjection", floor.projectionMatrix * floor.worldToCameraMatrix);
            previewCamera = camera;

            foreach (SpriteRenderer sprite in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (sprite.gameObject.scene != floor.gameObject.scene || sprite.name != "Background") continue;
                if (sprite.transform.parent == null || sprite.transform.parent.name != "Arena") continue;
                spriteMaterials.Add(sprite, sprite.sharedMaterial);
                sprite.sharedMaterial = floorPreview;
            }
        }

        private static Material CreatePreviewMaterial(Shader shader)
        {
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static void EndPreview(Camera camera)
        {
            if (previewCamera == camera) RestoreMaterials();
        }

        private static void RestoreMaterials()
        {
            foreach (KeyValuePair<SpriteRenderer, Material> entry in spriteMaterials)
                if (entry.Key != null) entry.Key.sharedMaterial = entry.Value;
            spriteMaterials.Clear();
            previewCamera = null;
        }

        private static void Dispose()
        {
            RestoreMaterials();
            if (floorPreview != null) Object.DestroyImmediate(floorPreview);
        }
    }
}
