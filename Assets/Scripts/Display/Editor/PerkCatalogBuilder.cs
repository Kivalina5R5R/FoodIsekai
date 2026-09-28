using System;
using FoodIsekaiZ.Gameplay;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FoodIsekaiZ.Display.Editor
{
    // Folder discovery happens in the editor; builds use explicit serialized prefab references.
    public sealed class PerkCatalogBuilder : AssetPostprocessor
    {
        private const string CatalogPath = "Assets/Settings/PerkCatalog.asset";
        private const string SmallFolder = "Assets/Prefab/Perk/Small";
        private const string BigFolder = "Assets/Prefab/Perk/Big";

        [MenuItem("Food Isekai/Rebuild Perk Catalog")]
        public static void Rebuild()
        {
            PerkCatalog catalog = AssetDatabase.LoadAssetAtPath<PerkCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PerkCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            WriteEntries(serialized.FindProperty("smallPerks"), SmallFolder);
            WriteEntries(serialized.FindProperty("bigPerks"), BigFolder);
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static void WriteEntries(SerializedProperty entries, string folder)
        {
            string[] ids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            Array.Sort(ids, (left, right) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(left), AssetDatabase.GUIDToAssetPath(right)));
            entries.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ids[i]));
                TMP_Text priceText = prefab.transform.Find("Price")?.GetComponent<TMP_Text>();
                int price = priceText != null && int.TryParse(priceText.text, out int parsed) ? parsed : 0;
                if (price <= 0) Debug.LogError($"Perk {prefab.name} needs a positive integer Price text.", prefab);
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = prefab.name;
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("price").intValue = price;
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
                foreach (string path in paths)
                    if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                        (path.StartsWith(SmallFolder + "/", StringComparison.Ordinal) ||
                         path.StartsWith(BigFolder + "/", StringComparison.Ordinal)))
                    {
                        EditorApplication.delayCall -= Rebuild;
                        EditorApplication.delayCall += Rebuild;
                        return;
                    }
        }
    }
}
