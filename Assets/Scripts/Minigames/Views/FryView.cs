using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // Fry It Golden on the wall and floor (prefab Assets/Prefab/Minigame/Fry).
    // Wall: the status words on the left, the pan in the middle holding one FryPieceSlot per player
    // (up to `columns` a row, spaced by pieceSpacing around pieceOffset), and on the right a guide of
    // the three stages to step out on.
    // Floor: the pan picture alone. Its child panZone marks the oil inside the pan (not the iron rim
    // or handles); the game reads the frying zone from it, so moving or resizing the pan in the
    // prefab moves the zone with it.
    public sealed class FryView : MinigameView
    {
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text guideTitle;
        [SerializeField] private TMP_Text guideRaw;
        [SerializeField] private TMP_Text guideGolden;
        [SerializeField] private TMP_Text guideBurnt;
        [SerializeField] private FryPieceSlot pieceTemplate;
        [SerializeField] private int columns = 3;
        [SerializeField] private Vector2 pieceSpacing = new Vector2(120f, 140f);
        [SerializeField] private Vector2 pieceOffset = new Vector2(-20f, -10f);
        [SerializeField] private RectTransform panZone;

        // The oil inside the floor pan as an ellipse in arena metres: centre and half-widths.
        public Vector2 PanCentre { get; private set; }
        public Vector2 PanRadii { get; private set; }

        protected override void OnBound()
        {
            // The zone's corners on the floor canvas, turned into arena metres.
            var corners = new Vector3[4];
            panZone.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 arena = Stage.ArenaPoint(Floor.InverseTransformPoint(corner));
                min = Vector2.Min(min, arena);
                max = Vector2.Max(max, arena);
            }

            PanCentre = (min + max) * 0.5f;
            PanRadii = (max - min) * 0.5f;
        }

        public void SetStatus(string words)
        {
            status.text = words;
        }

        public void SetGuideWords(string title, string raw, string golden, string burnt)
        {
            guideTitle.text = title;
            guideRaw.text = raw;
            guideGolden.text = golden;
            guideBurnt.text = burnt;
        }

        // The piece for the player at index out of count players, laid out in rows in the pan.
        public FryPieceSlot AddPiece(int index, int count, MinigamePlayer player)
        {
            int perRow = Mathf.Min(columns, Mathf.Max(1, count));
            int rows = (count + perRow - 1) / perRow;
            int row = index / perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);
            float x = (index % perRow - (inRow - 1) * 0.5f) * pieceSpacing.x + pieceOffset.x;
            float y = pieceOffset.y + ((rows - 1) * 0.5f - row) * pieceSpacing.y;

            FryPieceSlot piece = Spawn(pieceTemplate);
            ((RectTransform)piece.transform).anchoredPosition = new Vector2(x, y);
            piece.Setup(player);
            return piece;
        }
    }
}
