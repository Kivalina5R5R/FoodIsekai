using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Fortal.UWB;
using FoodIsekaiZ.Configuration;

// Invoked explicitly from the menu or a local request file; never changes or saves a scene.
[InitializeOnLoad]
public static class FoodUwbVerification
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Request = "Assets/Scripts/UWB/Editor/UwbVerification.request";
    private static bool live, cut;
    private static double started, cutAt, recovered;
    private static int frames, fresh7, fresh8, beforeCut;
    private static float maxAge;
    private static readonly StringBuilder report = new StringBuilder();
    private static readonly HashSet<int> observedTags = new HashSet<int>();
    private static string registeredTags;
    private static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report.AppendLine("PASS: " + message);
    }

    static FoodUwbVerification()
    {
        EditorApplication.update += Poll;
    }

    private static byte[] Packet(byte id, Vector3 raw)
    {
        var b = new byte[128];
        b[0] = 0x55;
        b[1] = 1;
        b[2] = id;
        b[3] = 2;
        for (int axis = 0; axis < 3; axis++)
        {
            int n = Mathf.RoundToInt(raw[axis] * 1000f);
            for (int j = 0; j < 3; j++) b[4 + axis * 3 + j] = (byte)(n >> (8 * j));
        }
        byte sum = 0;
        for (int i = 0; i < 127; i++) unchecked { sum += b[i]; }
        b[127] = sum;
        return b;
    }

    private static void Feed(UWBManager manager, byte id, Vector3 raw)
    {
        foreach (byte b in Packet(id, raw)) Call(manager, "PushProtocolByte", b);
    }

    [MenuItem("Tools/FoodIsekai/Verify UWB")]
    public static void Rules()
    {
        report.Clear();
        var config = JsonUtility.FromJson<UWBConfigData>(File.ReadAllText("Assets/StreamingAssets/UWBConfig.json"));
        var go = new GameObject("UWB verification (temporary)");
        go.SetActive(false);
        try
        {
            var manager = go.AddComponent<UWBManager>();
            Set(manager, "axisConversion", config.axisConversion);
            Set(manager, "inputOffset", config.UWBInputOffset);
            Set(manager, "metersToWorldScale", config.metersToWorldScale);
            manager.RegisterTag(7);
            manager.RegisterTag(8);
            Vector3[] raw = { new Vector3(5.3f,1.25f,2), new Vector3(6.3f,1.25f,0), new Vector3(5.3f,2.25f,0), new Vector3(-1,-2,3) };
            Vector2[] expected = { Vector2.zero, Vector2.right, Vector2.down, new Vector2(-6.3f,3.25f) };
            for (int i = 0; i < raw.Length; i++)
            {
                manager.Disconnect();
                Feed(manager, 7, raw[i]);
                Call(manager, "Update");
                Check(manager.TryGetArenaPosition2D(7, out var pos, out _) && Vector2.Distance(pos, expected[i]) < 0.002f,
                    $"Binary packet -> calibrated world point {raw[i]} -> {expected[i]}");
            }
            manager.Disconnect();
            var anchor = new byte[896];
            anchor[0] = 0x55;
            anchor[895] = 0xEE;
            anchor[894] = 1;
            for (int i = 0; i < 30; i++) anchor[2 + i * 27] = 0xFF;
            for (int i = 0; i < 2; i++)
            {
                anchor[2 + i * 27] = (byte)(7 + i);
                anchor[3 + i * 27] = 2;
                byte[] tag = Packet((byte)(7 + i), raw[i]);
                Array.Copy(tag, 4, anchor, 4 + i * 27, 9);
            }
            foreach (byte b in anchor) Call(manager, "PushProtocolByte", b);
            Call(manager, "Update");
            Check(manager.TryGetArenaPosition2D(7, out var anchor7, out _) && anchor7.magnitude < .002f &&
                manager.TryGetArenaPosition2D(8, out var anchor8, out _) && Vector2.Distance(anchor8, Vector2.right) < .002f,
                "AnchorFrame0 with multiple tags uses the same origin and axes as TagFrame0");
            manager.Disconnect();
            Set(manager, "metersToWorldScale", 2f);
            Feed(manager, 7, raw[1]);
            Call(manager, "Update");
            Check(manager.TryGetArenaPosition2D(7, out var scaled, out _) && Vector2.Distance(scaled, Vector2.right * 2) < 0.002f,
                "World scale applied after offset exactly once");
            var tracker = go.AddComponent<UWBTracker>();
            Set(tracker, "manager", manager);
            tracker.ApplyTrackedPosition(new Vector3(2,0,0), 0);
            Check(Vector3.Distance(go.transform.position, new Vector3(2,0,0)) < 0.001f, "Tracker does not apply world scale twice");
            tracker.SetOffline();
            Check(!(bool)Get(tracker, "hasFirstData"), "Offline tracker discards old smoothing state");

            manager.Disconnect();
            Feed(manager, 7, raw[0]);
            Feed(manager, 8, raw[1]);
            Call(manager, "Update");
            Check(manager.IsTagOnline(7) && manager.IsTagOnline(8), "Alternating tags both reach consumers in one Unity update");
            manager.Disconnect();
            for (int i = 0; i < 300; i++) Feed(manager, (byte)(7 + i % 2), raw[0]);
            var queue = Get(manager, "pendingPoses");
            Check((int)queue.GetType().GetProperty("Count").GetValue(queue) == 256 && manager.DroppedOverflowFrames == 44, "Queue stays bounded at 256 frames");
            manager.Disconnect();
            Feed(manager, 7, raw[0]);
            queue = Get(manager, "pendingPoses");
            var item = queue.GetType().GetMethod("Dequeue").Invoke(queue, null);
            item.GetType().GetField("receivedAt").SetValue(item, -999d);
            queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { item });
            Feed(manager, 8, raw[1]);
            Call(manager, "Update");
            Check(!manager.IsTagOnline(7) && manager.IsTagOnline(8) && manager.DroppedStaleFrames == 1, "Old backlog discarded while fresh peer retained");
            manager.Disconnect();
            Check(!manager.IsTagOnline(8) && !(bool)Get(manager, "connectionRequested"), "Manual disconnect invalidates poses and cancels retry");

            var filter = new AdaptiveUwbPosition();
            filter.Sample(Vector3.zero, 0, 0, .03f);
            filter.Sample(new Vector3(10,0,0), .02, .02f, .03f);
            Check(filter.Present(.02f).sqrMagnitude < .0001f, "Single position spike rejected");
            filter.Sample(Vector3.zero, .04, .04f, .03f);
            double inputMotion = 0, outputMotion = 0;
            Vector3 lastRaw = Vector3.zero, lastOutput = Vector3.zero;
            for (int i = 3; i < 500; i++)
            {
                float t = i * .02f;
                var p = new Vector3(Mathf.Sin(i * 2.3f) * .05f, 0, Mathf.Cos(i * 1.7f) * .05f);
                filter.Sample(p, t, t, .03f);
                var output = filter.Present(t);
                inputMotion += Vector3.Distance(p, lastRaw);
                outputMotion += Vector3.Distance(output, lastOutput);
                lastRaw = p;
                lastOutput = output;
            }
            Check(outputMotion < inputMotion * .2, $"Stationary synthetic jitter reduced {100 * (1 - outputMotion / inputMotion):F1}%");
            foreach (float speed in new[] { .2f, 1f, 3f })
            {
                filter.Reset();
                Vector3 output = default;
                for (int i = 0; i < 200; i++)
                {
                    float t = i * .02f;
                    filter.Sample(Vector3.right * t * speed, t, t, .03f);
                    output = filter.Present(t + .01f);
                }
                float lag = (3.98f * speed - output.x - .03f) / speed;
                Check(lag < .12f && output.x <= 3.98f * speed, $"Motion {speed} m/s added filter lag {lag:F3}s without overshoot");
                float held = filter.Present(5f).x;
                Check(filter.Present(10f).x <= 3.98f * speed, "Signal gap holds target without extrapolation");
                var reacquired = filter.Sample(Vector3.left, 11, 11, .03f);
                Check(reacquired == Vector3.left, "Reacquisition resets stale velocity and history");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText("Library/FoodUwbChecks.txt", report.ToString());
        }
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (File.Exists(Request))
            {
                string command = File.ReadAllText(Request).Trim();
                File.Delete(Request);
                if (command == "snapshot")
                {
                    var current = UnityEngine.Object.FindFirstObjectByType<UWBManager>();
                    File.WriteAllText("Library/FoodUwbSnapshot.txt", $"playing={EditorApplication.isPlaying}; sceneDirty={UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}; mode={Get(current, "transportMode")}");
                }
                if (command == "rules") Rules();
                if (command == "live")
                {
                    if (!EditorApplication.isPlaying) throw new Exception("Enter Play mode first");
                    var manager = UnityEngine.Object.FindFirstObjectByType<UWBManager>();
                    if (manager == null) throw new Exception("Scene has no UWBManager");
                    var tags = (IDictionary)Get(manager, "tags");
                    registeredTags = "";
                    foreach (object key in tags.Keys) registeredTags += key + ",";
                    observedTags.Clear();
                    if (!manager.IsSerialMode)
                    {
                        manager.Disconnect();
                        Set(manager, "transportMode", UWBTransportMode.Serial);
                        manager.Connect();
                    }
                    started = EditorApplication.timeSinceStartup;
                    frames = fresh7 = fresh8 = 0;
                    maxAge = 0;
                    recovered = 0;
                    live = true;
                    cut = false;
                    File.WriteAllText("Library/FoodUwbLive.txt", "RUNNING");
                }
            }
            if (!live) return;
            if (!EditorApplication.isPlaying) throw new Exception("Play mode stopped during live test");
            var m = UnityEngine.Object.FindFirstObjectByType<UWBManager>();
            double now = EditorApplication.timeSinceStartup;
            var poses = (IList)Get(m, "posesForMainThread");
            foreach (object timed in poses)
            {
                var pose = (NoopLoopPose)timed.GetType().GetField("pose").GetValue(timed);
                if (pose.Role == 2) observedTags.Add(pose.Id);
                if (pose.NodeIds != null)
                    for (int i = 0; i < pose.NodeIds.Length; i++)
                        if (pose.NodeRoles[i] == 2) observedTags.Add(pose.NodeIds[i]);
            }
            frames++;
            if (m.TryGetTagPosition(7, out _, out float a7)) { fresh7++; maxAge = Mathf.Max(maxAge, a7); }
            if (m.TryGetTagPosition(8, out _, out float a8)) { fresh8++; maxAge = Mathf.Max(maxAge, a8); }
            if (!cut && now - started > 5)
            {
                if (!m.IsReceivingFrames) throw new Exception("No live UWB frames received");
                beforeCut = m.ReceivedFrameCount;
                cut = true;
                cutAt = now;
                Set(m, "keepReading", false);
            }
            if (cut && recovered == 0 && m.IsConnected && m.ReceivedFrameCount > beforeCut) recovered = now;
            if (cut && recovered == 0 && now - cutAt > 6) throw new Exception("Auto reconnect did not recover");
            if (now - started < 25) return;
            live = false;
            var parser = (NoopLoopFrameParser)Get(m, "parser");
            string result = fresh7 > frames * .7f && fresh8 > frames * .7f && recovered > 0 ? "PASS" : "FAIL";
            File.WriteAllText("Library/FoodUwbLive.txt", $"{result}: real COM5 25s; observations {frames}; tag7 fresh {fresh7}; tag8 fresh {fresh8}; max sample age {maxAge:F3}s; reconnect {recovered-cutAt:F3}s; stale dropped {m.DroppedStaleFrames}; overflow {m.DroppedOverflowFrames}; parser checksum errors since reopen {parser.ChecksumFailCount}; serial {m.IsSerialMode}; adaptive {m.UsesAdaptiveTracking}; offset {Get(m,"inputOffset")}; scale {Get(m,"metersToWorldScale")}");
            File.AppendAllText("Library/FoodUwbLive.txt", $"\nRegistered tags: {registeredTags}; observed packet tags: {string.Join(",", observedTags)}\n");
        }
        catch (Exception e)
        {
            File.WriteAllText(live ? "Library/FoodUwbLive.txt" : "Library/FoodUwbChecks.txt", "FAIL: " + e + "\n" + report);
            live = false;
        }
    }
}
