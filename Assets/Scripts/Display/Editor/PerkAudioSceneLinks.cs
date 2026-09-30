using System;
using System.IO;
using FoodIsekaiZ.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FoodIsekaiZ.Display.Editor
{
    // Upgrades missing perk audio references in open scenes without saving unrelated edits.
    [InitializeOnLoad]
    internal static class PerkAudioSceneLinks
    {
        static PerkAudioSceneLinks()
        {
            EditorApplication.delayCall += Repair;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) Repair();
            };
        }

        [MenuItem("Food Isekai/Repair Perk Audio Links")]
        private static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (GameSoundPlayer player in Resources.FindObjectsOfTypeAll<GameSoundPlayer>())
            {
                if (!player.gameObject.scene.IsValid() || EditorSceneManager.IsPreviewScene(player.gameObject.scene)) continue;
                var serialized = new SerializedObject(player);
                SerializedProperty clips = serialized.FindProperty("clips");
                SerializedProperty cooldowns = serialized.FindProperty("cooldowns");
                int count = Enum.GetValues(typeof(GameSoundCue)).Length;
                int previousCount = clips.arraySize;
                if (clips.arraySize < count) clips.arraySize = count;
                int previousCooldowns = cooldowns.arraySize;
                if (cooldowns.arraySize < count) cooldowns.arraySize = count;
                for (int i = (int)GameSoundCue.PerkReveal; i < count; i++)
                {
                    if (i >= previousCount || clips.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{(GameSoundCue)i}.wav");
                    if (i >= previousCooldowns) cooldowns.GetArrayElementAtIndex(i).floatValue = .14f;
                }
                bool repaired = serialized.ApplyModifiedProperties();
                foreach (PerkShopPresentation shop in Resources.FindObjectsOfTypeAll<PerkShopPresentation>())
                {
                    if (shop.gameObject.scene != player.gameObject.scene) continue;
                    var shopData = new SerializedObject(shop);
                    SerializedProperty link = shopData.FindProperty("soundPlayer");
                    if (link.objectReferenceValue != null) continue;
                    link.objectReferenceValue = player;
                    repaired |= shopData.ApplyModifiedProperties();
                }
                foreach (SmallPerkPurchasePresentation presentation in Resources.FindObjectsOfTypeAll<SmallPerkPurchasePresentation>())
                {
                    if (presentation.gameObject.scene != player.gameObject.scene) continue;
                    var purchase = new SerializedObject(presentation);
                    SerializedProperty link = purchase.FindProperty("soundPlayer");
                    if (link.objectReferenceValue != null) continue;
                    link.objectReferenceValue = player;
                    repaired |= purchase.ApplyModifiedProperties();
                }
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/PerkAudioSceneLinks.txt", $"Scene: {player.gameObject.scene.path}\nPrevious clips: {previousCount}\nCurrent clips: {clips.arraySize}\nRepaired: {repaired}\nPlayer active: {player.isActiveAndEnabled}\nListener volume: {AudioListener.volume}\nListener paused: {AudioListener.pause}\n");
                if (repaired)
                    Debug.Log("[PerkAudio] Filled missing perk audio links in the open scene. Unrelated scene edits are preserved; save the scene when ready.", player);
            }
        }

    }
}
