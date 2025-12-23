using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;

namespace Halak
{
    public static class IconMiner
    {
        [MenuItem("Unity Editor Icons/Export All %e", priority = -1001)]
        private static void ExportIcons()
        {
            EditorUtility.DisplayProgressBar("Export Icons", "Exporting...", 0.0f);
            try
            {
                var editorAssetBundle = GetEditorAssetBundle();
                var iconsPath = GetIconsPath();
                var count = 0;
                foreach (var assetName in EnumerateIcons(editorAssetBundle, iconsPath))
                {
                    var icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);
                    if (icon == null)
                        continue;

                    var readableTexture = new Texture2D(icon.width, icon.height, icon.format, icon.mipmapCount > 1);

                    Graphics.CopyTexture(icon, readableTexture);

                    Texture2D copySource = readableTexture;

                    if (GraphicsFormatUtility.IsCompressedFormat(icon.format))
                    {
                        copySource = Decompress(readableTexture);
                    }

                    var folderPath = Path.GetDirectoryName(Path.Combine("icons/original/", assetName.Substring(iconsPath.Length)));
                    Directory.CreateDirectory(folderPath);

                    var iconPath = Path.Combine(folderPath, icon.name + ".png");
                    File.WriteAllBytes(iconPath, copySource.EncodeToPNG());

                    count++;

                    if (copySource != readableTexture)
                    {
                        Texture2D.DestroyImmediate(copySource);
                    }

                    Texture2D.DestroyImmediate(readableTexture);
                }

                Debug.Log($"{count} icons has been exported!");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Unity Editor Icons/Generate README.md %g", priority = -1000)]
        private static void GenerateREADME()
        {
            EditorUtility.DisplayProgressBar("Generate README.md", "Generating...", 0.0f);
            try
            {
                var editorAssetBundle = GetEditorAssetBundle();
                var iconsPath = GetIconsPath();
                var readmeContents = new StringBuilder();

                readmeContents.AppendLine($"Unity Editor Built-in Icons");
                readmeContents.AppendLine($"==============================");
                readmeContents.AppendLine($"Unity version: {Application.unityVersion}");
                readmeContents.AppendLine($"Icons what can load using `EditorGUIUtility.IconContent`");
                readmeContents.AppendLine();
                readmeContents.AppendLine($"File ID");
                readmeContents.AppendLine($"-------------");
                readmeContents.AppendLine($"You can change script icon by file id");
                readmeContents.AppendLine($"1. Open `*.cs.meta` in Text Editor");
                readmeContents.AppendLine($"2. Modify line `icon: {{instanceID: 0}}` to `icon: {{fileID: <FILE ID>, guid: 0000000000000000d000000000000000, type: 0}}`");
                readmeContents.AppendLine($"3. Save and focus Unity Editor");
                readmeContents.AppendLine();
                readmeContents.AppendLine($"| Icon | Name | File ID |");
                readmeContents.AppendLine($"|------|------|---------|");

                var assetNames = EnumerateIcons(editorAssetBundle, iconsPath).ToList();

                int count = 0;

                var categorizedAssetNames = assetNames
                    .GroupBy(name =>
                    {
                        var relativePath = name.Substring(iconsPath.Length);
                        var directoryName = Path.GetDirectoryName(relativePath);
                        if (string.IsNullOrEmpty(directoryName))
                            return string.Empty;
                        return directoryName;
                    })
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var category in categorizedAssetNames)
                {
                    readmeContents.AppendLine();
                    readmeContents.AppendLine($"## {(string.IsNullOrEmpty(category.Key) ? "Uncategorized" : category.Key)}");
                    readmeContents.AppendLine();
                    readmeContents.AppendLine($"| Icon | Name | File ID |");
                    readmeContents.AppendLine($"|------|------|---------|");

                    var categoryAssets = category.ToList();
                    for (var i = 0; i < categoryAssets.Count; i++)
                    {
                        var assetName = categoryAssets[i];
                        var icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);
                        if (icon == null)
                            continue;

                        EditorUtility.DisplayProgressBar("Generate README.md", $"Generating... ({count + 1}/{assetNames.Count})", (float)count / assetNames.Count);

                        var readableTexture = new Texture2D(icon.width, icon.height, icon.format, icon.mipmapCount > 1);

                        Graphics.CopyTexture(icon, readableTexture);

                        Texture2D copySource = readableTexture;

                        if (GraphicsFormatUtility.IsCompressedFormat(icon.format))
                        {
                            copySource = Decompress(readableTexture);
                        }

                        var folderPath = Path.GetDirectoryName(Path.Combine("icons/small/", assetName.Substring(iconsPath.Length)));
                        Directory.CreateDirectory(folderPath);

                        var iconPath = Path.Combine(folderPath, icon.name + ".png");
                        File.WriteAllBytes(iconPath, copySource.EncodeToPNG());

                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(icon, out var guid, out long fileId);

                        var escapedUrl = iconPath.Replace(" ", "%20").Replace('\\', '/');
                        readmeContents.AppendLine($"| ![]({escapedUrl}) | `{icon.name}` | `{fileId}` |");

                        ++count;

                        if (copySource != readableTexture)
                        {
                            Texture2D.DestroyImmediate(copySource);
                        }

                        Texture2D.DestroyImmediate(readableTexture);
                    }
                }

                File.WriteAllText("README.md", readmeContents.ToString());

                Debug.Log($"'READMD.md' has been generated.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static IEnumerable<string> EnumerateIcons(AssetBundle editorAssetBundle, string iconsPath)
        {
            foreach (var assetName in editorAssetBundle.GetAllAssetNames())
            {
                if (assetName.StartsWith(iconsPath, StringComparison.OrdinalIgnoreCase) == false)
                    continue;
                if (assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == false &&
                    assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                yield return assetName;
            }
        }

        private static AssetBundle GetEditorAssetBundle()
        {
            var editorGUIUtility = typeof(EditorGUIUtility);
            var getEditorAssetBundle = editorGUIUtility.GetMethod(
                "GetEditorAssetBundle",
                BindingFlags.NonPublic | BindingFlags.Static);

            return (AssetBundle)getEditorAssetBundle.Invoke(null, new object[] { });
        }

        private static string GetIconsPath()
        {
#if UNITY_2018_3_OR_NEWER
            return UnityEditor.Experimental.EditorResources.iconsPath;
#else
            var assembly = typeof(EditorGUIUtility).Assembly;
            var editorResourcesUtility = assembly.GetType("UnityEditorInternal.EditorResourcesUtility");

            var iconsPathProperty = editorResourcesUtility.GetProperty(
                "iconsPath",
                BindingFlags.Static | BindingFlags.Public);

            return (string)iconsPathProperty.GetValue(null, new object[] { });
#endif
        }

        public static Texture2D Decompress(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }
    }
}
