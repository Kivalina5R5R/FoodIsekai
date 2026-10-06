using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Arranges the result screen's player rows for one to six players, best score first:
    // 1 = one wide row; 2 = a pair side by side; 3 = one on top over a pair; 4 = two pairs;
    // 5 = one on top over two pairs; 6 = three pairs. Only one player gets the wide row; with more players
    // every row uses the narrow width so the columns fit.
    // The rows fill the free space under the total score: fewer players get larger rows, and the lines are
    // spread evenly between the band's top and bottom, keeping a margin from the card's edges.
    // The MVP badge sits at the back of the top row, tucked over its pointed end; a top row that stands
    // alone shifts left so the row and badge stay centered together.
    // The row background must be a sliced sprite so a narrow row keeps its pointed ends.
    [AddComponentMenu("Food Isekai Z/Display/Result Row Layout")]
    public sealed class ResultRowLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform[] rows = new RectTransform[6];
        [SerializeField] private RectTransform[] rowNames = new RectTransform[6];
        [SerializeField] private RectTransform[] rowScores = new RectTransform[6];
        [SerializeField] private RectTransform mvpBadge;

        [Header("Free Space (Content units)")]
        [SerializeField] private float centerX = 6f;
        // Where a lone top row and its MVP badge are centered together. The card's own center is 0; a little
        // to its left balances the badge's strong red, which draws the eye more than the cream row.
        [SerializeField] private float loneRowCenterX = -4f;
        // The band under the total score bar and above the card's bottom edge, already inside its margins.
        [SerializeField] private float bandTop = 9f;
        [SerializeField] private float bandBottom = -84f;

        [Header("Single Player")]
        // One player keeps the authored look: a wide row right under the total score with the badge on its end.
        // The row sits left of the score bar's center so the row and its badge together sit centered under it.
        [SerializeField] private Vector2 singleRowPosition = new Vector2(-16f, -3.3f);
        [SerializeField, Min(0.01f)] private float singleRowScale = 0.15f;
        [SerializeField] private float singleBadgeOverlap = 8f;

        [Header("Row Size")]
        // Row scale for one to six players; fewer players get larger rows.
        [SerializeField] private float[] rowScaleByCount = { 0.24f, 0.18f, 0.18f, 0.18f, 0.16f, 0.16f };
        [SerializeField, Min(100f)] private float wideWidth = 1005f;
        [SerializeField, Min(100f)] private float narrowWidth = 650f;
        // Where the name and score sit, as a share of the row's half width left and right of its center.
        // Scaling with the width keeps them apart on narrow rows the same way they sit on the wide row.
        [SerializeField, Range(0f, 1f)] private float nameOffset = 0.47f;
        [SerializeField, Range(0f, 1f)] private float scoreOffset = 0.56f;

        [Header("MVP Badge")]
        // The badge grows with the rows but never past this scale.
        [SerializeField, Min(0.01f)] private float maximumBadgeScale = 0.17f;
        // How far the badge tucks over the back end of the top row, in Content units.
        [SerializeField] private float badgeOverlap = 12f;
        // Extra space between the badge and the next column, in Content units.
        [SerializeField, Min(0f)] private float badgeClearance = 6f;

        // Places the first count rows; rows past the count are left for the caller to hide.
        public void Apply(int count)
        {
            count = Mathf.Clamp(count, 0, rows.Length);
            // The badge only appears when there is a top player to attach it to.
            if (mvpBadge != null) mvpBadge.gameObject.SetActive(count > 0);
            if (count == 0) return;

            if (count == 1)
            {
                PlaceRow(0, singleRowPosition.x, singleRowPosition.y, singleRowScale, wideWidth);
                PlaceBadge(singleRowPosition.x, singleRowPosition.y, wideWidth * 0.5f * singleRowScale,
                    Mathf.Min(singleRowScale, maximumBadgeScale), singleBadgeOverlap);
                return;
            }

            float rowScale = rowScaleByCount != null && rowScaleByCount.Length > 0
                ? rowScaleByCount[Mathf.Clamp(count - 1, 0, rowScaleByCount.Length - 1)] : 0.15f;
            float badgeScale = Mathf.Min(rowScale, maximumBadgeScale);
            float badgeWidth = mvpBadge != null ? mvpBadge.rect.width * badgeScale : 0f;
            // The badge's visible part past the row end, which the column gap or the centering must allow for.
            float badgeReach = Mathf.Max(0f, badgeWidth - badgeOverlap);
            float columnSpacing = narrowWidth * rowScale + badgeReach + badgeClearance;

            int lines = (count + 1) / 2;
            float pitch = (bandTop - bandBottom) / lines;
            float firstLineY = (bandTop + bandBottom) * 0.5f + (lines - 1) * 0.5f * pitch;

            for (int i = 0; i < count; i++)
            {
                if (rows[i] == null) continue;
                GetSlot(count, i, out int line, out int column, out bool alone);
                float x = alone ? centerX : centerX + (column == 0 ? -0.5f : 0.5f) * columnSpacing;
                // A lone top row carries the badge on its right, so the row and badge are centered as one.
                if (alone && i == 0) x = loneRowCenterX - badgeReach * 0.5f;
                float y = firstLineY - line * pitch;
                PlaceRow(i, x, y, rowScale, narrowWidth);
                if (i == 0) PlaceBadge(x, y, narrowWidth * 0.5f * rowScale, badgeScale, badgeOverlap);
            }
        }

        private void PlaceRow(int index, float x, float y, float scale, float width)
        {
            RectTransform row = rows[index];
            if (row == null) return;
            row.localScale = new Vector3(scale, scale, scale);
            row.anchoredPosition = new Vector2(x, y);
            row.sizeDelta = new Vector2(width, row.sizeDelta.y);
            float half = width * 0.5f;
            if (index < rowNames.Length && rowNames[index] != null)
                rowNames[index].anchoredPosition = new Vector2(-half * nameOffset, rowNames[index].anchoredPosition.y);
            if (index < rowScores.Length && rowScores[index] != null)
                rowScores[index].anchoredPosition = new Vector2(half * scoreOffset, rowScores[index].anchoredPosition.y);
        }

        // Puts the badge on the back end of a row whose center is (x, y) and whose half width is given.
        private void PlaceBadge(float x, float y, float rowHalfWidth, float scale, float overlap)
        {
            if (mvpBadge == null) return;
            mvpBadge.localScale = new Vector3(scale, scale, scale);
            float badgeHalf = mvpBadge.rect.width * scale * 0.5f;
            mvpBadge.anchoredPosition = new Vector2(x + rowHalfWidth + badgeHalf - overlap, y);
        }

        // Odd counts put the leader alone on the top line, then fill pairs below it.
        private static void GetSlot(int count, int index, out int line, out int column, out bool alone)
        {
            bool leaderAlone = count % 2 == 1;
            alone = leaderAlone && index == 0;
            if (alone)
            {
                line = 0;
                column = 0;
                return;
            }
            int paired = leaderAlone ? index - 1 : index;
            line = paired / 2 + (leaderAlone ? 1 : 0);
            column = paired % 2;
        }
    }
}
