using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // One minigame's starting layout, turned into its view prefab by the builder.
    public interface IMinigamePrefabRecipe
    {
        // The prefab name in Assets/Prefab/Minigame (also what the game passes to LoadView).
        string Name { get; }

        void Build();
    }

    // Menu FoodIsekaiZ > Minigames: builds the view prefabs from their recipes. "Build Missing
    // Prefabs" never touches a prefab that exists, so edits made in the prefab are kept; "Rebuild"
    // throws a prefab's edits away and starts it again from the recipe.
    // It first saves the plain shapes (circle, ring, pill, chevron, hexagon) as sprites in
    // Assets/Art/Minigame/Shared/Shapes, so prefabs can use them. Refreshing also relists the
    // minigame art the game looks up by name (Resources/MinigameArt).
    public static class MinigamePrefabBuilder
    {
        private const string ShapeFolder = MinigamePrefabKit.ArtFolder + "/Shared/Shapes";
        private const string LibraryPath = "Assets/Resources/" + MinigameViewLibrary.ResourceName + ".asset";
        private const string ArtLibraryPath = "Assets/Resources/" + MinigameArtLibrary.ResourceName + ".asset";

        [MenuItem("FoodIsekaiZ/Minigames/Build Missing Prefabs")]
        public static void BuildMissing()
        {
            Build(false, null);
        }

        [MenuItem("FoodIsekaiZ/Minigames/Rebuild All Prefabs (loses prefab edits)")]
        private static void RebuildAllFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild minigame prefabs",
                    "Every minigame prefab is rebuilt from code and loses its edits.", "Rebuild", "Cancel")) return;
            Build(true, null);
        }

        // Rebuilds only the named prefab, throwing away its edits.
        public static void Rebuild(string name)
        {
            Build(true, name);
        }

        // Builds the recipes' prefabs (only the named one when only is set); returns how many were written.
        public static int Build(bool overwrite, string only)
        {
            SaveShapes();
            int written = 0;
            foreach (IMinigamePrefabRecipe recipe in Recipes())
            {
                if (only != null && recipe.Name != only) continue;
                string path = MinigamePrefabKit.PathOf(recipe.Name);
                if (!overwrite && File.Exists(path)) continue;
                recipe.Build();
                written++;
                Debug.Log($"[Minigames] Built {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshLibrary();
            return written;
        }

        // Lists every view prefab in the prefab folder in Resources/MinigameViews.asset (made when
        // missing), so the game finds them by name. Also on its own menu item, for prefabs added by hand.
        [MenuItem("FoodIsekaiZ/Minigames/Refresh Prefab List")]
        public static void RefreshLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<MinigameViewLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MinigameViewLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var views = new List<MinigameView>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { MinigamePrefabKit.PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var view = prefab != null ? prefab.GetComponent<MinigameView>() : null;
                if (view != null) views.Add(view);
            }

            library.SetViews(views);
            EditorUtility.SetDirty(library);
            RefreshArtLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Minigames] {LibraryPath} lists {views.Count} view prefabs");
        }

        // Lists every sprite under Assets/Art/Minigame (except Unused) in Resources/MinigameArt.
        public static void RefreshArtLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<MinigameArtLibrary>(ArtLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MinigameArtLibrary>();
                AssetDatabase.CreateAsset(library, ArtLibraryPath);
            }

            var pictures = new List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { MinigamePrefabKit.ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(MinigamePrefabKit.ArtFolder + "/Unused/")) continue;
                var picture = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (picture != null) pictures.Add(picture);
            }

            library.SetPictures(pictures);
            EditorUtility.SetDirty(library);
            Debug.Log($"[Minigames] {ArtLibraryPath} lists {pictures.Count} pictures");
        }

        public static Sprite Circle => Shape("Circle");
        public static Sprite Ring => Shape("Ring");
        public static Sprite Pill => Shape("Pill");
        public static Sprite Chevron => Shape("Chevron");
        public static Sprite Hexagon => Shape("Hexagon");

        private static Sprite Shape(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ShapeFolder}/{name}.png");
        }

        private static IEnumerable<IMinigamePrefabRecipe> Recipes()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IMinigamePrefabRecipe>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                yield return (IMinigamePrefabRecipe)Activator.CreateInstance(type);
            }
        }

        private static void SaveShapes()
        {
            Directory.CreateDirectory(ShapeFolder);
            SaveShape("Circle", MinigameArt.Circle, Vector4.zero);
            SaveShape("Ring", MinigameArt.Ring, Vector4.zero);
            SaveShape("Pill", MinigameArt.Pill, new Vector4(31f, 31f, 31f, 31f));
            SaveShape("Chevron", MinigameArt.Chevron, Vector4.zero);
            SaveShape("Hexagon", MinigameArt.Hexagon, Vector4.zero);
        }

        private static void SaveShape(string name, Sprite sprite, Vector4 border)
        {
            string path = $"{ShapeFolder}/{name}.png";
            if (File.Exists(path)) return;
            File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }
}
