using UnityEditor;
using UnityEngine;

namespace Demons.Editor
{
    // Drawings dropped into Resources/Art import as sprites standing on their bottom edge.
    class ArtImport : AssetPostprocessor
    {
        public const float PixelsPerUnit = 300;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/") || !assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
