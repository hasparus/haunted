using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Demons.Editor
{
    public static class DemonArtImport
    {
        const string Art = "Assets/_Game/Resources/Art/Demon";
        const float PixelsPerUnit = 320;

        [MenuItem("Demons/Import Demon Art...")]
        static void Pick()
        {
            var folder = EditorUtility.OpenFolderPanel("A folder cut by tools/demon_psd.py", "", "");
            if (!string.IsNullOrEmpty(folder)) Import(folder);
        }

        public static void Import(string folder)
        {
            var json = File.ReadAllText(Path.Combine(folder, "pivots.json"));
            var pivots = Regex.Match(json, "\"pivots\":\\s*\\{([^}]*)\\}").Groups[1].Value;
            foreach (Match m in Regex.Matches(pivots, "\"(\\w+)\":\\s*\\[\\s*([-\\d.e]+),\\s*([-\\d.e]+)\\s*\\]"))
            {
                string name = m.Groups[1].Value, path = $"{Art}/{name}.png";
                File.Copy(Path.Combine(folder, name + ".png"), path, true);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = new Vector2(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
