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
        [SerializeField, Min(1f)] private float arrowLength = 48f;
        [SerializeField, Min(1f)] private float arrowWidth = 34f;
        [SerializeField, Min(0f)] private float outlineWidth = 4f;
        [SerializeField] private Color outlineColor = new Color(0.15f, 0.08f, 0.03f, 1f);
        [SerializeField] private Color[] playerBorderColors;
        [SerializeField, Min(0f)] private float trimWidth = 1.2f;
        [SerializeField] private Color trimColor = new Color(0.8f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color sealColor = new Color(0.65f, 0.16f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float pulseDistance = 6f;
        [SerializeField, Min(0.2f)] private float pulseSeconds = 1.4f;
        [SerializeField, Range(1f, 1.5f)] private float pulseScale = 1.08f;
        [SerializeField, Min(0f)] private float glowWidth = 2.5f;
        [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.6f, 0.35f);

        private static readonly Vector2[] ArrowContour =
        {
            new Vector2(0f, 0.5f), new Vector2(-0.5f, 0.08f),
            new Vector2(-0.5f, -0.5f), new Vector2(0f, -0.38f),
            new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.08f)
        };

        private FoodIsekaiZGameManager gameManager;
        private Vector2 direction;
        private bool hasTarget;
        private const int CornerSteps = 5;
        private readonly Vector2[] roundedContour = new Vector2[36];

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
            float phase = Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.2f, pulseSeconds), 1f);
            float pulse = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float scale = Mathf.Lerp(1f, pulseScale, pulse);
            float length = arrowLength * scale;
            float width = arrowWidth * scale;
            Vector2 center = rectTransform.rect.center + direction *
                (plateClearance + length * 0.5f + pulseDistance * pulse);

            // Animate mesh vertices and tints only; authored transforms and Graphic colors stay untouched.
            Color glow = glowColor;
            glow.a *= Mathf.Lerp(0.35f, 1f, pulse);
            int playerIndex = playerState != null ? playerState.PlayerId - 1 : -1;
            Color border = playerBorderColors != null && playerIndex >= 0 && playerIndex < playerBorderColors.Length
                ? playerBorderColors[playerIndex] : outlineColor;
            BuildContour(length, width);
            AddArrow(mesh, center, outlineWidth + trimWidth + glowWidth, glow);
            AddArrow(mesh, center, outlineWidth + trimWidth, trimColor);
            AddArrow(mesh, center, outlineWidth, border);
            AddArrow(mesh, center, 0f, color);
            Vector2 sealCenter = center - direction * (length * 0.12f);
            AddSeal(mesh, sealCenter, width * 0.15f, trimColor);
            AddSeal(mesh, sealCenter, width * 0.095f, sealColor);
        }

        // Keep the original ribbon silhouette, with balanced shoulders and a shallow split tail.
        private void BuildContour(float length, float width)
        {
            for (int i = 0; i < ArrowContour.Length; i++)
            {
                Vector2 corner = ArrowContour[i];
                Vector2 previous = ArrowContour[(i + ArrowContour.Length - 1) % ArrowContour.Length];
                Vector2 next = ArrowContour[(i + 1) % ArrowContour.Length];
                float rounding = i == 0 ? 0.06f : 0.1f;
                Vector2 start = Vector2.Lerp(corner, previous, rounding);
                Vector2 end = Vector2.Lerp(corner, next, rounding);
                for (int step = 0; step <= CornerSteps; step++)
                {
                    float t = step / (float)CornerSteps;
                    Vector2 point = (1f - t) * (1f - t) * start +
                        2f * (1f - t) * t * corner + t * t * end;
                    roundedContour[i * (CornerSteps + 1) + step] = new Vector2(point.x * width, point.y * length);
                }
            }
        }

        // Offset parallel edges by a fixed distance instead of scaling the silhouette.
        // Intersecting adjacent offset edges keeps the tip and recessed tail equally thick.
        private Vector2 OffsetPoint(int index, float distance)
        {
            int count = roundedContour.Length;
            Vector2 point = roundedContour[index];
            Vector2 incoming = (point - roundedContour[(index + count - 1) % count]).normalized;
            Vector2 outgoing = (roundedContour[(index + 1) % count] - point).normalized;
            Vector2 incomingNormal = new Vector2(incoming.y, -incoming.x);
            Vector2 outgoingNormal = new Vector2(outgoing.y, -outgoing.x);
            Vector2 bisector = incomingNormal + outgoingNormal;
            float denominator = Vector2.Dot(bisector, outgoingNormal);
            return point + bisector * (distance / Mathf.Max(0.001f, denominator));
        }

        private void AddArrow(VertexHelper mesh, Vector2 center, float offset, Color tint)
        {
            Vector2 right = new Vector2(direction.y, -direction.x);
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < roundedContour.Length; i++)
            {
                Vector2 point = OffsetPoint(i, offset);
                mesh.AddVert(center + right * point.x + direction * point.y, tint, Vector2.zero);
            }
            for (int i = 0; i < roundedContour.Length; i++)
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % roundedContour.Length + 1);
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
