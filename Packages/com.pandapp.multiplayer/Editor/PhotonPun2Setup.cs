#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Pandapp.Multiplayer.Editor
{
    public static class PhotonPun2Setup
    {
        private const string EnableDefine = "PANDAPP_PHOTON_PUN2";
        private const string PhotonDefine = "PHOTON_UNITY_NETWORKING";

        [MenuItem("Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)")]
        public static void Setup()
        {
            if (!HasScriptingDefine(PhotonDefine))
            {
                EditorUtility.DisplayDialog(
                    "Pandapp Multiplayer",
                    $"Photon PUN2 bulunamadı.\n\nBeklenen define: {PhotonDefine}\n\nÖnce Photon PUN2 import et, sonra tekrar dene.",
                    "OK");
                return;
            }

            var changes = 0;
            var issues = new List<string>();

            var punFolder = FindFolder("PhotonUnityNetworking");
            var realtimeFolder = FindFolder("PhotonRealtime");

            if (string.IsNullOrEmpty(punFolder))
            {
                issues.Add("PhotonUnityNetworking klasörü bulunamadı.");
            }

            if (string.IsNullOrEmpty(realtimeFolder))
            {
                issues.Add("PhotonRealtime klasörü bulunamadı.");
            }

            if (issues.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Pandapp Multiplayer",
                    "Setup yarıda kaldı:\n- " + string.Join("\n- ", issues),
                    "OK");
                return;
            }

            changes += EnsureRuntimeAsmdef(realtimeFolder, "PhotonRealtime", Array.Empty<string>(), issues);
            changes += EnsureRuntimeAsmdef(punFolder, "PhotonUnityNetworking", new[] { "PhotonRealtime" }, issues);

            changes += EnsureEditorAsmdefs(realtimeFolder, "PhotonRealtime");
            changes += EnsureEditorAsmdefs(punFolder, "PhotonUnityNetworking");

            if (AddScriptingDefine(EnableDefine))
            {
                changes++;
            }

            AssetDatabase.Refresh();

            var message =
                $"Setup tamam.\n\nDeğişiklik sayısı: {changes}\n\nTransport'u aktif etmek için define eklendi: {EnableDefine}\nUnity şimdi scriptleri tekrar derleyecek.";

            if (issues.Count > 0)
            {
                message += "\n\nNotlar:\n- " + string.Join("\n- ", issues);
            }

            EditorUtility.DisplayDialog("Pandapp Multiplayer", message, "OK");
        }

        private static string FindFolder(string folderName)
        {
            var defaultPath = $"Assets/Photon/{folderName}";
            if (AssetDatabase.IsValidFolder(defaultPath))
            {
                return defaultPath;
            }

            var allPaths = AssetDatabase.GetAllAssetPaths();
            for (var i = 0; i < allPaths.Length; i++)
            {
                var path = allPaths[i];
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!path.EndsWith("/" + folderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static int EnsureRuntimeAsmdef(
            string folderAssetPath,
            string asmName,
            IReadOnlyList<string> references,
            List<string> issues)
        {
            if (HasNonEditorAsmdefAssembly(asmName))
            {
                return 0;
            }

            var folderFullPath = ToFullPath(folderAssetPath);
            if (Directory.Exists(folderFullPath))
            {
                var asmdefs = Directory.GetFiles(folderFullPath, "*.asmdef", SearchOption.TopDirectoryOnly);
                if (asmdefs.Length > 0)
                {
                    issues.Add($"'{folderAssetPath}' içinde asmdef bulundu; '{asmName}' otomatik oluşturulmadı.");
                    return 0;
                }
            }

            var asmdefPath = $"{folderAssetPath}/{asmName}.asmdef";
            WriteAsmdef(asmdefPath, asmName, references, Array.Empty<string>());
            return 1;
        }

        private static int EnsureEditorAsmdefs(string rootFolderAssetPath, string runtimeAssemblyName)
        {
            var changes = 0;
            var rootFullPath = ToFullPath(rootFolderAssetPath);

            if (!Directory.Exists(rootFullPath))
            {
                return 0;
            }

            var editorDirs = Directory.GetDirectories(rootFullPath, "Editor", SearchOption.AllDirectories);
            for (var i = 0; i < editorDirs.Length; i++)
            {
                var editorFullPath = editorDirs[i];
                var editorAssetPath = ToAssetPath(editorFullPath);
                if (string.IsNullOrEmpty(editorAssetPath))
                {
                    continue;
                }

                if (Directory.GetFiles(editorFullPath, "*.asmdef", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    continue;
                }

                var assemblyName = BuildEditorAssemblyName(runtimeAssemblyName, rootFolderAssetPath, editorAssetPath);
                var asmdefPath = $"{editorAssetPath}/{assemblyName}.asmdef";

                WriteAsmdef(
                    asmdefPath,
                    name: assemblyName,
                    references: new[] { runtimeAssemblyName },
                    includePlatforms: new[] { "Editor" });

                changes++;
            }

            return changes;
        }

        private static string BuildEditorAssemblyName(string runtimeAssemblyName, string rootAssetPath, string editorAssetPath)
        {
            var baseName = $"{runtimeAssemblyName}.Editor";

            if (string.Equals(rootAssetPath, editorAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return baseName;
            }

            var relative = editorAssetPath.StartsWith(rootAssetPath, StringComparison.OrdinalIgnoreCase)
                ? editorAssetPath.Substring(rootAssetPath.Length).Trim('/', '\\')
                : editorAssetPath.Trim('/', '\\');

            if (string.IsNullOrEmpty(relative))
            {
                return baseName;
            }

            var suffix = SanitizeAssemblyName(relative.Replace('\\', '/').Replace('/', '.'));
            return string.IsNullOrEmpty(suffix) ? baseName : $"{baseName}.{suffix}";
        }

        private static string SanitizeAssemblyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if ((ch >= 'a' && ch <= 'z')
                    || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9')
                    || ch == '.'
                    || ch == '_')
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Trim('.');
        }

        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] includePlatforms;
        }

        private static bool HasNonEditorAsmdefAssembly(string assemblyName)
        {
            var guids = AssetDatabase.FindAssets("t:asmdef");
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var fullPath = ToFullPath(assetPath);
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                {
                    continue;
                }

                AsmdefData asmdef;
                try
                {
                    var json = File.ReadAllText(fullPath).TrimStart('\uFEFF');
                    asmdef = JsonUtility.FromJson<AsmdefData>(json);
                }
                catch
                {
                    continue;
                }

                if (!string.Equals(asmdef?.name, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                var includePlatforms = asmdef.includePlatforms ?? Array.Empty<string>();
                if (includePlatforms.Length == 1
                    && string.Equals(includePlatforms[0], "Editor", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void WriteAsmdef(string asmdefAssetPath, string name, IReadOnlyList<string> references, IReadOnlyList<string> includePlatforms)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"name\": \"{name}\",");
            json.AppendLine("  \"rootNamespace\": \"\",");
            json.AppendLine("  \"references\": [");

            for (var i = 0; i < references.Count; i++)
            {
                var comma = i + 1 < references.Count ? "," : string.Empty;
                json.AppendLine($"    \"{references[i]}\"{comma}");
            }

            json.AppendLine("  ],");
            json.AppendLine($"  \"includePlatforms\": [{string.Join(", ", includePlatforms.Select(p => $"\"{p}\""))}],");
            json.AppendLine("  \"excludePlatforms\": [],");
            json.AppendLine("  \"allowUnsafeCode\": false,");
            json.AppendLine("  \"overrideReferences\": false,");
            json.AppendLine("  \"precompiledReferences\": [],");
            json.AppendLine("  \"autoReferenced\": true,");
            json.AppendLine("  \"defineConstraints\": [],");
            json.AppendLine("  \"versionDefines\": [],");
            json.AppendLine("  \"noEngineReferences\": false");
            json.AppendLine("}");

            var fullPath = ToFullPath(asmdefAssetPath);
            var folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(fullPath, json.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssetDatabase.ImportAsset(asmdefAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool AddScriptingDefine(string define)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(group);
            var current = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = SplitDefines(current);
            if (!list.Add(define))
            {
                return false;
            }

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            return true;
#else
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var list = SplitDefines(current);
            if (!list.Add(define))
            {
                return false;
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
            return true;
#endif
        }

        private static bool HasScriptingDefine(string define)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(group);
            var current = PlayerSettings.GetScriptingDefineSymbols(target);
            return SplitDefines(current).Contains(define);
#else
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            return SplitDefines(current).Contains(define);
#endif
        }

        private static HashSet<string> SplitDefines(string defines)
        {
            return new HashSet<string>(
                (defines ?? string.Empty)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim()),
                StringComparer.Ordinal);
        }

        private static string ToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string ToAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            var assetsFullPath = Path.GetFullPath(Application.dataPath);
            var normalized = Path.GetFullPath(fullPath);

            if (!normalized.StartsWith(assetsFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relative = normalized.Substring(assetsFullPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return "Assets/" + relative.Replace('\\', '/');
        }
    }
}
#endif
