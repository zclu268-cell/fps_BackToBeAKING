#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    public static class RoguePulseSyntyLocomotionImporter
    {
        private const string DefaultPackagePath =
            @"C:\Users\30835\xwechat_files\wxid_2tdaads1m59422_6bc6\msg\file\2026-03\Synty ANIMATION - Base Locomotion - Character Animset 1.1.2.unitypackage";

        private const string SidekickMasculineRoot = "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";
        private const string PolygonMasculineRoot = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine";
        private const string ResourcesTargetRoot = "Assets/Resources/Animations/PlayerReimportOnly";

        private static readonly string[] WalkTokens =
        {
            "a_mod_bl_walk_f_rm_masc",
            "a_mod_bl_walk_fwdstrafef_rm_masc",
            "a_walk_f_rootmotion_masc",
            "a_walk_f_masc",
            "a_mod_bl_walk_f_masc"
        };

        private static readonly string[] RunTokens =
        {
            "a_mod_bl_run_f_rm_masc",
            "a_mod_bl_run_fwdstrafef_rm_masc",
            "a_run_f_rootmotion_masc",
            "a_run_f_masc",
            "a_mod_bl_run_f_masc"
        };

        private static readonly string[] SprintTokens =
        {
            "a_mod_bl_sprint_f_rm_masc",
            "a_mod_bl_sprint_f_masc",
            "a_sprint_f_rootmotion_masc",
            "a_sprint_f_masc",
            "a_mod_bl_sprint_up25f_rm_masc",
            "a_mod_bl_sprint_up25f_masc"
        };

        private static readonly string[] TurnRightTokens =
        {
            "a_mod_bl_turn_standing_90r_rm_masc",
            "a_mod_bl_turn_standing_90r_masc",
            "a_turn_standing_90r_rootmotion_masc",
            "a_turn_standing_90r_masc",
            "a_mod_bl_turn_standing_180r_rm_masc",
            "a_mod_bl_turn_standing_180r_masc"
        };

        [MenuItem("RoguePulse/Setup Characters/Import Synty Base Locomotion And Apply To Player")]
        public static void ImportAndApply()
        {
            string packagePath = ResolvePackagePath();
            if (string.IsNullOrEmpty(packagePath))
            {
                return;
            }

            if (!File.Exists(packagePath))
            {
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    $"Package not found:\n{packagePath}",
                    "OK");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();

            List<AnimationClip> clips = CollectCandidateClips();
            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    "No candidate locomotion clips found after import.\nExpected under:\n" +
                    $"{SidekickMasculineRoot}\n{PolygonMasculineRoot}",
                    "OK");
                return;
            }

            AnimationClip walk = PickClip(clips, WalkTokens);
            AnimationClip run = PickClip(clips, RunTokens);
            AnimationClip sprint = PickClip(clips, SprintTokens);
            AnimationClip turnRight = PickClip(clips, TurnRightTokens);

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Animations");
            EnsureFolder(ResourcesTargetRoot);

            int savedCount = 0;
            savedCount += SaveClipCopy("Walking", walk) ? 1 : 0;
            savedCount += SaveClipCopy("Slow Run", run) ? 1 : 0;
            savedCount += SaveClipCopy("Great Sword Run", sprint) ? 1 : 0;
            savedCount += SaveClipCopy("Running Right Turn", turnRight) ? 1 : 0;

            if (savedCount <= 0)
            {
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    "No clip was mapped successfully.\nPlease check imported clip names in Synty package.",
                    "OK");
                return;
            }

            bool sceneDirty = EnsurePlayerControllerUsesCustomOverrides();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            string summary =
                $"Mapped clips saved to:\n{ResourcesTargetRoot}\n\n" +
                $"Walking => {ClipNameOrMissing(walk)}\n" +
                $"Slow Run => {ClipNameOrMissing(run)}\n" +
                $"Great Sword Run => {ClipNameOrMissing(sprint)}\n" +
                $"Running Right Turn => {ClipNameOrMissing(turnRight)}\n\n" +
                "Player will use these clips at runtime via PlayerController custom override.";

            Debug.Log($"[RoguePulse] Synty locomotion import complete.\n{summary}");
            EditorUtility.DisplayDialog("RoguePulse", summary, "OK");
        }

        private static string ResolvePackagePath()
        {
            if (File.Exists(DefaultPackagePath))
            {
                return DefaultPackagePath;
            }

            string selected = EditorUtility.OpenFilePanel(
                "Select Synty Base Locomotion unitypackage",
                Application.dataPath,
                "unitypackage");

            if (string.IsNullOrEmpty(selected))
            {
                return null;
            }

            return selected;
        }

        private static List<AnimationClip> CollectCandidateClips()
        {
            List<AnimationClip> result = new List<AnimationClip>(256);
            HashSet<string> seenPaths = new HashSet<string>();
            CollectFromFolder(SidekickMasculineRoot, result, seenPaths);
            CollectFromFolder(PolygonMasculineRoot, result, seenPaths);
            return result;
        }

        private static void CollectFromFolder(string folder, List<AnimationClip> output, HashSet<string> seenPaths)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
                {
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                output.Add(clip);
            }
        }

        private static AnimationClip PickClip(List<AnimationClip> clips, string[] orderedTokens)
        {
            for (int i = 0; i < orderedTokens.Length; i++)
            {
                string wanted = Normalize(orderedTokens[i]);
                for (int j = 0; j < clips.Count; j++)
                {
                    AnimationClip clip = clips[j];
                    if (clip == null)
                    {
                        continue;
                    }

                    string normalizedName = Normalize(clip.name);
                    if (normalizedName == wanted || normalizedName.Contains(wanted))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static bool SaveClipCopy(string targetName, AnimationClip source)
        {
            if (source == null || string.IsNullOrEmpty(targetName))
            {
                return false;
            }

            string targetPath = $"{ResourcesTargetRoot}/{targetName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath) != null)
            {
                AssetDatabase.DeleteAsset(targetPath);
            }

            AnimationClip copy = Object.Instantiate(source);
            copy.name = targetName;
            AssetDatabase.CreateAsset(copy, targetPath);
            EditorUtility.SetDirty(copy);
            return true;
        }

        private static bool EnsurePlayerControllerUsesCustomOverrides()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(player);
            SerializedProperty useCustomProp = so.FindProperty("useCustomLocomotionOverrides");
            if (useCustomProp != null && !useCustomProp.boolValue)
            {
                useCustomProp.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(player);
                return true;
            }

            return false;
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return raw
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace("@", string.Empty)
                .Replace(".", string.Empty);
        }

        private static string ClipNameOrMissing(AnimationClip clip)
        {
            return clip != null ? clip.name : "(not found)";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
