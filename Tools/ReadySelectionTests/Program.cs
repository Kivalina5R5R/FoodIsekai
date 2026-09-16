using System;
using FoodIsekaiZ.Players;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private static void Main()
    {
        var selection = new PlayerNumberSelection(new[] { 6, 7, 8, 9 }, 4, 2f);
        selection.TickNumber(4, 6, 0f);
        Check(!selection.TickNumber(4, 6, 1.5f), "A partial hold does not bind Tag 6.");
        selection.TickNumber(4, null, .1f);
        Check(selection.GetProgress(4) == 0f, "Leaving, disconnecting or contesting a zone resets its hold.");
        selection.TickNumber(4, 6, 0f);
        Check(!selection.TickNumber(4, 6, 1.5f), "Returning must begin a fresh two-second hold.");
        Check(selection.TickNumber(4, 6, .5f) && selection.GetOwner(4) == 6,
            "Tag 6 freely claims Player 4 after two continuous seconds.");
        Check(!selection.IsComplete, "One selection cannot start a four-player session.");
        Check(!selection.TickNumber(4, 7, 3f) && selection.GetOwner(4) == 6,
            "A claimed number cannot be stolen.");
        Check(!selection.TickNumber(1, 6, 3f) && selection.GetOwner(1) == null,
            "One tag cannot claim two numbers.");
        selection.TickNumber(1, 7, 0f);
        selection.TickNumber(1, 7, 1.5f);
        Check(!selection.TickNumber(1, 8, .5f) && selection.GetProgress(1) == 0f,
            "A replacement tag cannot inherit another tag's progress.");
        Check(!selection.TickNumber(1, 123, 10f) && selection.GetOwner(1) == null,
            "Tags outside the configured participants cannot claim a number.");
        foreach (var pair in new[] { (Number: 1, Tag: 9), (Number: 2, Tag: 7), (Number: 3, Tag: 8) })
        {
            selection.TickNumber(pair.Number, pair.Tag, 0f);
            Check(selection.TickNumber(pair.Number, pair.Tag, 2f), $"Tag {pair.Tag} claims Player {pair.Number}.");
        }
        Check(selection.IsComplete, "Gameplay unlocks only after every configured tag has a distinct number.");
        bool rejectedDuplicates = false;
        try { _ = new PlayerNumberSelection(new[] { 6, 6 }, 4, 2f); }
        catch (ArgumentException) { rejectedDuplicates = true; }
        Check(rejectedDuplicates, "Duplicate tag configuration fails instead of completing early.");
        bool rejectedEmpty = false;
        try { _ = new PlayerNumberSelection(Array.Empty<int>(), 4, 2f); }
        catch (ArgumentException) { rejectedEmpty = true; }
        Check(rejectedEmpty, "An empty participant list cannot auto-start the game.");
        var twoPlayers = new PlayerNumberSelection(new[] { 6, 9 }, 2, 2f);
        twoPlayers.TickNumber(2, 9, 0f);
        twoPlayers.TickNumber(2, 9, 2f);
        Check(!twoPlayers.IsComplete, "A two-tag live round waits for its second participant.");
        twoPlayers.TickNumber(1, 6, 0f);
        twoPlayers.TickNumber(1, 6, 2f);
        Check(twoPlayers.IsComplete, "Two online tags can start without absent configured tags 7 and 8.");
        var solo = new PlayerNumberSelection(new[] { 8 }, 1, 2f);
        solo.TickNumber(1, 8, 0f);
        solo.TickNumber(1, 8, 2f);
        Check(solo.IsComplete, "A single online tag can ready and start a solo round.");
    }
}
