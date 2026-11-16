using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // The Chef Says basket at the bottom middle of the wall. Each ingredient a player collects rises
    // in from the bottom edge on that player's side, arcs over and drops onto the pile in the basket,
    // squashing as it lands. The pile stays for the whole game and grows a little higher as it fills.
    // The pile sits between the basket's back (its inside) and its front wall and cloth, so the
    // ingredients look like they are in it and never poke out through the front or the sides.
    public sealed class ChefSaysBasket : MonoBehaviour
    {
        [SerializeField] private RectTransform pile;
        [SerializeField] private RectTransform flying;
        [SerializeField] private Image ingredientTemplate;
        // Where ingredients enter: this far left or right of the middle, below the wall's bottom edge.
        [SerializeField] private Vector2 entry = new Vector2(380f, -260f);
        [SerializeField] private float flySeconds = 0.7f;
        [SerializeField] private float arcHeight = 150f;
        // The pile's landing spots: random across this width around the basket's mouth, rising by
        // riseEach for every ingredient already in, up to maxRise.
        [SerializeField] private float pileWidth = 70f;
        [SerializeField] private float riseEach = 3f;
        [SerializeField] private float maxRise = 18f;
        [SerializeField] private float landSquash = 0.25f;
        [SerializeField] private float settleSeconds = 0.25f;

        private readonly List<Flight> flights = new List<Flight>();
        private readonly List<Settle> settling = new List<Settle>();
        private int count;

        private sealed class Flight
        {
            public Image Image;
            public Vector2 From;
            public Vector2 To;
            public float Spin;
            public float Age;
        }

        private sealed class Settle
        {
            public RectTransform Rect;
            public float Age;
        }

        // Sends one ingredient picture into the basket from the bottom edge, on the right or left.
        public void Add(Sprite ingredient, bool fromRight)
        {
            if (ingredient == null || ingredientTemplate == null) return;
            Image copy = Instantiate(ingredientTemplate, flying, false);
            copy.gameObject.SetActive(true);
            copy.sprite = ingredient;
            RectTransform spot = ingredientTemplate.rectTransform;
            float rise = Mathf.Min(count * riseEach, maxRise);
            Vector2 landing = spot.anchoredPosition + new Vector2(Random.Range(-0.5f, 0.5f) * pileWidth, rise);
            var flight = new Flight
            {
                Image = copy,
                From = new Vector2(fromRight ? entry.x : -entry.x, entry.y),
                To = landing,
                Spin = Random.Range(-25f, 25f),
            };
            count++;
            flights.Add(flight);
            Pose(flight);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                flight.Age += deltaTime;
                if (flight.Age < flySeconds)
                {
                    Pose(flight);
                    continue;
                }

                Land(flight);
                flights.RemoveAt(i);
            }

            for (int i = settling.Count - 1; i >= 0; i--)
            {
                Settle settle = settling[i];
                settle.Age += deltaTime;
                float u = Mathf.Clamp01(settle.Age / settleSeconds);
                float squash = Mathf.Sin(u * Mathf.PI) * landSquash * (1f - u);
                settle.Rect.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                if (u >= 1f) settling.RemoveAt(i);
            }
        }

        // Rises along an arc that peaks above the basket, turning and growing to full size on the way.
        private void Pose(Flight flight)
        {
            float u = Mathf.Clamp01(flight.Age / flySeconds);
            float eased = 1f - (1f - u) * (1f - u);
            Vector2 position = Vector2.Lerp(flight.From, flight.To, eased);
            position.y += Mathf.Sin(u * Mathf.PI) * arcHeight;
            RectTransform rect = flight.Image.rectTransform;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, flight.Spin * (1f - u));
            float size = Mathf.Lerp(0.6f, 1f, u);
            rect.localScale = new Vector3(size, size, 1f);
        }

        // Moves the ingredient into the basket, behind its front wall. The pile and
        // flying groups cover the same area, so positions carry over between them.
        private void Land(Flight flight)
        {
            RectTransform rect = flight.Image.rectTransform;
            rect.SetParent(pile, false);
            rect.anchoredPosition = flight.To;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
            settling.Add(new Settle { Rect = rect });
        }
    }
}
