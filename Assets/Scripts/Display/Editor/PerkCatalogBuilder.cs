using System;
using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FoodIsekaiZ.Display.Editor
{
    // Folder discovery happens in the editor; builds use explicit serialized prefab references.
    public sealed class PerkCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string CatalogPath = "Assets/Settings/PerkCatalog.asset";
        private const string SmallFolder = "Assets/Prefab/Perk/Small";
        private const string BigFolder = "Assets/Prefab/Perk/Big";

        public int callbackOrder => 0;

        // Refresh and validate serialized card references before packaging the standalone player.
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!TryRebuild())
                throw new BuildFailedException("Perk Catalog could not be refreshed. Fix the reported PerkPrice components and display references before building.");
            PerkCatalog catalog = AssetDatabase.LoadAssetAtPath<PerkCatalog>(CatalogPath);
            if (catalog == null || catalog.GetOffers(false).Count < 4 || catalog.GetOffers(true).Count < 4)
                throw new BuildFailedException("Perk Catalog must contain at least four valid Small and four valid Big cards. Check prefab references and PerkPrice values before building.");
        }

        [MenuItem("Food Isekai/Rebuild Perk Catalog")]
        public static void Rebuild()
        {
            TryRebuild();
        }

        private static bool TryRebuild()
        {
            PerkCatalog catalog = AssetDatabase.LoadAssetAtPath<PerkCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PerkCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            bool smallValid = WriteEntries(serialized.FindProperty("smallPerks"), SmallFolder);
            bool bigValid = WriteEntries(serialized.FindProperty("bigPerks"), BigFolder);
            // Keep the last valid catalog intact if any authored price is invalid.
            if (!smallValid || !bigValid) return false;
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssetIfDirty(catalog);
            return true;
        }

        private static bool WriteEntries(SerializedProperty entries, string folder)
        {
            string[] ids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            Array.Sort(ids, (left, right) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(left), AssetDatabase.GUIDToAssetPath(right)));
            entries.arraySize = ids.Length;
            bool valid = true;
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ids[i]));
                PerkPrice data = prefab.GetComponent<PerkPrice>();
                PerkPriceDisplay display = prefab.GetComponent<PerkPriceDisplay>();
                if (data == null || data.Price <= 0 || display == null || !display.HasValidReferences)
                {
                    Debug.LogError($"Perk {prefab.name} needs a positive PerkPrice value and a PerkPriceDisplay with assigned references.", prefab);
                    valid = false;
                    continue;
                }
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = prefab.name;
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            }
            return valid;
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
