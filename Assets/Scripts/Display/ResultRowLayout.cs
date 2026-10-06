using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Arranges the result screen's player rows for one to six players, best score first:
    // 1 = one wide row; 2 = a pair side by side; 3 = one on top over a pair; 4 = two pairs;
    // 5 = one on top over two pairs; 6 = three pairs. Only one player gets the wide row; with more players
    // every row uses the narrow width so the columns fit. The MVP badge sits at the back of the top row,
    // tucked over its pointed end; the gap between the columns leaves room for it.
    // The row background must be a sliced sprite so a narrow row keeps its pointed ends.
    [AddComponentMenu("Food Isekai Z/Display/Result Row Layout")]
    public sealed class ResultRowLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform[] rows = new RectTransform[6];
        [SerializeField] private RectTransform[] rowNames = new RectTransform[6];
        [SerializeField] private RectTransform[] rowScores = new RectTransform[6];
        [SerializeField] private RectTransform mvpBadge;

        [Header("Placement (Content units)")]
        [SerializeField] private float centerX = 6f;
        [SerializeField] private float topY = -3.3f;
        [SerializeField, Min(1f)] private float rowSpacing = 26f;
        [SerializeField, Min(1f)] private float columnSpacing = 132f;

        [Header("Row Width (row units)")]
        [SerializeField, Min(100f)] private float wideWidth = 1005f;
        [SerializeField, Min(100f)] private float narrowWidth = 650f;
        // Where the name and score sit, as a share of the row's half width left and right of its center.
        // Scaling with the width keeps them apart on narrow rows the same way they sit on the wide row.
        [SerializeField, Range(0f, 1f)] private float nameOffset = 0.47f;
        [SerializeField, Range(0f, 1f)] private float scoreOffset = 0.56f;

        [Header("MVP Badge")]
        // How far the badge tucks over the back end of the top row, in Content units.
        [SerializeField] private float badgeOverlap = 8f;

        // Places the first count rows; rows past the count are left for the caller to hide.
        public void Apply(int count)
        {
            count = Mathf.Clamp(count, 0, rows.Length);
            // The badge only appears when there is a top player to attach it to.
            if (mvpBadge != null) mvpBadge.gameObject.SetActive(count > 0);
            for (int i = 0; i < count; i++)
            {
                RectTransform row = rows[i];
                if (row == null) continue;
                GetSlot(count, i, out float x, out float y, out bool wide);
                float width = wide ? wideWidth : narrowWidth;
                row.anchoredPosition = new Vector2(x, y);
                row.sizeDelta = new Vector2(width, row.sizeDelta.y);
                float half = width * 0.5f;
                if (i < rowNames.Length && rowNames[i] != null)
                    rowNames[i].anchoredPosition = new Vector2(-half * nameOffset, rowNames[i].anchoredPosition.y);
                if (i < rowScores.Length && rowScores[i] != null)
                    rowScores[i].anchoredPosition = new Vector2(half * scoreOffset, rowScores[i].anchoredPosition.y);

                if (i == 0 && mvpBadge != null)
                {
                    float rowHalf = half * Mathf.Abs(row.localScale.x);
                    float badgeHalf = mvpBadge.rect.width * 0.5f * Mathf.Abs(mvpBadge.localScale.x);
                    mvpBadge.anchoredPosition = new Vector2(x + rowHalf + badgeHalf - badgeOverlap, y);
                }
            }
        }

        private void GetSlot(int count, int index, out float x, out float y, out bool wide)
        {
            wide = count == 1;
            float left = centerX - columnSpacing * 0.5f;
            float right = centerX + columnSpacing * 0.5f;
            // Odd counts above one put the leader alone on the top line, then fill pairs below it.
            bool leaderAlone = count % 2 == 1;
            if (leaderAlone && index == 0)
            {
                x = centerX;
                y = topY;
                return;
            }
            int paired = leaderAlone ? index - 1 : index;
            int line = paired / 2 + (leaderAlone ? 1 : 0);
            x = paired % 2 == 0 ? left : right;
            y = topY - line * rowSpacing;
        }
    }
}
