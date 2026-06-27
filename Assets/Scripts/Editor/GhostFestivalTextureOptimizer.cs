#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-shot Android texture optimizer for the Ghost Festival art set (the
    /// project's heaviest texture folder). Sets per-texture ANDROID import
    /// overrides to cut APK/download weight.
    ///
    /// The dominant lever is <b>max size</b> — 2048→1024 quarters a texture's
    /// pixels (both memory and download). On top of that:
    ///  - "Smallest Download" uses crunched ETC (ETC2 RGBA when the source has
    ///    alpha, ETC1 RGB otherwise) — smallest on-disk/APK size.
    ///  - "Best Quality" uses ASTC 6x6 — better look, larger download (ASTC has
    ///    no crunch).
    ///
    /// "Preview" logs every change without writing. Run it first, then Apply.
    /// Bump any genuine hero surface back to 2048 by hand afterwards.
    /// </summary>
    internal static class GhostFestivalTextureOptimizer
    {
        private const string AndroidPlatform = "Android";
        private const int MaxTextureSize = 1024;
        private const int CompressionQuality = 50;

        private static readonly string[] TargetFolders =
        {
            "Assets/Ghost Festival/Textures",
            "Assets/Ghost Festival/VooDoo Doll Anim",
        };

        private enum Mode
        {
            SmallestDownload,
            BestQuality,
        }

        [MenuItem("CoinDreams/Optimize/Ghost Textures — Preview (dry run)")]
        private static void Preview() => Run(Mode.SmallestDownload, dryRun: true);

        [MenuItem("CoinDreams/Optimize/Ghost Textures — Apply (Smallest Download)")]
        private static void ApplySmallest() => Run(Mode.SmallestDownload, dryRun: false);

        [MenuItem("CoinDreams/Optimize/Ghost Textures — Apply (Best Quality ASTC)")]
        private static void ApplyQuality() => Run(Mode.BestQuality, dryRun: false);

        private static void Run(Mode mode, bool dryRun)
        {
            string[] folders = TargetFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Ghost Texture Optimizer",
                    "None of the target folders were found:\n" + string.Join("\n", TargetFolders),
                    "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", folders);
            if (!dryRun && !EditorUtility.DisplayDialog(
                    "Ghost Texture Optimizer",
                    $"Apply Android overrides to {guids.Length} texture(s)?\n\n" +
                    $"Mode: {mode}\nMax size: {MaxTextureSize}px\n\n" +
                    "This reimports the textures (may take a minute).",
                    "Apply", "Cancel"))
            {
                return;
            }

            var log = new StringBuilder();
            int touched = 0;

            try
            {
                if (!dryRun)
                {
                    AssetDatabase.StartAssetEditing();
                }

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    {
                        continue;
                    }

                    TextureImporterPlatformSettings settings =
                        importer.GetPlatformTextureSettings(AndroidPlatform);
                    settings.overridden = true;
                    settings.maxTextureSize = MaxTextureSize;
                    settings.compressionQuality = CompressionQuality;
                    settings.format = ResolveFormat(mode, importer);

                    if (!dryRun)
                    {
                        importer.SetPlatformTextureSettings(settings);
                        importer.SaveAndReimport();
                    }

                    touched++;
                    log.Append("  ").Append(System.IO.Path.GetFileName(path))
                        .Append(" -> ").Append(settings.maxTextureSize).Append("px ")
                        .Append(settings.format).Append('\n');
                }
            }
            finally
            {
                if (!dryRun)
                {
                    AssetDatabase.StopAssetEditing();
                }
            }

            Debug.Log(
                $"[GhostTextureOptimizer] {(dryRun ? "PREVIEW" : "APPLIED")} " +
                $"{touched} texture(s) (mode={mode}, maxSize={MaxTextureSize}px).\n{log}");

            if (!dryRun)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Ghost Texture Optimizer",
                    $"Applied to {touched} texture(s). See the Console for the per-texture log,\n" +
                    "then check Build Report → Textures to confirm the on-device sizes.",
                    "OK");
            }
        }

        private static TextureImporterFormat ResolveFormat(Mode mode, TextureImporter importer)
        {
            if (mode == Mode.BestQuality)
            {
                return TextureImporterFormat.ASTC_6x6;
            }

            // Smallest download: crunched ETC. ETC2 RGBA when the source carries
            // alpha, ETC1 (RGB) crunched otherwise so we don't pay for an unused
            // alpha channel.
            return importer.DoesSourceTextureHaveAlpha()
                ? TextureImporterFormat.ETC2_RGBA8Crunched
                : TextureImporterFormat.ETC_RGB4Crunched;
        }
    }
}
#endif
