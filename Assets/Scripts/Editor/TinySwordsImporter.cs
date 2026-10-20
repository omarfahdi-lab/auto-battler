using UnityEditor;
using UnityEngine;

namespace AutoBattler.Editor
{
    // import settings for the Tiny Swords art. slicing happens at runtime (Art.cs), this just
    // makes them crisp, uncompressed and readable
    public sealed class TinySwordsImporter : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Art/TinySwords/";

        // pack uses a 64px grid, so 1 tile = 1 cell = 1 unit
        public const int PixelsPerUnit = 64;

        // bump to force a reimport
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
            // needs to be readable for the 9-slice stitching + water color
            importer.isReadable = true;
            importer.maxTextureSize = 4096; // Lancer_Idle is 3840px wide
        }
    }
}
