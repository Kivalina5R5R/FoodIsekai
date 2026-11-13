using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // Perfect Grill on the wall and floor (prefab Assets/Prefab/Minigame/Grill).
    // Wall: a grill across the bottom, a status line on the left, and one GrillSteakColumn per player
    // cloned from the template and spread across the grill.
    // Floor: the grill strip players stand on; the game moves it onto its zone.
    public sealed class GrillView : MinigameView
    {
        [SerializeField] private RectTransform floorGrill;
        [SerializeField] private TMP_Text status;
        [SerializeField] private GrillSteakColumn columnTemplate;

        // Centres the floor grill on its zone (arena metres).
        public void PlaceGrill(Vector2 arenaCentre)
        {
            PlaceOnFloor(floorGrill, arenaCentre);
        }

        public void SetStatus(string words)
        {
            status.text = words;
        }

        // A player's column centred x units from the middle of the wall; see GrillSteakColumn.Setup.
        public GrillSteakColumn AddSteak(MinigamePlayer player, string orderName, float x, float barWidth, float windowLower, float windowUpper)
        {
            GrillSteakColumn column = Spawn(columnTemplate);
            var rect = (RectTransform)column.transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            column.Setup(player, orderName, barWidth, windowLower, windowUpper);
            return column;
        }
    }
}
