using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws a destination pointer around the authored plate without moving its UI hierarchy.
    public sealed class PlayerDeliveryArrow : MaskableGraphic
    {
        [SerializeField] private FoodIsekaiZPlayerState playerState;
        [SerializeField, Min(0f)] private float plateClearance = 100f;
        [SerializeField, Min(1f)] private float arrowLength = 56f;
        [SerializeField, Min(1f)] private float arrowWidth = 42f;
        [SerializeField, Min(0f)] private float outlineWidth = 4f;
        [SerializeField] private Color outlineColor = new Color(0.15f, 0.08f, 0.03f, 1f);
        [SerializeField] private Color trimColor = new Color(0.8f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color sealColor = new Color(0.65f, 0.16f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float pulseDistance = 5f;

        private static readonly Vector2[] ArrowContour =
        {
            new Vector2(0f, 0.5f), new Vector2(-0.5f, 0.08f),
            new Vector2(-0.5f, -0.5f), new Vector2(0f, -0.38f),
            new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.08f)
        };

        private FoodIsekaiZGameManager gameManager;
        private Vector2 direction;
        private bool hasTarget;

        protected override void OnEnable()
        {
            base.OnEnable();
            gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            hasTarget = false;
        }

        private void LateUpdate()
        {
            bool wasVisible = hasTarget;
            hasTarget = false;
            if (gameManager != null && gameManager.TryGetDeliveryTarget(playerState, out ArenaSlot2D target))
            {
                Vector3 offset = target.transform.position - playerState.transform.position;
                offset.y = 0f;
                Vector3 localDirection = rectTransform.InverseTransformVector(offset);
                direction = new Vector2(localDirection.x, localDirection.y);
                hasTarget = direction.sqrMagnitude > 0.001f;
                if (hasTarget) direction.Normalize();
            }
            if (hasTarget || wasVisible) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!hasTarget) return;
            float pulse = pulseDistance * (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f));
            Vector2 center = rectTransform.rect.center + direction * (plateClearance + arrowLength * 0.5f + pulse);
            AddArrow(mesh, center, arrowLength + outlineWidth * 2f, arrowWidth + outlineWidth * 2f, outlineColor);
            AddArrow(mesh, center, arrowLength, arrowWidth, trimColor);
            AddArrow(mesh, center, arrowLength * 0.82f, arrowWidth * 0.76f, color);
            Vector2 sealCenter = center - direction * (arrowLength * 0.12f);
            AddSeal(mesh, sealCenter, arrowWidth * 0.15f, trimColor);
            AddSeal(mesh, sealCenter, arrowWidth * 0.095f, sealColor);
        }

        // Keep the original ribbon silhouette, with balanced shoulders and a shallow split tail.
        private void AddArrow(VertexHelper mesh, Vector2 center, float length, float width, Color tint)
        {
            Vector2 right = new Vector2(direction.y, -direction.x);
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            const int cornerSteps = 5;
            for (int i = 0; i < ArrowContour.Length; i++)
            {
                Vector2 corner = ArrowContour[i];
                Vector2 previous = ArrowContour[(i + ArrowContour.Length - 1) % ArrowContour.Length];
                Vector2 next = ArrowContour[(i + 1) % ArrowContour.Length];
                float rounding = i == 0 ? 0.06f : 0.1f;
                Vector2 start = Vector2.Lerp(corner, previous, rounding);
                Vector2 end = Vector2.Lerp(corner, next, rounding);
                for (int step = 0; step <= cornerSteps; step++)
                {
                    float t = step / (float)cornerSteps;
                    Vector2 point = (1f - t) * (1f - t) * start +
                        2f * (1f - t) * t * corner + t * t * end;
                    mesh.AddVert(center + right * (point.x * width) + direction * (point.y * length),
                        tint, Vector2.zero);
                }
            }
            int contourCount = ArrowContour.Length * (cornerSteps + 1);
            for (int i = 0; i < contourCount; i++)
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % contourCount + 1);
        }

        private void AddSeal(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            Vector2 right = new Vector2(direction.y, -direction.x);
            int first = mesh.currentVertCount;
            mesh.AddVert(center + direction * radius, tint, Vector2.zero);
            mesh.AddVert(center - right * radius, tint, Vector2.zero);
            mesh.AddVert(center - direction * radius, tint, Vector2.zero);
            mesh.AddVert(center + right * radius, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
