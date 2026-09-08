using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Reveals an entering NPC on the foreground layer while preserving its opaque rear image.
    public sealed class NpcForegroundBlend : MonoBehaviour
    {
        private static readonly int ForegroundWeightId = Shader.PropertyToID("_ForegroundWeight");
        private RectTransform sourceRoot;
        private RectTransform sourceVisual;
        private Image sourceImage;
        private Material originalMaterial;
        private Material rearBlendMaterial;
        private RectTransform foregroundRoot;
        private RectTransform foregroundVisual;
        private CanvasGroup foregroundOpacity;
        private float approachStartDistance;
        private float rearLaneOffset;
        private float weight;

        // Gets the temporary foreground object used by the NPC slot sorting.
        public GameObject ForegroundRoot => foregroundRoot != null ? foregroundRoot.gameObject : null;

        // Gets the distance from the destination where the forward step begins.
        public float ApproachStartDistance => approachStartDistance;

        // Gets the remaining vertical offset from the final standing baseline.
        public float RearLaneOffset => foregroundRoot != null ? rearLaneOffset * (1f - weight) : 0f;

        // Gets how much of the foreground image is visible.
        public float Weight => weight;

        // Creates a visual-only foreground copy using the live NPC's authored geometry.
        public void Initialize(RectTransform npcRoot, Image npcImage, Transform frontLayer,
            float startDistance, float laneOffset)
        {
            Clear();
            sourceRoot = npcRoot;
            sourceVisual = npcImage.rectTransform;
            sourceImage = npcImage;
            originalMaterial = npcImage.material;
            approachStartDistance = startDistance;
            rearLaneOffset = laneOffset;

            var rootObject = new GameObject(npcRoot.name + " Foreground Blend",
                typeof(RectTransform), typeof(CanvasGroup));
            rootObject.layer = npcRoot.gameObject.layer;
            foregroundRoot = (RectTransform)rootObject.transform;
            foregroundRoot.SetParent(frontLayer, false);
            CopyRect(sourceRoot, foregroundRoot);
            foregroundOpacity = rootObject.GetComponent<CanvasGroup>();
            foregroundOpacity.interactable = false;
            foregroundOpacity.blocksRaycasts = false;
            foregroundOpacity.alpha = 0f;

            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.layer = npcImage.gameObject.layer;
            foregroundVisual = (RectTransform)imageObject.transform;
            foregroundVisual.SetParent(foregroundRoot, false);
            CopyRect(sourceVisual, foregroundVisual);

            Image foregroundImage = imageObject.GetComponent<Image>();
            foregroundImage.sprite = npcImage.sprite;
            foregroundImage.overrideSprite = npcImage.overrideSprite;
            foregroundImage.material = npcImage.material;
            foregroundImage.color = npcImage.color;
            foregroundImage.type = npcImage.type;
            foregroundImage.preserveAspect = npcImage.preserveAspect;
            foregroundImage.fillCenter = npcImage.fillCenter;
            foregroundImage.fillMethod = npcImage.fillMethod;
            foregroundImage.fillAmount = npcImage.fillAmount;
            foregroundImage.fillClockwise = npcImage.fillClockwise;
            foregroundImage.fillOrigin = npcImage.fillOrigin;
            foregroundImage.pixelsPerUnitMultiplier = npcImage.pixelsPerUnitMultiplier;
            foregroundImage.useSpriteMesh = npcImage.useSpriteMesh;
            foregroundImage.maskable = npcImage.maskable;
            foregroundImage.raycastTarget = false;

            Shader rearShader = Resources.Load<Shader>("Shaders/NpcRearBlend");
            if (rearShader != null)
            {
                // Compensate the rear alpha per texture pixel so soft sprite edges
                // keep their original opacity while both layers are being rendered.
                rearBlendMaterial = new Material(rearShader) { hideFlags = HideFlags.HideAndDontSave };
                rearBlendMaterial.CopyPropertiesFromMaterial(originalMaterial);
                rearBlendMaterial.SetFloat(ForegroundWeightId, 0f);
                sourceImage.material = rearBlendMaterial;
            }
        }

        // Blends in the foreground without fading the unoccluded body in the rear layer.
        public void SetProgress(float progress)
        {
            weight = Mathf.Clamp01(progress);
            if (foregroundOpacity != null)
            {
                foregroundOpacity.alpha = weight;
            }

            if (rearBlendMaterial != null)
            {
                rearBlendMaterial.SetFloat(ForegroundWeightId, weight);
                // A UI mask may use a stencil material derived from our material.
                sourceImage.materialForRendering.SetFloat(ForegroundWeightId, weight);
            }
        }

        // Keeps both images at the same animated pose, including their authored scale and flip.
        public void SynchronizePose()
        {
            if (foregroundRoot == null || sourceRoot == null || sourceVisual == null)
            {
                return;
            }

            CopyPose(sourceRoot, foregroundRoot);
            CopyPose(sourceVisual, foregroundVisual);
        }

        // Immediately hides and releases the temporary foreground image.
        public void Clear()
        {
            if (sourceImage != null && rearBlendMaterial != null)
            {
                sourceImage.material = originalMaterial;
            }

            if (rearBlendMaterial != null)
            {
                Destroy(rearBlendMaterial);
            }

            if (foregroundRoot != null)
            {
                foregroundRoot.gameObject.SetActive(false);
                Destroy(foregroundRoot.gameObject);
            }

            foregroundRoot = null;
            foregroundVisual = null;
            foregroundOpacity = null;
            sourceRoot = null;
            sourceVisual = null;
            sourceImage = null;
            originalMaterial = null;
            rearBlendMaterial = null;
            weight = 0f;
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.sizeDelta = source.sizeDelta;
            CopyPose(source, destination);
        }

        private static void CopyPose(RectTransform source, RectTransform destination)
        {
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }
    }
}
