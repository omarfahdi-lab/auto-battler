using UnityEditor;
using UnityEngine;

namespace AutoBattler.Editor
{
    /// <summary>
    /// Pixel-art import settings for the Tiny Swords pack. Sheets are sliced into frames at runtime
    /// (see AutoBattler.Game.Art), so here we only make them crisp, uncompressed and readable.
    /// </summary>
    public sealed class TinySwordsImporter : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Art/TinySwords/";

        /// <summary>The pack is drawn on a 64px grid: one terrain tile = one board cell = one world unit.</summary>
        public const int PixelsPerUnit = 64;

        // Bump to force a reimport when these settings change.
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            // UI pieces are stitched into 9-slices and the water colour is sampled at runtime.
            importer.isReadable = true;
            importer.maxTextureSize = 4096; // Lancer_Idle is 3840px wide
        }
    }
}
