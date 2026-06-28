namespace Game.EditorTools
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.U2D;
    using UnityEngine;
    using UnityEngine.U2D;
    using Object = UnityEngine.Object;

    internal static class UiSpriteAtlasPacker
    {
        private const string UiAtlasFolder = "Assets/Art/Sprites/UI/Atlases";

        [MenuItem("CoinDreams/Art/Pack UI Sprite Atlases")]
        private static void PackUiSpriteAtlases()
        {
            SpriteAtlas[] atlases = LoadUiSpriteAtlases();
            if (atlases.Length == 0)
            {
                Debug.LogError("[UI Sprite Atlases] No Sprite Atlas assets found in " + UiAtlasFolder + ".");
                return;
            }

            bool isValid = ValidateAtlases(atlases, logDetails: true);
            if (!isValid)
            {
                Debug.LogError("[UI Sprite Atlases] Packing skipped. Fix the validation errors above first.");
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            SpriteAtlasUtility.PackAtlases(atlases, target, canCancel: false);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[UI Sprite Atlases] Packed {atlases.Length} atlases for {target}. " +
                "If Inspector Pack Preview is still disabled, use this menu item as the packing path.");
        }

        [MenuItem("CoinDreams/Art/Validate UI Sprite Atlases")]
        private static void ValidateUiSpriteAtlases()
        {
            SpriteAtlas[] atlases = LoadUiSpriteAtlases();
            if (atlases.Length == 0)
            {
                Debug.LogError("[UI Sprite Atlases] No Sprite Atlas assets found in " + UiAtlasFolder + ".");
                return;
            }

            ValidateAtlases(atlases, logDetails: true);
        }

        [MenuItem("CoinDreams/Art/Select UI Sprite Atlases")]
        private static void SelectUiSpriteAtlases()
        {
            SpriteAtlas[] atlases = LoadUiSpriteAtlases();
            Selection.objects = atlases;
            EditorGUIUtility.PingObject(atlases.Length > 0 ? atlases[0] : null);
        }

        private static SpriteAtlas[] LoadUiSpriteAtlases()
        {
            string[] guids = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { UiAtlasFolder });
            List<SpriteAtlas> atlases = new List<SpriteAtlas>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas != null)
                {
                    atlases.Add(atlas);
                }
            }

            atlases.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return atlases.ToArray();
        }

        private static bool ValidateAtlases(SpriteAtlas[] atlases, bool logDetails)
        {
            bool isValid = true;
            SpritePackerMode mode = EditorSettings.spritePackerMode;

            if (mode != SpritePackerMode.SpriteAtlasV2 && mode != SpritePackerMode.AlwaysOnAtlas)
            {
                Debug.LogWarning(
                    "[UI Sprite Atlases] Sprite Packer Mode is " + mode +
                    ". Pack Preview in the Inspector needs SpriteAtlasV2/Always Enabled. " +
                    "Packing from this menu still runs through Unity's SpriteAtlasUtility.");
            }

            for (int atlasIndex = 0; atlasIndex < atlases.Length; atlasIndex++)
            {
                SpriteAtlas atlas = atlases[atlasIndex];
                string atlasPath = AssetDatabase.GetAssetPath(atlas);
                Object[] packables = SpriteAtlasExtensions.GetPackables(atlas);

                if (packables == null || packables.Length == 0)
                {
                    Debug.LogError("[UI Sprite Atlases] " + atlasPath + " has no objects for packing.", atlas);
                    isValid = false;
                    continue;
                }

                int validPackables = 0;
                HashSet<string> seenPaths = new HashSet<string>();
                for (int packableIndex = 0; packableIndex < packables.Length; packableIndex++)
                {
                    Object packable = packables[packableIndex];
                    if (packable == null)
                    {
                        Debug.LogError("[UI Sprite Atlases] " + atlasPath + " has a missing object for packing.", atlas);
                        isValid = false;
                        continue;
                    }

                    string packablePath = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrWhiteSpace(packablePath))
                    {
                        Debug.LogError(
                            "[UI Sprite Atlases] " + atlasPath + " contains a non-asset packable: " + packable.name,
                            atlas);
                        isValid = false;
                        continue;
                    }

                    if (!seenPaths.Add(packablePath))
                    {
                        Debug.LogWarning(
                            "[UI Sprite Atlases] " + atlasPath + " contains duplicate packable path: " + packablePath,
                            atlas);
                    }

                    if (AssetDatabase.IsValidFolder(packablePath))
                    {
                        validPackables++;
                        continue;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(packablePath) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogError(
                            "[UI Sprite Atlases] " + atlasPath + " packable is not a texture/folder: " + packablePath,
                            atlas);
                        isValid = false;
                        continue;
                    }

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        Debug.LogError(
                            "[UI Sprite Atlases] " + packablePath + " is not imported as Sprite. Current type: " +
                            importer.textureType + ".",
                            packable);
                        isValid = false;
                        continue;
                    }

                    if (importer.spriteImportMode == SpriteImportMode.None)
                    {
                        Debug.LogError(
                            "[UI Sprite Atlases] " + packablePath + " has sprite import mode None.",
                            packable);
                        isValid = false;
                        continue;
                    }

                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        Debug.LogWarning(
                            "[UI Sprite Atlases] " + packablePath +
                            " is compressed before packing. This is valid, but can reduce atlas source quality.",
                            packable);
                    }

                    validPackables++;
                }

                if (logDetails)
                {
                    string fileName = Path.GetFileName(atlasPath);
                    Debug.Log(
                        $"[UI Sprite Atlases] {fileName}: {validPackables}/{packables.Length} packables valid, " +
                        $"spriteCount={atlas.spriteCount}.",
                        atlas);
                }
            }

            if (isValid)
            {
                Debug.Log("[UI Sprite Atlases] Validation passed.");
            }

            return isValid;
        }
    }
}
