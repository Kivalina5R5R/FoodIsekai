using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display.Editor
{
    // Builds the kiosk screen (background image and exit button) into the open game scene once.
    // It only adds objects; the scene is marked dirty for the user to save. After the first run it
    // never recreates the kiosk, so a manually removed or restyled kiosk stays as authored.
    // It also keeps the serialized target displays of the DisplayManager outputs in the role order
    // from DisplayOutput (Display 1 kiosk, Display 2 wall, Display 3 floor), so edit mode previews match a build.
    [InitializeOnLoad]
    public static class KioskDisplaySetup
    {
        private const string ScenePath = "Assets/Scenes/FoodIsekai.unity";
        private const string SpritePath = "Assets/Art/UI/Kiosk/KioskBackground.png";
        private const string FontPath = "Assets/Fonts/NotoSansThai_Condensed-Medium SDF.asset";
        private const string DoneKey = "FoodIsekaiZ.KioskDisplaySetupDone";

        static KioskDisplaySetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(DoneKey, false)) CreateInOpenScene();
                ApplyDisplayOrder();
            };
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += ApplyDisplayOrder;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyDisplayOrder;
            };
        }

        [MenuItem("Food Isekai/Apply Display Order")]
        public static void ApplyDisplayOrder()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath) return;
            DisplayManager manager = Object.FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            if (manager == null) return;

            var managerData = new SerializedObject(manager);
            int changed = 0;
            changed += SetCameraDisplay(managerData, "kioskCamera", DisplayOutput.KioskDisplayIndex);
            changed += SetCameraDisplay(managerData, "sideCamera", DisplayOutput.WallDisplayIndex);
            changed += SetCameraDisplay(managerData, "floorCamera", DisplayOutput.FloorDisplayIndex);
            changed += SetCanvasDisplays(managerData, "kioskCanvases", DisplayOutput.KioskDisplayIndex);
            changed += SetCanvasDisplays(managerData, "sideCanvases", DisplayOutput.WallDisplayIndex);
            changed += SetCanvasDisplays(managerData, "floorCanvases", DisplayOutput.FloorDisplayIndex);
            if (changed == 0) return;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[KioskDisplaySetup] Updated {changed} target display(s): Display 1 kiosk, Display 2 wall, " +
                "Display 3 floor. Save the scene to keep it.");
        }

        private static int SetCameraDisplay(SerializedObject managerData, string property, int displayIndex)
        {
            var camera = managerData.FindProperty(property)?.objectReferenceValue as Camera;
            if (camera == null || camera.targetDisplay == displayIndex) return 0;
            Undo.RecordObject(camera, "Apply Display Order");
            camera.targetDisplay = displayIndex;
            return 1;
        }

        private static int SetCanvasDisplays(SerializedObject managerData, string property, int displayIndex)
        {
            SerializedProperty canvases = managerData.FindProperty(property);
            if (canvases == null) return 0;
            int changed = 0;
            for (int i = 0; i < canvases.arraySize; i++)
            {
                var canvas = canvases.GetArrayElementAtIndex(i).objectReferenceValue as Canvas;
                if (canvas == null || canvas.targetDisplay == displayIndex) continue;
                Undo.RecordObject(canvas, "Apply Display Order");
                canvas.targetDisplay = displayIndex;
                changed++;
            }
            return changed;
        }

        [MenuItem("Food Isekai/Create Kiosk Display")]
        public static void CreateInOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath) return;

            DisplayManager manager = Object.FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogWarning("[KioskDisplaySetup] No DisplayManager in the open scene");
                return;
            }

            var managerData = new SerializedObject(manager);
            if (managerData.FindProperty("kioskCamera").objectReferenceValue != null)
            {
                EditorPrefs.SetBool(DoneKey, true);
                return;
            }

            Sprite background = ImportBackground();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = new GameObject("KioskDisplay");
            Undo.RegisterCreatedObjectUndo(root, "Create Kiosk Display");

            var cameraObject = new GameObject("KioskCamera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.targetDisplay = DisplayOutput.KioskDisplayIndex;

            var canvasObject = new GameObject("KioskCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = DisplayOutput.KioskDisplayIndex;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var backgroundObject = new GameObject("Background", typeof(RectTransform));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            Stretch(backgroundObject.GetComponent<RectTransform>());
            Image backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.sprite = background;
            backgroundImage.raycastTarget = false;

            var buttonObject = new GameObject("ExitButton", typeof(RectTransform));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 160f);
            buttonRect.sizeDelta = new Vector2(560f, 150f);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(1f, 1f, 1f, 0.12f);
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            buttonObject.AddComponent<ExitGameButton>();

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = "ออกจากเกม";
            label.fontSize = 64f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            Undo.RecordObject(manager, "Create Kiosk Display");
            managerData.Update();
            managerData.FindProperty("kioskCamera").objectReferenceValue = camera;
            SerializedProperty canvases = managerData.FindProperty("kioskCanvases");
            canvases.arraySize = 1;
            canvases.GetArrayElementAtIndex(0).objectReferenceValue = canvas;
            managerData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorPrefs.SetBool(DoneKey, true);
            Debug.Log("[KioskDisplaySetup] Created KioskDisplay (Game view Display 1). Save the scene to keep it.", root);
        }

        private static Sprite ImportBackground()
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
