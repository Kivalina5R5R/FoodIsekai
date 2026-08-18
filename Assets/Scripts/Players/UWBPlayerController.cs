using FoodIsekaiZ.Gameplay;
using Fortal.UWB;
using UnityEngine;
using UnityEngine.Serialization;

namespace FoodIsekaiZ.Players
{

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(SpriteRenderer))]
    [RequireComponent(typeof(FoodIsekaiZPlayerState))]
    public sealed class UWBPlayerController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Min(1)] private int playerId = 1;
        [SerializeField, Min(0)] private int tagId = 1;
        [SerializeField] private Color playerColor = Color.cyan;

        [Header("UWB")]
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private bool hideWhenOffline;

        [Header("Movement Smoothing")]
        [SerializeField, Range(0.02f, 0.5f)] private float smoothTime = 0.12f;
        [SerializeField, Min(0f)] private float positionDeadZone = 0.03f;
        [SerializeField, Min(0.1f)] private float snapDistance = 2f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 12f;
        [SerializeField, Min(0f)] private float floorHeight = 0.12f;

        [Header("Editor Simulation")]
        [Tooltip("During Play Mode, dragging this Player in Scene View writes the position back to its simulated UWB tag.")]
        [SerializeField] private bool allowSceneViewDragInSimulation = true;
        [SerializeField, Min(0.0001f)] private float sceneViewDragThreshold = 0.002f;

        [Header("Floor Player Marker")]
        [Tooltip("Overall width of the eye-shaped marker in world units.")]
        [SerializeField, Min(0.1f)] private float markerWorldWidth = 1f;
        [Tooltip("Marker height relative to its width.")]
        [SerializeField, Range(0.4f, 1f)] private float markerHeightRatio = 0.76f;
        [FormerlySerializedAs("markerWhiteGapScale")]
        [SerializeField, Range(0.2f, 0.9f)] private float markerTransparentGapScale = 0.72f;
        [SerializeField, Range(0.05f, 0.7f)] private float markerCenterWidthScale = 0.35f;
        [SerializeField, Range(0.05f, 0.7f)] private float markerCenterHeightScale = 0.40f;
        [SerializeField, Range(32, 256)] private int markerTextureWidth = 128;

        [Header("Floor Player Status")]
        [SerializeField] private bool showCarriedItems = true;
        [SerializeField, Range(0.02f, 0.12f)] private float statusCharacterSize = 0.055f;
        [SerializeField] private Color statusTextColor = Color.white;
        [Tooltip("Local X/Y offset from the marker centre. Default places text at the upper-right.")]
        [SerializeField] private Vector2 statusLabelOffset = new Vector2(0.48f, 0.28f);

        [Header("Runtime (Read Only)")]
        [SerializeField] private bool isTracking;
        [SerializeField] private float sampleAgeSeconds = 999f;

        private Rigidbody body;
        private SpriteRenderer circleRenderer;
        private SpriteRenderer markerCenterRenderer;
        private FoodIsekaiZPlayerState playerState;
        private TextMesh carriedStatusText;
        private Vector3 targetPosition;
        private Vector3 smoothVelocity;
        private bool hasFirstPosition;
        private bool isRegistered;
        private bool hasControllerPosition;
        private Vector3 lastControllerPosition;
        private Texture2D generatedMarkerTexture;
        private Sprite generatedMarkerSprite;
        private Texture2D generatedMarkerCenterTexture;
        private Sprite generatedMarkerCenterSprite;

        public int PlayerId => playerId;
        public int TagId => tagId;
        public bool IsTracking => isTracking;
        public float SampleAgeSeconds => sampleAgeSeconds;

        private void Awake()
        {
            CacheRequiredComponents();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            // Sprite ปกติอยู่บน XY; หมุนให้นอนบนพื้น XZ และหันขึ้นหากล้อง Floor
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            EnsurePlayerMarker();
            ApplyPlayerMarkerColor();

            CreateStatusLabel();
            RefreshStatusLabel();
            RecordControllerPosition(transform.position);
        }

        private void OnEnable()
        {
            FindAndRegisterManager();
        }

        private void OnDisable()
        {
            UnregisterManager();
        }

        private void OnDestroy()
        {
            if (generatedMarkerSprite != null)
            {
                Destroy(generatedMarkerSprite);
            }

            if (generatedMarkerTexture != null)
            {
                Destroy(generatedMarkerTexture);
            }

            if (generatedMarkerCenterSprite != null)
            {
                Destroy(generatedMarkerCenterSprite);
            }

            if (generatedMarkerCenterTexture != null)
            {
                Destroy(generatedMarkerCenterTexture);
            }
        }

        private void Update()
        {
            RefreshStatusLabel();

            if (!isRegistered)
            {
                FindAndRegisterManager();
            }

            if (CaptureSceneViewSimulationDrag())
            {
                return;
            }

            if (uwbManager == null ||
                !uwbManager.TryGetArenaPosition2D(tagId, out Vector2 measuredPosition2D, out sampleAgeSeconds))
            {
                isTracking = false;
                SetPlayerMarkerVisible(!hideWhenOffline);
                return;
            }

            isTracking = true;
            SetPlayerMarkerVisible(true);
            Vector3 measuredPosition = new Vector3(measuredPosition2D.x, floorHeight, measuredPosition2D.y);

            if (!hasFirstPosition || Vector3.Distance(body.position, measuredPosition) >= snapDistance)
            {
                targetPosition = measuredPosition;
                body.position = measuredPosition;
                smoothVelocity = Vector3.zero;
                hasFirstPosition = true;
                RecordControllerPosition(measuredPosition);
                return;
            }

            if (Vector3.Distance(targetPosition, measuredPosition) >= positionDeadZone)
            {
                targetPosition = measuredPosition;
            }
        }

        private void FixedUpdate()
        {
            if (CaptureSceneViewSimulationDrag())
            {
                return;
            }

            if (!hasFirstPosition)
            {
                return;
            }

            Vector3 next = Vector3.SmoothDamp(
                body.position,
                targetPosition,
                ref smoothVelocity,
                smoothTime,
                maxSpeed,
                Time.fixedDeltaTime);
            body.MovePosition(next);
            RecordControllerPosition(next);
        }

        private bool CaptureSceneViewSimulationDrag()
        {
#if UNITY_EDITOR
            if (!allowSceneViewDragInSimulation || !Application.isPlaying ||
                uwbManager == null || !uwbManager.IsSimulationMode || body == null ||
                !hasControllerPosition)
            {
                return false;
            }

            Vector3 currentPosition = transform.position;
            Vector2 planarDelta = new Vector2(
                currentPosition.x - lastControllerPosition.x,
                currentPosition.z - lastControllerPosition.z);
            if (planarDelta.sqrMagnitude <= sceneViewDragThreshold * sceneViewDragThreshold)
            {
                return false;
            }

            Vector3 committedPosition = new Vector3(currentPosition.x, floorHeight, currentPosition.z);
            if (!uwbManager.SetSimulatedArenaPosition2D(
                    tagId,
                    new Vector2(committedPosition.x, committedPosition.z)))
            {
                return false;
            }

            body.position = committedPosition;
            targetPosition = committedPosition;
            smoothVelocity = Vector3.zero;
            hasFirstPosition = true;
            RecordControllerPosition(committedPosition);
            return true;
#else
            return false;
#endif
        }

        private void RecordControllerPosition(Vector3 position)
        {
            lastControllerPosition = position;
            hasControllerPosition = true;
            transform.hasChanged = false;
        }

        public void SetTagId(int newTagId)
        {
            newTagId = Mathf.Max(0, newTagId);
            if (tagId == newTagId)
            {
                return;
            }

            UnregisterManager();
            tagId = newTagId;
            hasFirstPosition = false;
            FindAndRegisterManager();
        }

        public void Configure(int newPlayerId, int newTagId, Color newColor)
        {

            CacheRequiredComponents();
            playerId = Mathf.Max(1, newPlayerId);
            playerColor = newColor;
            SetTagId(newTagId);

            if (circleRenderer != null)
            {
                EnsurePlayerMarker();
                ApplyPlayerMarkerColor();
            }

            gameObject.name = $"Player{playerId:00}_Tag{tagId}";
            RefreshStatusLabel();
        }

        private void CreateStatusLabel()
        {
            if (!showCarriedItems || carriedStatusText != null)
            {
                return;
            }

            Transform existingLabel = transform.Find("PlayerStatus");
            GameObject labelObject = existingLabel != null
                ? existingLabel.gameObject
                : new GameObject("PlayerStatus");
            if (existingLabel == null)
            {
                labelObject.transform.SetParent(transform, false);
            }

            labelObject.transform.localPosition = new Vector3(
                statusLabelOffset.x,
                statusLabelOffset.y,
                -0.035f);

            carriedStatusText = labelObject.GetComponent<TextMesh>();
            if (carriedStatusText == null)
            {
                carriedStatusText = labelObject.AddComponent<TextMesh>();
            }

            carriedStatusText.anchor = TextAnchor.LowerLeft;
            carriedStatusText.alignment = TextAlignment.Left;
            carriedStatusText.fontSize = 64;
            carriedStatusText.characterSize = statusCharacterSize;
            carriedStatusText.fontStyle = FontStyle.Bold;
            carriedStatusText.color = statusTextColor;

            MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (labelRenderer != null && circleRenderer != null)
            {
                labelRenderer.sortingOrder = circleRenderer.sortingOrder + 1;
            }
        }

        private void CacheRequiredComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (circleRenderer == null)
            {
                circleRenderer = GetComponent<SpriteRenderer>();
            }

            if (playerState == null)
            {
                playerState = GetComponent<FoodIsekaiZPlayerState>();
            }
        }

        private void EnsurePlayerMarker()
        {
            CacheRequiredComponents();
            if (circleRenderer == null)
            {
                return;
            }

            if (generatedMarkerSprite == null)
            {
                generatedMarkerSprite = CreateEllipseSprite(
                    markerTextureWidth,
                    markerHeightRatio,
                    markerTransparentGapScale,
                    out generatedMarkerTexture);
            }

            if (generatedMarkerCenterSprite == null)
            {
                generatedMarkerCenterSprite = CreateEllipseSprite(
                    markerTextureWidth,
                    markerHeightRatio,
                    0f,
                    out generatedMarkerCenterTexture);
            }

            circleRenderer.sprite = generatedMarkerSprite;
            circleRenderer.drawMode = SpriteDrawMode.Simple;
            DisableObsoleteWhiteGap();
            markerCenterRenderer = GetOrCreateMarkerLayer(
                "MarkerCenter",
                new Vector2(markerCenterWidthScale, markerCenterHeightScale),
                circleRenderer.sortingOrder + 1,
                generatedMarkerCenterSprite);
        }

        private void DisableObsoleteWhiteGap()
        {
            Transform obsoleteGap = transform.Find("MarkerWhiteGap");
            if (obsoleteGap == null)
            {
                return;
            }

            SpriteRenderer obsoleteRenderer = obsoleteGap.GetComponent<SpriteRenderer>();
            if (obsoleteRenderer != null)
            {
                obsoleteRenderer.enabled = false;
            }

            Destroy(obsoleteGap.gameObject);
        }

        private SpriteRenderer GetOrCreateMarkerLayer(
            string objectName,
            Vector2 scale,
            int sortingOrder,
            Sprite sprite)
        {
            Transform layerTransform = transform.Find(objectName);
            GameObject layerObject;
            if (layerTransform == null)
            {
                layerObject = new GameObject(objectName);
                layerTransform = layerObject.transform;
                layerTransform.SetParent(transform, false);
            }
            else
            {
                layerObject = layerTransform.gameObject;
            }

            layerTransform.localPosition = Vector3.zero;
            layerTransform.localRotation = Quaternion.identity;
            layerTransform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = layerObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingLayerID = circleRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplyPlayerMarkerColor()
        {
            if (circleRenderer != null)
            {
                circleRenderer.color = playerColor;
            }

            if (markerCenterRenderer != null)
            {
                markerCenterRenderer.color = playerColor;
            }
        }

        private void SetPlayerMarkerVisible(bool visible)
        {
            if (circleRenderer != null)
            {
                circleRenderer.enabled = visible;
            }

            if (markerCenterRenderer != null)
            {
                markerCenterRenderer.enabled = visible;
            }
        }

        private void RefreshStatusLabel()
        {
            if (!showCarriedItems)
            {
                if (carriedStatusText != null)
                {
                    carriedStatusText.gameObject.SetActive(false);
                }

                return;
            }

            if (carriedStatusText == null)
            {
                CreateStatusLabel();
            }

            if (carriedStatusText == null)
            {
                return;
            }

            if (playerState == null)
            {
                playerState = GetComponent<FoodIsekaiZPlayerState>();
            }

            string food = playerState != null && playerState.HeldFood != FoodType.None
                ? $"F{(int)playerState.HeldFood}"
                : "--";
            int money = playerState != null ? playerState.CarriedMoney : 0;
            carriedStatusText.text = $"P{playerId}\n{food}  ${money}";
            carriedStatusText.color = statusTextColor;
            carriedStatusText.characterSize = statusCharacterSize;
            carriedStatusText.gameObject.SetActive(true);
        }

        private void FindAndRegisterManager()
        {
            if (uwbManager == null)
            {
                uwbManager = FindAnyObjectByType<UWBManager>();
            }

            if (uwbManager == null || isRegistered)
            {
                return;
            }

            uwbManager.RegisterTag(tagId);
            isRegistered = true;
        }

        private void UnregisterManager()
        {
            if (uwbManager != null && isRegistered)
            {
                uwbManager.UnregisterTag(tagId);
            }

            isRegistered = false;
        }

        private Sprite CreateEllipseSprite(
            int requestedWidth,
            float heightRatio,
            float transparentInnerScale,
            out Texture2D generatedTexture)
        {
            int width = Mathf.Clamp(requestedWidth, 32, 256);
            int height = Mathf.Max(16, Mathf.RoundToInt(width * Mathf.Clamp(heightRatio, 0.4f, 1f)));
            generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"Runtime Player Marker P{playerId}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[width * height];
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float radiusX = Mathf.Max(1f, centerX - 1f);
            float radiusY = Mathf.Max(1f, centerY - 1f);
            float edgePixels = Mathf.Min(radiusX, radiusY);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = (x - centerX) / radiusX;
                    float normalizedY = (y - centerY) / radiusY;
                    float normalizedDistance = Mathf.Sqrt(
                        (normalizedX * normalizedX) + (normalizedY * normalizedY));
                    float outerAlpha = Mathf.Clamp01((1f - normalizedDistance) * edgePixels);
                    float innerAlpha = transparentInnerScale > 0f
                        ? Mathf.Clamp01((normalizedDistance - transparentInnerScale) * edgePixels)
                        : 1f;
                    float alpha = outerAlpha * innerAlpha;
                    pixels[(y * width) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply();
            Sprite generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                width / Mathf.Max(0.1f, markerWorldWidth));
            generatedSprite.name = $"Runtime Player Marker P{playerId}";
            return generatedSprite;
        }
    }
}
