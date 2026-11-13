using FoodIsekaiZ.Display;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Chef Says on the wall and floor (prefab Assets/Prefab/Minigame/ChefSays).
    // Wall: an arrow on each side with its word, the chef with a small arrow on her board, and the
    // countdown gauge (the same gilded gauge the players fill when they get ready), which stays where
    // the prefab puts it. The called arrow shows at its authored colour, the other fades to
    // ghostAlpha and loses its word.
    // Floor: one ChefSaysFloorMark per player, cloned from the template: its moving chevrons during a
    // call, then that player's own right or wrong mark after it.
    public sealed class ChefSaysView : MinigameView
    {
        [SerializeField] private Image leftArrow;
        [SerializeField] private Image rightArrow;
        [SerializeField] private TMP_Text leftWord;
        [SerializeField] private TMP_Text rightWord;
        [SerializeField] private Image signArrow;
        [SerializeField] private ReadyConfirmationGauge callGauge;
        [SerializeField] private ChefSaysFloorMark markTemplate;
        [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.1f;

        private Color leftColour;
        private Color rightColour;

        protected override void OnBound()
        {
            leftColour = leftArrow.color;
            rightColour = rightArrow.color;
            Quiet();
        }

        public void SetWords(string left, string right)
        {
            leftWord.text = left;
            rightWord.text = right;
        }

        // Lights the called side, flips the chef's arrow to match and shows the full gauge.
        public void ShowCall(bool right)
        {
            Light(!right, right);
            Vector3 scale = signArrow.rectTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
            signArrow.rectTransform.localScale = scale;
            signArrow.enabled = true;
            callGauge.gameObject.SetActive(true);
            SetBar(1f);
        }

        // Between calls: both arrows faded, no gauge, nothing on the chef's board.
        public void Quiet()
        {
            Light(false, false);
            callGauge.gameObject.SetActive(false);
            signArrow.enabled = false;
        }

        // share runs 1 -> 0 as the call runs out.
        public void SetBar(float share)
        {
            callGauge.ShowProgress(share, false, false);
        }

        // A floor mark for one player, with chevrons made for up to maxReach metres.
        public ChefSaysFloorMark AddMark(float maxReach)
        {
            ChefSaysFloorMark mark = Spawn(markTemplate);
            mark.Setup(maxReach * MinigameStage.UnitsPerMetre);
            return mark;
        }

        // Moves mark to arena point here and points its chevrons the called way for reach metres.
        public void PointMark(ChefSaysFloorMark mark, Vector2 here, bool right, float reach)
        {
            PlaceOnFloor((RectTransform)mark.transform, here);
            mark.Point(right, reach * MinigameStage.UnitsPerMetre);
        }

        // Moves mark to where the player stands now (arena point here) and shows their result there.
        public void ShowResult(ChefSaysFloorMark mark, Vector2 here, bool correct)
        {
            PlaceOnFloor((RectTransform)mark.transform, here);
            mark.ShowResult(correct);
        }

        private void Light(bool left, bool right)
        {
            SetLit(leftArrow, leftWord, leftColour, left);
            SetLit(rightArrow, rightWord, rightColour, right);
        }

        private void SetLit(Image arrow, TMP_Text word, Color authored, bool lit)
        {
            Color colour = authored;
            if (!lit) colour.a *= ghostAlpha;
            arrow.color = colour;
            word.enabled = lit;
        }
    }
}
