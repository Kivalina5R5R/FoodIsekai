using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A vector question-mark order icon; no localized text drives wildcard gameplay.
    public sealed class OmakaseOrderGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height) * .72f;
            Vector2 center = rect.center + Vector2.up * size * .1f;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= 24; i++)
            {
                float angle = Mathf.Lerp(160, -75, i / 24f) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle) * .23f, .16f + Mathf.Sin(angle) * .23f) * size;
                if (i > 0) AddStroke(mesh, previous, point, size * .075f);
                previous = point;
            }
            Vector2 stem = center + Vector2.down * size * .2f;
            AddStroke(mesh, previous, stem, size * .075f);
            AddStroke(mesh, center + Vector2.down * size * .34f,
                center + Vector2.down * size * .42f, size * .08f);
        }

        private void AddStroke(VertexHelper mesh, Vector2 start, Vector2 end, float width)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(start - normal, color, Vector2.zero);
            mesh.AddVert(start + normal, color, Vector2.zero);
            mesh.AddVert(end + normal, color, Vector2.zero);
            mesh.AddVert(end - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
