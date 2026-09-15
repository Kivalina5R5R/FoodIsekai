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

        private void LateUpdate()
        {
            if (playerState == null)
            {
                return;
            }

            int plateIndex = Mathf.Clamp(playerState.PlayerId - 1, 0, playerPlates.Length - 1);
            for (int i = 0; i < playerPlates.Length; i++)
            {
                playerPlates[i].SetActive(i == plateIndex);
            }

            playerName.text = $"PLAYER {playerState.PlayerId}";
            int foodIndex = (int)playerState.HeldFood - 1;
            bool hasFood = foodIndex >= 0 && foodIndex < foodSprites.Length;
            foodImage.enabled = hasFood;
            if (hasFood)
            {
                foodImage.sprite = foodSprites[foodIndex];
            }

            int money = playerState.CarriedMoney;
            moneyVisual.SetActive(money > 0);
            moneyAmount.text = money > 0 ? money.ToString() : string.Empty;
        }
    }
}
