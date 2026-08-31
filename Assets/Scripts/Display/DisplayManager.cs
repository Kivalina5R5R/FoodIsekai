using UnityEngine;

namespace FoodIsekaiZ.Display
{

    [DefaultExecutionOrder(-200)]
    public sealed class DisplayManager : MonoBehaviour
    {
        [Header("Display 1 - Wall")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private Canvas[] sideCanvases;

        [Header("Display 2 - LED Floor")]
        [SerializeField] private Camera floorCamera;
        [SerializeField] private Canvas[] floorCanvases;

        [Header("Options")]
        [SerializeField] private bool activateSecondDisplayOnAwake = true;
        [SerializeField] private bool applyPaperArenaResolutionsInStandalone = true;
        [SerializeField] private Vector2Int sideDisplayResolution = new Vector2Int(1536, 435);
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2816, 1280);
        [SerializeField, Min(30)] private int refreshRate = 60;
        [SerializeField] private Color sideDisplayBackground = new Color(0.025f, 0.035f, 0.055f, 1f);

        public Vector2Int SideDisplayResolution => sideDisplayResolution;
        public Vector2Int FloorDisplayResolution => floorDisplayResolution;

        private void Awake()
        {
            ConfigureOutputs();

            if (activateSecondDisplayOnAwake)
            {
                ActivateDisplays();
            }
        }

        [ContextMenu("Configure Camera And Canvas Outputs")]
        public void ConfigureOutputs()
        {
            // This installation's Game View profiles identify Display 1 as the wall
            // and Display 2 as the floor, so role constants are used instead of raw indices.
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
                // Display 1 is the wall (1536x435); Display 2 is the floor (2816x1280).
                var targetRefreshRate = new RefreshRate
                {
                    numerator = (uint)refreshRate,
                    denominator = 1u
                };
                Screen.SetResolution(
                    sideDisplayResolution.x,
                    sideDisplayResolution.y,
                    FullScreenMode.FullScreenWindow,
                    targetRefreshRate);
                UnityEngine.Display.displays[1].Activate(
                    floorDisplayResolution.x,
                    floorDisplayResolution.y,
                    targetRefreshRate);
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
