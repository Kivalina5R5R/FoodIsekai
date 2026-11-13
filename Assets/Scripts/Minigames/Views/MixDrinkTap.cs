using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One syrup tap on the Mix the Drink floor, centred on its play circle: a see-through pool in the
    // tap's colour, a rim in the full colour and the syrup jug picture lying in the middle.
    public sealed class MixDrinkTap : MonoBehaviour
    {
        [SerializeField] private Image pool;
        [SerializeField] private Image rim;
        [SerializeField] private Image jug;
        [SerializeField, Range(0f, 1f)] private float poolAlpha = 0.5f;

        // jugPicture may be null, which leaves the middle empty.
        public void Setup(Color tapColour, Sprite jugPicture)
        {
            Color see = tapColour;
            see.a = poolAlpha;
            pool.color = see;
            rim.color = tapColour;
            jug.enabled = jugPicture != null;
            if (jugPicture != null) jug.sprite = jugPicture;
        }
    }
}
