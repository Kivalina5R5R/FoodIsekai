using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Renders a minigame view prefab to a PNG for checking its layout without playing: the wall on
    // top and the floor below, on a dark tavern backdrop, with every template and hidden part
    // switched on so each piece shows once. Nothing in the project is changed.
    public static class MinigamePrefabPreview
    {
        private const int Width = 1536;
        private const int Height = 1015;

        public static string Render(string name, string outputPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MinigamePrefabKit.PathOf(name));
            if (prefab == null) return "missing " + MinigamePrefabKit.PathOf(name);

            Scene scene = EditorSceneManager.NewPreviewScene();
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            try
            {
                var cameraObject = new GameObject("Preview Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.07f, 0.04f, 1f);
                camera.targetTexture = texture;

                var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = (RectTransform)canvasObject.transform;
                canvasRect.sizeDelta = new Vector2(Width, Height);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var root = (RectTransform)instance.transform;
                root.SetParent(canvasRect, false);
                // Wall at the top edge, floor right under it.
                root.anchoredPosition = new Vector2(0f, Height * 0.5f - MinigamePrefabKit.WallSize.y * 0.5f);
                var view = instance.GetComponent<MinigameView>();
                // The floor lies flat in the prefab; stand it up under the wall for a flat picture.
                if (view != null && view.Floor != null)
                {
                    view.Floor.localRotation = Quaternion.identity;
                    view.Floor.localScale = Vector3.one;
                    view.Floor.anchoredPosition3D = new Vector3(0f, -(MinigamePrefabKit.WallSize.y + MinigamePrefabKit.FloorSize.y) * 0.5f - 40f, 0f);
                }
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();

                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return outputPath;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }
    }
}
