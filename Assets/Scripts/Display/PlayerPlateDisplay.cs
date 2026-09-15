using FoodIsekaiZ.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Displays inventory state on the authored player plate without changing its layout.
    public sealed class PlayerPlateDisplay : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZPlayerState playerState;
        [SerializeField] private GameObject[] playerPlates;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private Image foodImage;
        [SerializeField] private Sprite[] foodSprites;
        [SerializeField] private GameObject moneyVisual;
        [SerializeField] private TMP_Text moneyAmount;

        private int displayedPlayerId = -1;
        private FoodType displayedFood = (FoodType)(-1);
        private int displayedMoney = -1;

        private void OnEnable()
        {
            displayedPlayerId = -1;
            displayedFood = (FoodType)(-1);
            displayedMoney = -1;
        }

        private void LateUpdate()
        {
            if (playerState == null)
            {
                return;
            }

            if (displayedPlayerId != playerState.PlayerId)
            {
                displayedPlayerId = playerState.PlayerId;
                int plateIndex = Mathf.Clamp(displayedPlayerId - 1, 0, playerPlates.Length - 1);
                for (int i = 0; i < playerPlates.Length; i++)
                {
                    playerPlates[i].SetActive(i == plateIndex);
                }
                playerName.text = $"PLAYER {displayedPlayerId}";
            }

            if (displayedFood != playerState.HeldFood)
            {
                displayedFood = playerState.HeldFood;
                RefreshFood();
            }

            if (displayedMoney != playerState.CarriedMoney)
            {
                displayedMoney = playerState.CarriedMoney;
                moneyVisual.SetActive(displayedMoney > 0);
                moneyAmount.text = displayedMoney > 0 ? displayedMoney.ToString() : string.Empty;
            }
        }

        private void RefreshFood()
        {
            int foodIndex = (int)displayedFood - 1;
            bool hasFood = foodIndex >= 0 && foodIndex < foodSprites.Length;
            foodImage.enabled = hasFood;
            if (hasFood)
            {
                foodImage.sprite = foodSprites[foodIndex];
            }

        }
    }
}
