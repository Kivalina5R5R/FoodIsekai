using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Moves each card's background, food and timer together in mesh space, preserving scene transforms.
    [DisallowMultipleComponent]
    public sealed class OrderCardMeshEffect : BaseMeshEffect
    {
        [SerializeField] private PairedOrderPresentation presentation;
        // 0 and 1 are the pair cards, 2 the single card, and 3 to 5 the three-dish cards.
        [SerializeField, Range(0, 5)] private int card;

        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || presentation == null || !presentation.IsTransitioning) return;
            presentation.GetPose(card, out Vector3 center, out Vector3 offset, out float scale, out float alpha);
            Matrix4x4 toPanel = presentation.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromPanel = transform.worldToLocalMatrix * presentation.transform.localToWorldMatrix;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Vector3 position = toPanel.MultiplyPoint3x4(vertex.position);
                vertex.position = fromPanel.MultiplyPoint3x4(center + (position - center) * scale + offset);
                vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * alpha);
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
