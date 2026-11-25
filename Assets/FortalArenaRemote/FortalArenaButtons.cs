using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fortal.Arena
{
    // The game's own buttons on the arena web launcher (kit 3.2 / 2026-11-18).
    //
    // The Launcher's game screen shows the buttons the game reports, and the admin presses them there. START matters
    // most: the Launcher takes each player's credit when START is pressed, and only then passes the press to the game.
    // So every game needs a START button here, and the game must not start a paid round any other way at the arena.
    //
    //   FortalArenaButtons.AddStart(StartRound, () => state == GameState.Menu);          // START, shown only at the menu
    //   FortalArenaButtons.Add("mode", () => "MODE: " + mode, NextMode, () => state == GameState.Menu);
    //   FortalArenaButtons.Remove("mode");
    //
    // No code: FortalArenaRemoteBridge > Start Button (a UI Button of the game; the Launcher's press clicks it).
    //
    // A button is reported while its "visible" answers true (no "visible" = always). The Launcher knows a START button
    // by its key starting with "start" or its label holding "start" / "เริ่ม" (so "RESTART" counts as a new, paid round).
    // Everything is called on the main thread.
    //
    //   in   RMOTpress {"id":"a1b2","key":"start","label":"START"}   one id = one press; a resend of the same id is ignored
    //   out  in the report: "buttons":[{"key":"start","label":"START","primary":true}], "press":{"id":"a1b2","ok":true}
    public static class FortalArenaButtons
    {
        sealed class Entry { public string key; public Func<string> label; public Action press; public Func<bool> visible; public bool primary; }

        static readonly List<Entry> entries = new List<Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { entries.Clear(); }

        /// <summary>The START button: key "start", label "START", the loud one on the Launcher.</summary>
        public static void AddStart(Action onPress, Func<bool> visible = null, string label = "START")
        {
            Add("start", () => label, onPress, visible, true);
        }

        /// <summary>Add (or replace) a button. label is asked every report, so it may change ("MODE: 2v2").</summary>
        public static void Add(string key, Func<string> label, Action onPress, Func<bool> visible = null, bool primary = false)
        {
            if (string.IsNullOrEmpty(key) || onPress == null) return;
            Remove(key);
            entries.Add(new Entry { key = key, label = label ?? (() => key.ToUpperInvariant()), press = onPress, visible = visible, primary = primary });
            FortalArenaRemote.ReportNow();
        }

        public static void Remove(string key)
        {
            if (entries.RemoveAll(e => e.key == key) > 0) FortalArenaRemote.ReportNow();
        }

        static bool Visible(Entry e)
        {
            try { return e.visible == null || e.visible(); }
            catch (Exception ex) { Debug.LogException(ex); return false; }
        }

        static string Label(Entry e)
        {
            try { return e.label() ?? ""; }
            catch (Exception ex) { Debug.LogException(ex); return e.key; }
        }

        // The page presses what it SAW: if the button's words changed since (PLAY became RESUME), the press is refused and
        // the page shows the new button. Only the words count, so "READY 3" pressed as "READY 2" is the same button.
        static string Words(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text ?? "") if (char.IsLetter(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        /// <summary>Called by FortalArenaRemote for "press". false = that button is not there (any more).</summary>
        public static bool Press(string key, string label)
        {
            foreach (var e in entries)
            {
                if (e.key != key || !Visible(e)) continue;
                if (!string.IsNullOrEmpty(label) && Words(Label(e)) != Words(label)) return false;
                try { e.press(); } catch (Exception ex) { Debug.LogException(ex); return false; }
                return true;
            }
            return false;
        }

        public static string ReportJson()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            // loud ones first, the order the Launcher draws them in
            for (int pass = 0; pass < 2; pass++)
                foreach (var e in entries)
                {
                    if (e.primary != (pass == 0) || !Visible(e)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"key\":\"").Append(Escape(e.key)).Append("\",\"label\":\"").Append(Escape(Label(e)))
                      .Append("\",\"primary\":").Append(e.primary ? "true" : "false").Append('}');
                }
            return sb.Append(']').ToString();
        }

        internal static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
