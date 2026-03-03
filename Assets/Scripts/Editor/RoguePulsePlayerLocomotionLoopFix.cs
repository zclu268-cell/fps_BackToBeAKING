#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Ensures imported custom player locomotion FBX clips are looped.
    /// This prevents "walks once then legs freeze" when clips are used in BlendTrees.
    /// </summary>
    public sealed class RoguePulsePlayerLocomotionLoopFix : AssetPostprocessor
    {
        private const string CustomAnimationFolder = "Assets/Resources/Animations/PlayerReimportOnly";
        private const string SessionKey = "RoguePulse.PlayerLocomotionLoopFix.AppliedThisSession";

        private static readonly HashSet<string> TargetFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Walking.fbx",
            "Slow Run.fbx",
            "Great Sword Run.fbx",
            "Running Right Turn.fbx"
        };

        [InitializeOnLoadMethod]
        private static void ApplyOnceAfterDomainReload()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            ApplyLoopSettingsToExistingAssets(verbose: false);
        }

        [MenuItem("RoguePulse/Animations/Fix Player Locomotion Loop Time")]
        private static void FixLoopTimeMenu()
        {
            int changed = ApplyLoopSettingsToExistingAssets(verbose: true);
            Debug.Log($"[RoguePulse] Player locomotion loop fix completed. Updated {changed} FBX asset(s).");
        }

        private void OnPreprocessModel()
        {
            if (!IsTargetAsset(assetPath))
            {
                return;
            }

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null)
            {
                return;
            }

            // Fallback default for any new import. Explicit per-clip loop flags are
            // still applied by ApplyLoopSettingsToExistingAssets.
            importer.animationWrapMode = WrapMode.Loop;
        }

        private static int ApplyLoopSettingsToExistingAssets(bool verbose)
        {
            int changedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CustomAnimationFolder });
            if (guids == null || guids.Length == 0)
            {
                return 0;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsTargetAsset(path))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || !importer.importAnimation)
                {
                    continue;
                }

                if (!EnsureLoopingClips(importer))
                {
                    continue;
                }

                importer.SaveAndReimport();
                changedCount++;

                if (verbose)
                {
                    Debug.Log($"[RoguePulse] Enabled Loop Time: {path}");
                }
            }

            return changedCount;
        }

        private static bool EnsureLoopingClips(ModelImporter importer)
        {
            bool changed = importer.animationWrapMode != WrapMode.Loop;
            if (changed)
            {
                importer.animationWrapMode = WrapMode.Loop;
            }

            ModelImporterClipAnimation[] source = importer.clipAnimations;
            if (source == null || source.Length == 0)
            {
                source = importer.defaultClipAnimations;
            }

            if (source == null || source.Length == 0)
            {
                return changed;
            }

            var updated = new ModelImporterClipAnimation[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ModelImporterClipAnimation clip = source[i];
                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }

                updated[i] = clip;
            }

            if (changed)
            {
                importer.clipAnimations = updated;
            }

            return changed;
        }

        private static bool IsTargetAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(CustomAnimationFolder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            return TargetFileNames.Contains(fileName);
        }
    }
}
#endif
