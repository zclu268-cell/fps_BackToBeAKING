using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoguePulse.EditorTools
{
    [InitializeOnLoad]
    internal static class RoguePulseSciFiBeastsPackageImporter
    {
        private const string ImportedKey = "RoguePulse.AutoImport.SciFi_Beasts_Pack_V1_0";
        private const string PackageFileName = "SciFi_Beasts_Pack_V1.0.unitypackage";
        private const string ExpectedRootFolder = "Assets/SciFi_Beasts_Pack";

        private static readonly string[] CandidatePaths =
        {
            @"D:\unity素材\Unity_SciFi_Beasts_Pack_V1.0_科幻机械机器人怪物怪兽野兽带动画\SciFi_Beasts_Pack_V1.0.unitypackage",
            @"D:\unity素材\SciFi_Beasts_Pack_V1.0.unitypackage"
        };

        static RoguePulseSciFiBeastsPackageImporter()
        {
            EditorApplication.delayCall += TryImportOnceOnLoad;
        }

        [MenuItem("RoguePulse/Import/Import SciFi Beasts Pack 1.0")]
        private static void ImportFromMenu()
        {
            ImportPackage(force: true);
        }

        [MenuItem("RoguePulse/Import/Reset SciFi Beasts Auto-Import Flag")]
        private static void ResetImportedFlag()
        {
            EditorPrefs.DeleteKey(ImportedKey);
            Debug.Log("[RoguePulse] Cleared SciFi Beasts auto-import flag.");
        }

        private static void TryImportOnceOnLoad()
        {
            ImportPackage(force: false);
        }

        private static void ImportPackage(bool force)
        {
            if (!force && (EditorPrefs.GetBool(ImportedKey, false) || AssetDatabase.IsValidFolder(ExpectedRootFolder)))
            {
                EditorPrefs.SetBool(ImportedKey, true);
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => ImportPackage(force);
                return;
            }

            var packagePath = ResolvePackagePath();
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogWarning("[RoguePulse] SciFi Beasts unitypackage not found. Checked configured paths.");
                return;
            }

            try
            {
                if (!force)
                {
                    EditorPrefs.SetBool(ImportedKey, true);
                }

                AssetDatabase.ImportPackage(packagePath, false);
                AssetDatabase.Refresh();
                Debug.Log($"[RoguePulse] Imported unitypackage: {packagePath}");
            }
            catch (Exception ex)
            {
                if (!force)
                {
                    EditorPrefs.DeleteKey(ImportedKey);
                }

                Debug.LogError($"[RoguePulse] Failed to import SciFi Beasts unitypackage: {ex.Message}");
            }
        }

        private static string ResolvePackagePath()
        {
            foreach (var path in CandidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var searchRoot = @"D:\unity素材";
            if (!Directory.Exists(searchRoot))
            {
                return string.Empty;
            }

            try
            {
                foreach (var match in Directory.EnumerateFiles(searchRoot, PackageFileName, SearchOption.AllDirectories))
                {
                    return match;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoguePulse] Package search failed under {searchRoot}: {ex.Message}");
            }

            return string.Empty;
        }
    }
}
