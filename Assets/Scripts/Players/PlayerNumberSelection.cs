using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Players
{
    // Owns exclusive number claims and continuous dwell timing, independent of tracking and UI.
    public sealed class PlayerNumberSelection
    {
        private readonly HashSet<int> participants;
        private readonly Dictionary<int, int> assignedNumbers = new Dictionary<int, int>();
        private readonly int?[] owners;
        private readonly int?[] candidates;
        private readonly float[] elapsed;
        private readonly float holdSeconds;

        public bool IsComplete => assignedNumbers.Count == participants.Count;

        public PlayerNumberSelection(IEnumerable<int> tagIds, int numberCount, float holdSeconds)
        {
            if (numberCount < 1 || holdSeconds <= 0f) throw new ArgumentOutOfRangeException();
            participants = new HashSet<int>();
            foreach (int tag in tagIds)
            {
                if (tag < 0 || !participants.Add(tag))
                    throw new ArgumentException("Ready phase requires distinct, non-negative tag IDs.");
            }
            if (participants.Count == 0 || participants.Count > numberCount)
                throw new ArgumentException("Ready phase requires one available number per participant.");
            owners = new int?[numberCount];
            candidates = new int?[numberCount];
            elapsed = new float[numberCount];
            this.holdSeconds = holdSeconds;
        }

        public bool HasAssignedNumber(int tagId) => assignedNumbers.ContainsKey(tagId);
        public int? GetOwner(int playerNumber) => owners[GetIndex(playerNumber)];
        public float GetProgress(int playerNumber) => elapsed[GetIndex(playerNumber)] / holdSeconds;

        // Pass null when the zone is empty, contested, or its tag is offline to reset the hold.
        // Returns true only on the frame when a new number is claimed.
        public bool TickNumber(int playerNumber, int? soleTagId, float deltaSeconds)
        {
            int index = GetIndex(playerNumber);
            if (owners[index].HasValue) return false;
            if (!soleTagId.HasValue || !participants.Contains(soleTagId.Value) ||
                HasAssignedNumber(soleTagId.Value))
            {
                candidates[index] = null;
                elapsed[index] = 0f;
                return false;
            }
            if (candidates[index] != soleTagId)
            {
                candidates[index] = soleTagId;
                elapsed[index] = 0f;
                return false;
            }
            elapsed[index] = Math.Min(holdSeconds, elapsed[index] + Math.Max(0f, deltaSeconds));
            if (elapsed[index] < holdSeconds) return false;
            owners[index] = soleTagId;
            assignedNumbers.Add(soleTagId.Value, playerNumber);
            return true;
        }

        private int GetIndex(int playerNumber)
        {
            if (playerNumber < 1 || playerNumber > owners.Length)
                throw new ArgumentOutOfRangeException(nameof(playerNumber));
            return playerNumber - 1;
        }
    }
}
