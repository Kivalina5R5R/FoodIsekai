using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Applies a small transition scale to rendered marker vertices, preserving authored transforms.
    [DisallowMultipleComponent]
    public sealed class MarkerBlendMesh : BaseMeshEffect
    {
        private float meshScale = 1f;

        public void SetBlendScale(float scale)
        {
            if (Mathf.Approximately(meshScale, scale)) return;
            meshScale = scale;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || Mathf.Approximately(meshScale, 1f)) return;
            Vector3 center = graphic.rectTransform.rect.center;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position = center + (vertex.position - center) * meshScale;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
