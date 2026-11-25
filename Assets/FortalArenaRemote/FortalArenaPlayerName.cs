using System;
using UnityEngine;
using UnityEngine.Events;

namespace Fortal.Arena
{
    // Shows a customer's name on a label, with no code (kit 3.3 / 2026-11-18: "ในเกมต้องเห็นเป็นชื่อผู้เล่น").
    //
    // Put it on the label that says "PLAYER 1" (or on any object), and choose who it is for:
    //   Player  = the player slot, 1 = player 1 (needs FortalArenaPlayers.SetSeats, so the kit knows whose tag is in that slot)
    //   Tag Id  = or a UWB tag number directly (wins over Player when above 0)
    // The label gets the customer's name, or Fallback ("PLAYER 1") when that slot / tag has no paying customer, and is
    // kept up to date when the Launcher's list or the seating changes.
    //   Legacy UI Text: drag it into Label.
    //   TextMeshPro:    add On Name > the label > TMP_Text.text (the "Dynamic string" entry at the top of the list).
    // From code the same thing is one line: label.text = FortalArenaPlayers.Label(slot, "PLAYER 1");
    [AddComponentMenu("Fortal/Arena Player Name")]
    public sealed class FortalArenaPlayerName : MonoBehaviour
    {
        [Tooltip("Player slot, 1 = player 1. Needs FortalArenaPlayers.SetSeats to be called by the game.")]
        [Min(1)] public int player = 1;
        [Tooltip("A UWB tag number. Above 0: the customer of this tag is shown, whatever Player says.")]
        public int tagId;
        [Tooltip("Shown when there is no customer for this player. {0} = the player number.")]
        public string fallback = "PLAYER {0}";
        [Tooltip("Longest name shown; a longer one is cut with …. 0 = no limit.")]
        [Min(0)] public int maxLength = 14;
        [Tooltip("A legacy UI Text to write the name into. TextMeshPro: use On Name instead.")]
        public UnityEngine.UI.Text label;
        public StringEvent onName = new StringEvent();

        [Serializable] public class StringEvent : UnityEvent<string> { }

        /// <summary>The name shown right now (the customer's, or the fallback).</summary>
        public string Current { get; private set; } = "";

        void OnEnable() { FortalArenaPlayers.Changed += Refresh; FortalArenaPlayers.SeatsChanged += Refresh; Refresh(); }
        void OnDisable() { FortalArenaPlayers.Changed -= Refresh; FortalArenaPlayers.SeatsChanged -= Refresh; }

        /// <summary>For a label that is reused for another player (a result row, a pooled HUD item).</summary>
        public void SetPlayer(int number) { player = Mathf.Max(1, number); tagId = 0; Refresh(); }
        public void SetTag(int tag) { tagId = tag; Refresh(); }

        public void Refresh()
        {
            string name = tagId > 0 ? FortalArenaPlayers.NameForTag(tagId) : FortalArenaPlayers.NameForSlot(player - 1);
            if (name.Length == 0) name = string.Format(fallback ?? "", player);
            else if (maxLength > 0 && name.Length > maxLength)
            {
                int cut = maxLength - 1;
                if (cut > 0 && char.IsHighSurrogate(name[cut - 1])) cut--;   // never half of an emoji
                name = name.Substring(0, cut) + "…";
            }
            if (name == Current && Application.isPlaying) return;
            Current = name;
            if (label != null) label.text = name;
            onName.Invoke(name);
        }
    }
}
