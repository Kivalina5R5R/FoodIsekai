using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fortal.Arena
{
    // Player profiles from the arena web launcher (kit 3.1 / 2026-11-18).
    //
    // At the arena the admin adds each customer on the Launcher page: the customer scans their own Fortal ID QR (and pays
    // with credit), and the admin picks the NUMBER OF THE UWB TAG that customer carries. The Launcher sends that list to
    // the game, and shows what the game answers: who is in which player slot, and each player's score after a round.
    //
    //   in   RMOTarena_players {"list":[{"tag":7,"name":"ภูมิ","id":"FTL-ABCD-EFGH"}, ...]}
    //        the whole list every time (a state, so a resend changes nothing); an empty list = nobody
    //   out  in the game's report: "players":{"roster":2,
    //            "seats":[{"slot":0,"tag":7,"name":"ภูมิ","id":"FTL-ABCD-EFGH"}],
    //            "result":{"round":"3","reason":"completed","rows":[{"slot":0,"tag":7,"name":"ภูมิ","id":"FTL-ABCD-EFGH","score":1820,"win":true}]}}
    //
    // What a game does (all on the main thread):
    //   1. show names:  no code: the FortalArenaPlayerName component on a label (kit 3.3)
    //                   code:    label.text = FortalArenaPlayers.Label(slot, "PLAYER 1");   // or LabelForTag(tagId, ...)
    //                   FortalArenaPlayers.NameForTag(tagId)  ("" = that tag is not a paying customer)
    //                   or listen to FortalArenaPlayers.Changed and read FortalArenaPlayers.Players
    //   2. say who is where (optional, shows P1 / P2 on the Launcher):  FortalArenaPlayers.SetSeats(new[] { 7, 9 })   // tag of player 1, player 2, ...
    //   3. send the scores when a round ends:
    //        FortalArenaPlayers.ReportScores(new[] { 1820, 40 }, winners: new[] { 0 });           // by player slot (needs SetSeats)
    //        FortalArenaPlayers.ReportScoresByTag(new[] { 7, 9 }, new[] { 1820, 40 }, new[] { true, false });   // or by tag
    //
    // "id" is the customer's Fortal ID friend code (FTL-XXXX-XXXX): public, stable, safe to keep with a score.
    // The game never talks to Fortal ID and never sees credit: the Launcher does the paying.
    public static class FortalArenaPlayers
    {
        [Serializable] public class Player { public int tag; public string name; public string id; }
        [Serializable] public class Message { public Player[] list; }

        static readonly List<Player> players = new List<Player>();
        static readonly Dictionary<int, Player> byTag = new Dictionary<int, Player>();
        static int[] seats = new int[0];
        static string result = "";
        static int rounds;

        /// <summary>The customers the admin added, in the order they were added.</summary>
        public static IReadOnlyList<Player> Players { get { return players; } }
        /// <summary>Fired on the main thread when the list from the Launcher changes.</summary>
        public static event Action Changed;
        /// <summary>Fired on the main thread when SetSeats changes who plays in which slot.</summary>
        public static event Action SeatsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Changed = null; SeatsChanged = null; players.Clear(); byTag.Clear(); seats = new int[0]; result = ""; rounds = 0; }

        /// <summary>Called by FortalArenaRemote when "arena_players" arrives.</summary>
        public static void Set(Message message)
        {
            if (message == null) return;
            var before = Signature();
            players.Clear(); byTag.Clear();
            if (message.list != null)
                foreach (var p in message.list)
                    if (p != null && p.tag > 0 && !byTag.ContainsKey(p.tag)) { p.name = Clean(p.name); players.Add(p); byTag[p.tag] = p; }
            if (Signature() == before) return;   // a resend: nothing changed
            var changed = Changed;
            if (changed != null) { try { changed(); } catch (Exception e) { Debug.LogException(e); } }
        }

        static string Signature()
        {
            var sb = new StringBuilder();
            foreach (var p in players) sb.Append(p.tag).Append('|').Append(p.name).Append('|').Append(p.id).Append(';');
            return sb.ToString();
        }

        // A name is typed by the customer and often goes into a rich-text label: no tags, no line breaks.
        static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim()) sb.Append(c == '<' ? '‹' : c == '>' ? '›' : c < ' ' ? ' ' : c);
            return sb.ToString();
        }

        /// <summary>What to write on a player's label: the customer's name for that slot (0 = player 1), or the fallback
        /// ("PLAYER 1") when nobody paid for that slot's tag. Needs SetSeats.</summary>
        public static string Label(int slot, string fallback) { var name = NameForSlot(slot); return name.Length > 0 ? name : fallback; }
        /// <summary>The same by UWB tag number.</summary>
        public static string LabelForTag(int tag, string fallback) { var name = NameForTag(tag); return name.Length > 0 ? name : fallback; }

        public static bool TryGetByTag(int tag, out Player player) { return byTag.TryGetValue(tag, out player); }
        /// <summary>The customer's name for a UWB tag, "" when that tag is not on the admin's list.</summary>
        public static string NameForTag(int tag) { Player p; return byTag.TryGetValue(tag, out p) ? p.name ?? "" : ""; }
        /// <summary>The customer's name for a player slot (0 = player 1), after SetSeats. "" = unknown.</summary>
        public static string NameForSlot(int slot) { return slot >= 0 && slot < seats.Length ? NameForTag(seats[slot]) : ""; }

        /// <summary>Which tag plays in each slot: tagBySlot[0] = player 1's tag (0 or -1 = empty). Call when it changes.</summary>
        public static void SetSeats(int[] tagBySlot)
        {
            seats = tagBySlot == null ? new int[0] : (int[])tagBySlot.Clone();
            FortalArenaRemote.ReportNow();
            var changed = SeatsChanged;
            if (changed != null) { try { changed(); } catch (Exception e) { Debug.LogException(e); } }
        }

        /// <summary>A round ended: scores by player slot (uses SetSeats for the tags), winners = winning slots.</summary>
        public static void ReportScores(int[] scoresBySlot, int[] winners = null, string reason = "completed")
        {
            if (scoresBySlot == null || scoresBySlot.Length == 0) return;
            var tags = new int[scoresBySlot.Length];
            var wins = new bool[scoresBySlot.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                tags[i] = i < seats.Length ? seats[i] : -1;
                wins[i] = winners != null && Array.IndexOf(winners, i) >= 0;
            }
            Write(tags, scoresBySlot, wins, reason);
        }

        /// <summary>A round ended: one score per tag (wins may be null).</summary>
        public static void ReportScoresByTag(int[] tags, int[] scores, bool[] wins = null, string reason = "completed")
        {
            if (tags == null || scores == null || tags.Length == 0 || tags.Length != scores.Length) return;
            Write(tags, scores, wins, reason);
        }

        static void Write(int[] tags, int[] scores, bool[] wins, string reason)
        {
            rounds++;
            var sb = new StringBuilder(256);
            sb.Append("{\"round\":\"").Append(rounds).Append("\",\"reason\":\"").Append(Escape(reason)).Append("\",\"rows\":[");
            for (int i = 0; i < tags.Length; i++)
            {
                Player p; byTag.TryGetValue(tags[i], out p);
                if (i > 0) sb.Append(',');
                sb.Append("{\"slot\":").Append(i).Append(",\"tag\":").Append(tags[i])
                  .Append(",\"name\":\"").Append(Escape(p != null ? p.name : "")).Append("\",\"id\":\"").Append(Escape(p != null ? p.id : ""))
                  .Append("\",\"score\":").Append(scores[i])
                  .Append(",\"win\":").Append(wins != null && i < wins.Length && wins[i] ? "true" : "false").Append('}');
            }
            result = sb.Append("]}").ToString();
            FortalArenaRemote.ReportNow();
        }

        public static string ReportJson()
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"roster\":").Append(players.Count).Append(",\"seats\":[");
            bool first = true;
            for (int slot = 0; slot < seats.Length; slot++)
            {
                if (seats[slot] <= 0) continue;
                Player p; byTag.TryGetValue(seats[slot], out p);
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"slot\":").Append(slot).Append(",\"tag\":").Append(seats[slot])
                  .Append(",\"name\":\"").Append(Escape(p != null ? p.name : "")).Append("\",\"id\":\"").Append(Escape(p != null ? p.id : "")).Append("\"}");
            }
            sb.Append(']');
            if (result.Length > 0) sb.Append(",\"result\":").Append(result);
            return sb.Append('}').ToString();
        }

        static string Escape(string text)
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
