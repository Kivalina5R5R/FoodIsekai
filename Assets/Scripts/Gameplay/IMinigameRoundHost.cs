using System;

namespace FoodIsekaiZ.Gameplay
{
    // Plays a round of minigames after a meal. The game manager starts a round once the meal's last
    // customer has gone. The host must invoke the finished action exactly once, when the round's
    // tally is over; the game then moves on to the perk break (or the results after the last meal).
    public interface IMinigameRoundHost
    {
        // roundIndex counts from 0 (after the first meal); roundCount is how many meals there are.
        void BeginRound(int roundIndex, int roundCount, Action finished);
    }
}
