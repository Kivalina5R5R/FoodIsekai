using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // Marks a part of a minigame prefab that stays switched on in the prefab, so it can be seen and
    // edited there, but is switched off when the game starts: templates the view clones, and parts
    // the view only shows later (a result, a hidden dish, the HUD counter).
    [DisallowMultipleComponent]
    public sealed class MinigameHiddenAtStart : MonoBehaviour
    {
    }
}
