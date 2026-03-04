using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoguePulse.EditorTools
{
    [InitializeOnLoad]
    internal static class RoguePulseAutoPackageImporter
    {
        private const string PackagePath = @"D:\unity素材\POLYGON Fantasy Rivals - Low Poly 3D Art by Synty 1.3.1.unitypackage";
        private const string ImportedKey = "RoguePulse.AutoImport.POLYGON_Fantasy_Rivals_1_3_1";

        static RoguePulseAutoPackageImporter()
        {
            EditorApplication.delayCall += TryImportOnceOnLoad;
        }

        [MenuItem("RoguePulse/Import/Import POLYGON Fantasy Rivals 1.3.1")]
        private static void ImportFromMenu()
        {
            ImportPackage(force: true);
        }

        private static void TryImportOnceOnLoad()
        {
            ImportPackage(force: false);
        }

        private static void ImportPackage(bool force)
        {
            if (!force && EditorPrefs.GetBool(ImportedKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => ImportPackage(force);
                return;
            }

            if (!File.Exists(PackagePath))
            {
                Debug.LogWarning($"[RoguePulse] unitypackage not found: {PackagePath}");
                return;
            }

            try
            {
                if (!force)
                {
                    EditorPrefs.SetBool(ImportedKey, true);
                }

                AssetDatabase.ImportPackage(PackagePath, false);
                AssetDatabase.Refresh();
                Debug.Log($"[RoguePulse] Imported unitypackage: {PackagePath}");
            }
            catch (Exception ex)
            {
                if (!force)
                {
                    EditorPrefs.DeleteKey(ImportedKey);
                }

                Debug.LogError($"[RoguePulse] Failed to import unitypackage: {ex.Message}");
            }
        }
    }
}
