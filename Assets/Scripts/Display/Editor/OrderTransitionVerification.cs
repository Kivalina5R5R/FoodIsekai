using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display.Editor
{
    // Serves multi-dish orders against a real PairedOrderPresentation in an isolated preview scene and checks
    // that every partial delivery starts its card transition: three to two, three to one and two to one.
    [InitializeOnLoad]
    internal static class OrderTransitionVerification
    {
        private const string RequestPath = "Temp/OrderTransitionVerification.request";
        private const string ResultPath = "Temp/OrderTransitionVerification.txt";

        static OrderTransitionVerification() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            Verify();
        }

        [MenuItem("Food Isekai/Verify Order Card Transitions")]
        private static void Verify()
        {
            var results = new List<string>();
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var panel = new GameObject("Panel", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(panel, preview);
                RectTransform Card(string name, float x, float scale)
                {
                    var card = new GameObject(name, typeof(RectTransform));
                    card.transform.SetParent(panel.transform, false);
                    var rect = (RectTransform)card.transform;
                    rect.anchoredPosition = new Vector2(x, 0f);
                    rect.localScale = Vector3.one * scale;
                    return rect;
                }
                var presentation = panel.AddComponent<PairedOrderPresentation>();
                Set(presentation, "firstCard", Card("Pair First", -52f, 0.72f));
                Set(presentation, "secondCard", Card("Pair Second", 52f, 0.72f));
                Set(presentation, "singleCard", Card("Single", 0f, 0.83f));
                Set(presentation, "tripleCards", new[] { Card("Triple 1", -72f, 0.6f), Card("Triple 2", 0f, 0.6f), Card("Triple 3", 72f, 0.6f) });
                Set(presentation, "tripleParticles", new CustomerPanelSuccessParticles[3]);

                var slotObject = new GameObject("Slot");
                SceneManager.MoveGameObjectToScene(slotObject, preview);
                var slot = slotObject.AddComponent<ArenaSlot2D>();
                slot.Configure("C1", ArenaSlotType.Customer, FoodType.None, null);

                // Three to two: serving the middle dish keeps the outer cards, which move onto the pair.
                Order(slot, presentation);
                Check(slot.TryServeFood(FoodType.Food2, 3f), "Serve the middle dish.", results);
                presentation.Synchronize(slot);
                Check(presentation.IsTripleTransitioning, "Three to two starts the triple transition.", results);
                Advance(presentation, 0.3f);
                CardPose leftLeaving = GetPose(presentation, 3);
                CardPose pairArriving = GetPose(presentation, 0);
                Check(leftLeaving.offset.x > 0f && pairArriving.offset.x < 0f,
                    "The left card glides right toward pair card one while pair card one comes from its place.", results);

                // Three to one in one hand-over: two cards pop, the last one moves to the single card.
                Order(slot, presentation);
                Check(slot.TryServeFood(FoodType.Food1, 3f) && slot.TryServeFood(FoodType.Food3, 3f),
                    "Serve two dishes in the same frame.", results);
                presentation.Synchronize(slot);
                Check(presentation.IsTripleTransitioning, "Three to one starts the triple transition.", results);
                Advance(presentation, 0.3f);
                Check(GetPose(presentation, 3).offset.y > 0f && GetPose(presentation, 5).offset.y > 0f,
                    "Both served cards rise while they fade.", results);
                Check(GetPose(presentation, 2).alpha < 1f && GetPose(presentation, 4).alpha > 0f,
                    "The middle card hands over to the single card.", results);

                // Two to one after the triple transition: the pair transition still plays.
                Order(slot, presentation);
                slot.TryServeFood(FoodType.Food1, 3f);
                presentation.Synchronize(slot);
                Advance(presentation, 1f);
                presentation.Synchronize(slot);
                Check(!presentation.IsTransitioning, "The triple transition finishes.", results);
                Check(slot.TryServeFood(FoodType.Food2, 3f), "Serve the next dish.", results);
                presentation.Synchronize(slot);
                Check(presentation.IsTransitioning && !presentation.IsTripleTransitioning,
                    "Two to one starts the pair transition.", results);

                // Each delivered dish adds five seconds and visibly refills the bar without stretching it.
                Order(slot, presentation);
                slot.AdvanceStateTimer(7f);
                Check(Mathf.Approximately(slot.StateRemainingSeconds, 13f) && Mathf.Approximately(slot.StateTimeNormalized, 0.65f),
                    "Seven seconds into a twenty-second order the bar is at 65%.", results);
                slot.TryServeFood(FoodType.Food1, 3f);
                Check(Mathf.Approximately(slot.StateRemainingSeconds, 18f) && Mathf.Approximately(slot.OrderDurationSeconds, 20f) &&
                    Mathf.Approximately(slot.StateTimeNormalized, 0.9f),
                    "One delivered dish adds five seconds: 13 to 18 seconds, bar 65% to 90%.", results);
                slot.TryServeFood(FoodType.Food2, 3f);
                Check(Mathf.Approximately(slot.StateRemainingSeconds, 23f) && Mathf.Approximately(slot.OrderDurationSeconds, 23f) &&
                    Mathf.Approximately(slot.StateTimeNormalized, 1f),
                    "A bonus past the full bar extends it only by the overflow: 23 seconds, bar full.", results);
                slot.AdvanceStateTimer(40f);
                Check(Mathf.Approximately(slot.WaitedSeconds, 30f),
                    "Waited time counts real seconds only: 7 before plus 23 until the timer ran out.", results);

                results.Add("PASS: all order card transitions start.");
            }
            catch (Exception error)
            {
                results.Add("FAIL: " + error.Message);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                File.WriteAllLines(ResultPath, results);
            }
        }

        private struct CardPose
        {
            public Vector3 offset;
            public float scale;
            public float alpha;
        }

        private static CardPose GetPose(PairedOrderPresentation presentation, int card)
        {
            presentation.GetPose(card, out _, out Vector3 offset, out float scale, out float alpha);
            return new CardPose { offset = offset, scale = scale, alpha = alpha };
        }

        // A fresh three-dish order that the presentation has already seen at three dishes.
        private static void Order(ArenaSlot2D slot, PairedOrderPresentation presentation)
        {
            Call(presentation, "ResetPresentation");
            slot.ConfigureCustomer(FoodType.Food1, 20f, 10, FoodType.Food2, false, false, FoodType.Food3);
            slot.StartCustomerTimer();
            presentation.Synchronize(slot);
        }

        private static void Advance(PairedOrderPresentation presentation, float seconds)
        {
            FieldInfo elapsed = typeof(PairedOrderPresentation).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
            elapsed.SetValue(presentation, Mathf.Min(0.58f, (float)elapsed.GetValue(presentation) + seconds));
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Check(bool condition, string message, List<string> results)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }
    }
}
