using UnityEngine;

namespace FoodIsekaiZ.Display
{

    [DefaultExecutionOrder(-200)]
    public sealed class DisplayManager : MonoBehaviour
    {
        [Header("Display 2 - Wall")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private Canvas[] sideCanvases;

        [Header("Display 1 - LED Floor")]
        [SerializeField] private Camera floorCamera;
        [SerializeField] private Canvas[] floorCanvases;

        [Header("Options")]
        [SerializeField] private bool activateSecondDisplayOnAwake = true;
        [SerializeField] private bool applyPaperArenaResolutionsInStandalone = true;
        [SerializeField] private Vector2Int sideDisplayResolution = new Vector2Int(8192, 2160);
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2944, 1408);
        [SerializeField, Min(30)] private int refreshRate = 60;
        [SerializeField] private Color sideDisplayBackground = new Color(0.025f, 0.035f, 0.055f, 1f);

        public Vector2Int SideDisplayResolution => sideDisplayResolution;
        public Vector2Int FloorDisplayResolution => floorDisplayResolution;

        private int floorOutputWidth;
        private int floorOutputHeight;

        private void Awake()
        {
            ConfigureOutputs();
            ApplyFloorResolution();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }
        }

        private void ApplyFloorResolution()
        {
            if (!applyPaperArenaResolutionsInStandalone || Application.isEditor) return;
            Screen.SetResolution(floorDisplayResolution.x, floorDisplayResolution.y,
                FullScreenMode.FullScreenWindow, TargetRefreshRate);
        }

        private RefreshRate TargetRefreshRate => new RefreshRate
        {
            numerator = (uint)refreshRate,
            denominator = 1u
        };

        // Resolution changes complete after the current frame. Fit again when the output changes.
        private void LateUpdate()
        {
            if (Application.isEditor || !applyPaperArenaResolutionsInStandalone || floorCamera == null) return;
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0 || floorDisplayResolution.x <= 0 || floorDisplayResolution.y <= 0) return;
            if (width == floorOutputWidth && height == floorOutputHeight) return;
            floorOutputWidth = width;
            floorOutputHeight = height;

            float referenceAspect = (float)floorDisplayResolution.x / floorDisplayResolution.y;
            float outputAspect = (float)width / height;
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (outputAspect > referenceAspect)
            {
                viewport.width = referenceAspect / outputAspect;
                viewport.x = (1f - viewport.width) * 0.5f;
            }
            else
            {
                viewport.height = outputAspect / referenceAspect;
                viewport.y = (1f - viewport.height) * 0.5f;
            }
            floorCamera.rect = viewport;
            floorCamera.aspect = referenceAspect;
            Debug.Log($"[DisplayManager] Floor requested={floorDisplayResolution.x}x{floorDisplayResolution.y}, " +
                $"rendering={width}x{height}, desktop={UnityEngine.Display.main.systemWidth}x{UnityEngine.Display.main.systemHeight}, " +
                $"displays={UnityEngine.Display.displays.Length}, viewport={viewport}", this);
        }

        [ContextMenu("Configure Camera And Canvas Outputs")]
        public void ConfigureOutputs()
        {
            // This installation uses Display 1 for the floor and Display 2 for the wall,
            // so role constants are used instead of raw indices.
            if (floorCamera != null)
            {
                floorCamera.targetDisplay = DisplayOutput.FloorDisplayIndex;
            }

            if (sideCamera != null)
            {
                sideCamera.targetDisplay = DisplayOutput.WallDisplayIndex;
                sideCamera.clearFlags = CameraClearFlags.SolidColor;
                sideCamera.backgroundColor = sideDisplayBackground;
            }

            SetCanvasDisplay(floorCanvases, DisplayOutput.FloorDisplayIndex);
            SetCanvasDisplay(sideCanvases, DisplayOutput.WallDisplayIndex);
        }

        [ContextMenu("Activate Displays")]
        public void ActivateDisplays()
        {
            if (UnityEngine.Display.displays.Length < 2)
            {
                Debug.LogWarning("[DisplayManager] ระบบปฏิบัติการรายงานจอเพียง 1 จอ", this);
                return;
            }

            if (applyPaperArenaResolutionsInStandalone && !Application.isEditor)
            {
                UnityEngine.Display.displays[1].Activate(
                    sideDisplayResolution.x,
                    sideDisplayResolution.y,
                    TargetRefreshRate);
            }
            else
            {
                UnityEngine.Display.displays[1].Activate();
            }
        }

        private static void SetCanvasDisplay(Canvas[] canvases, int displayIndex)
        {
            if (canvases == null)
            {
                return;
            }

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].targetDisplay = displayIndex;
                }
            }
        }
    }
}
