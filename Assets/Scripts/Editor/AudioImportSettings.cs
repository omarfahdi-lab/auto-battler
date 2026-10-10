using UnityEditor;
using UnityEngine;

namespace AutoBattler.Editor
{
    /// <summary>
    /// Keeps the Kenney audio small in builds: sound effects are mono, everything is compressed at medium quality,
    /// and short clips decompress on load so they play without delay.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Audio/";

        public override uint GetVersion() => 1;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = assetPath.StartsWith(Root + "Sfx/");
            importer.loadInBackground = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.5f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
