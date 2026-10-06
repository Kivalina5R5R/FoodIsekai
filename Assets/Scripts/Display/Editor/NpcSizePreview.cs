using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Renders the imported NPC prefabs through the open scene's wall camera in an isolated preview scene.
    [InitializeOnLoad]
    internal static class NpcSizePreview
    {
        private const string RequestPath = "Temp/NpcSizePreview.request";

        static NpcSizePreview() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            Capture();
        }

        [MenuItem("Food Isekai/Preview NPC Sizes")]
        private static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
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
                report.AppendLine($"Source scene dirty={source.gameObject.scene.isDirty}; camera size={sourceCamera.orthographicSize}");
                GameObject copy = UnityEngine.Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, preview);
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && !(behaviour is Graphic)) behaviour.enabled = false;
                Canvas wall = copy.GetComponent<Canvas>();
                var cameraObject = new GameObject("NPC Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera captureCamera = cameraObject.GetComponent<Camera>();
                captureCamera.CopyFrom(sourceCamera);
                captureCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                captureCamera.scene = preview;
                captureCamera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
                wall.worldCamera = captureCamera;
                Transform hud = copy.transform.Find("Text");
                if (hud != null) hud.gameObject.SetActive(true);
                Transform menu = copy.transform.Find("Menu");
                if (menu != null) menu.gameObject.SetActive(true);
                if (menu != null)
                    foreach (CanvasGroup group in menu.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
                foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Wall Intro Overlay" || child.name == "NPC Guide Player") child.gameObject.SetActive(false);
                var spawner = copy.GetComponent<FoodIsekaiZNpcWaveSpawner>();
                typeof(FoodIsekaiZNpcWaveSpawner).GetField("sideCanvas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, wall);
                MethodInfo align = typeof(FoodIsekaiZNpcWaveSpawner).GetMethod("AlignNpcToDisplaySlot", BindingFlags.Instance | BindingFlags.NonPublic);
                GameObject[] previewNpcs = (GameObject[])typeof(FoodIsekaiZNpcWaveSpawner).GetField("spawnedNpcs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
                string[] names = { "NPC12-Fox", "NPC03-Kwang", "NPC02-Rabbit", "NPC05-Maow", "NPC11-Fairy-1", "NPC07-Raven-1" };
                int firstMenu = menu != null ? menu.GetSiblingIndex() : copy.transform.childCount;
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/Charactor/{names[i]}.prefab");
                    GameObject npc = UnityEngine.Object.Instantiate(prefab, copy.transform, false);
                    previewNpcs[i] = npc;
                    npc.transform.SetSiblingIndex(firstMenu + i);
                    align.Invoke(spawner, new object[] { npc, i });
                    Transform emoji = npc.transform.Find("Emoji");
                    if (emoji != null) emoji.gameObject.SetActive(false);
                    RectTransform body = npc.transform.Find("Image") as RectTransform;
                    report.AppendLine($"{names[i]}: imported scale={body.localScale:F6}, offset={body.anchoredPosition:F3}, root={((RectTransform)npc.transform).anchoredPosition:F3}");
                }
                copy.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>())
                    if (graphic.rectTransform.rect.width > 1000)
                        report.AppendLine($"Large graphic {graphic.name}: type={graphic.GetType().Name}, rect={graphic.rectTransform.rect}, color={graphic.color}, material={graphic.material.name}");
                report.AppendLine($"Canvas rect={((RectTransform)wall.transform).rect}; world scale={wall.transform.lossyScale:F8}");
                target = RenderTexture.GetTemporary(1368, 361, 24, RenderTextureFormat.ARGB32);
                captureCamera.aspect = 1368f / 361f;
                RenderPipeline.SubmitRenderRequest(captureCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels = new Texture2D(1368, 361, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1368, 361), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Temp/NpcSizePreview.png", pixels.EncodeToPNG());
                VerifyWalking(spawner, previewNpcs, report);
                report.AppendLine("Captured with Unity URP; source scene was not saved or edited.");
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(preview);
                File.WriteAllText("Temp/NpcSizePreview.txt", report.ToString());
            }
        }

        private static void VerifyWalking(FoodIsekaiZNpcWaveSpawner spawner, GameObject[] npcs, StringBuilder report)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(FoodIsekaiZNpcWaveSpawner);
            Vector2[] movement = (Vector2[])type.GetField("npcMovementPositions", flags).GetValue(spawner);
            Vector2[] targets = (Vector2[])type.GetField("npcTargetPositions", flags).GetValue(spawner);
            bool[] approaching = (bool[])type.GetField("npcApproachingAtSlots", flags).GetValue(spawner);
            bool[] arrived = (bool[])type.GetField("npcArrivedAtSlots", flags).GetValue(spawner);
            MethodInfo animate = type.GetMethod("AnimateNpcArrival", flags);
            float[] elapsed = (float[])type.GetField("npcWalkingElapsedSeconds", flags).GetValue(spawner);
            float bobHeight = (float)type.GetField("walkingBobHeight", flags).GetValue(spawner);
            int checks = 0;
            for (int slot = 0; slot < npcs.Length; slot++)
            {
                RectTransform root = (RectTransform)npcs[slot].transform;
                Vector3 scale = root.localScale;
                foreach (int fps in new[] { 30, 60, 144 })
                foreach (float speed in new[] { 405.504f, 567.7056f })
                foreach (int direction in new[] { -1, 1 })
                {
                    elapsed[slot] = 0f;
                    approaching[slot] = false;
                    arrived[slot] = false;
                    movement[slot] = targets[slot] - Vector2.right * (direction * 600f);
                    root.anchoredPosition = movement[slot];
                    float previousX = root.anchoredPosition.x;
                    for (int frame = 0; frame < fps * 4 && !arrived[slot]; frame++)
                    {
                        animate.Invoke(spawner, new object[] { slot, root, speed, 0.28f, 1f / fps });
                        float lift = root.anchoredPosition.y - targets[slot].y;
                        if (lift < -0.01f || lift > bobHeight + 0.01f ||
                            Mathf.Abs(root.anchoredPosition.x - movement[slot].x) > 0.001f ||
                            (root.anchoredPosition.x - previousX) * direction < -0.01f ||
                            (root.anchoredPosition.x - targets[slot].x) * direction > 0.01f)
                            throw new InvalidOperationException($"Walking bounds or direction failed: slot={slot}, fps={fps}, speed={speed}");
                        previousX = root.anchoredPosition.x;
                    }
                    if (!arrived[slot] || Vector2.Distance(root.anchoredPosition, targets[slot]) > 0.001f ||
                        Quaternion.Angle(root.localRotation, Quaternion.identity) > 0.001f || root.localScale != scale)
                        throw new InvalidOperationException("Arrival did not restore the exact standing pose.");
                    checks++;
                }
            }
            report.AppendLine($"PASS vertical-only gait: {checks} Unity arrival simulations, both directions, 30/60/144 FPS, base/fast pace; X exactly matches travel path, lift 0..{bobHeight}, exact standing pose and unchanged scale.");
        }

    }
}
