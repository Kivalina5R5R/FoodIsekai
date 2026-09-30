using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEngine;

namespace FoodIsekaiZ.Display.Editor
{
    [CustomEditor(typeof(PerkManagerMonitor))]
    public sealed class PerkManagerMonitorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gameManager"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation Shop Offers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Nothing checked for a break: use normal random perk offers. With any perks checked: offer ONLY those selections, up to 4, with no random fill. Serial mode always uses normal random offers. You must still buy the cards; owned or locked perks are skipped.", MessageType.Info);
            DrawOffers("Break 1 - Small", "smallSimulationOffers", "Perk_Small_");
            DrawOffers("Break 2 - Big", "bigSimulationOffers", "Perk_Big_");
            EditorGUILayout.HelpBox("Happiness requires buying Tasty first. Changes affect the next shop opening, not the current cards.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            var monitor = (PerkManagerMonitor)target;
            if (monitor.GameManager == null)
            {
                EditorGUILayout.HelpBox("Assign the game's manager to inspect its team perks.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Perks (Team)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Count", monitor.ActivePerks.Count.ToString());
            EditorGUILayout.LabelField("Food Capacity", monitor.GameManager.Perks.FoodCapacity.ToString());
            if (monitor.ActivePerks.Count == 0)
                EditorGUILayout.HelpBox(Application.isPlaying ? "The team has not bought a perk yet."
                    : "Enter Play Mode to see the team's active perks update as purchases succeed.", MessageType.Info);

            foreach (PerkPurchase purchase in monitor.ActivePerks)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(purchase.PerkId.Replace("Perk_", "").Replace("_", " / "), EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Perk ID", purchase.PerkId);
                        EditorGUILayout.IntField("Bought By Player", purchase.PlayerId);
                        EditorGUILayout.IntField("Active From Wave", purchase.BeforeWave);
                        EditorGUILayout.IntField("Price Paid", purchase.Price);
                    }
                }
            }
        }

        private void DrawOffers(string title, string propertyName, string prefix)
        {
            var selected = serializedObject.FindProperty(propertyName);
            EditorGUILayout.LabelField($"{title} ({selected.arraySize}/4)", EditorStyles.boldLabel);
            foreach (PerkDefinition definition in PerkDefinitions.All)
            {
                if (!definition.Id.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                int index = -1;
                for (int i = 0; i < selected.arraySize; i++)
                    if (selected.GetArrayElementAtIndex(i).stringValue == definition.Id) index = i;
                bool wasSelected = index >= 0;
                bool isSelected;
                using (new EditorGUI.DisabledScope(!wasSelected && selected.arraySize >= 4))
                    isSelected = EditorGUILayout.ToggleLeft(definition.Id.Substring(prefix.Length), wasSelected);
                if (wasSelected == isSelected) continue;
                if (!isSelected) selected.DeleteArrayElementAtIndex(index);
                else
                {
                    int newIndex = selected.arraySize;
                    selected.InsertArrayElementAtIndex(newIndex);
                    selected.GetArrayElementAtIndex(newIndex).stringValue = definition.Id;
                }
            }
        }
    }
}
