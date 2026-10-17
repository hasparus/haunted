using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Demons.Editor
{
    // UI Toolkit can't build font assets from raw .ttf files in WebGL players, so we ship dynamic SDF assets.
    public static class FontAssets
    {
        const string Folder = "Assets/_Game/Resources/UI/Fonts";

        [MenuItem("Demons/Build Font Assets")]
        public static void Build()
        {
            foreach (var ttf in Directory.GetFiles(Folder, "*.ttf"))
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
                string path = Path.ChangeExtension(ttf, ".asset");
                AssetDatabase.DeleteAsset(path);
                var asset = FontAsset.CreateFontAsset(font);
                asset.name = font.name;
                AssetDatabase.CreateAsset(asset, path);
                asset.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (var atlas in asset.atlasTextures)
                {
                    atlas.name = font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
