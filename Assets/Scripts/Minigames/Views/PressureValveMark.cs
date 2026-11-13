using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One valve on the Pressure Pot floor, centred on the valve: a coloured disc that shows who is
    // turning it, its rim, the brass wheel lying on it and a numbered chip at its lower edge.
    // The disc is idleColour while shut, leans towards the turning player's colour as they hold,
    // goes openedColour once safely opened and boomColour when it blew the pot.
    public sealed class PressureValveMark : MonoBehaviour
    {
        [SerializeField] private Image disc;
        [SerializeField] private TMP_Text number;
        [SerializeField] private Color idleColour = new Color(0.72f, 0.74f, 0.78f, 1f);
        [SerializeField] private Color openedColour = new Color(0.32f, 0.32f, 0.34f, 0.85f);
        [SerializeField] private Color boomColour = new Color(0.9f, 0.27f, 0.2f, 1f);
        // How far the disc leans to the player's colour when they first step on, before the hold fills it.
        [SerializeField, Range(0f, 1f)] private float firstLean = 0.3f;

        public void Setup(int valveNumber)
        {
            number.text = valveNumber.ToString();
            ShowIdle();
        }

        public void ShowIdle()
        {
            disc.color = idleColour;
        }

        // progress runs 0 -> 1 as the player holds still on it.
        public void ShowHeld(Color playerColour, float progress)
        {
            disc.color = Color.Lerp(idleColour, playerColour, firstLean + (1f - firstLean) * progress);
        }

        public void ShowOpened()
        {
            disc.color = openedColour;
        }

        public void ShowBoom()
        {
            disc.color = boomColour;
        }
    }
}
