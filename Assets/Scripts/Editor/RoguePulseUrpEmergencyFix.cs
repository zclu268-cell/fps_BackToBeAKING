#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoguePulse.Editor
{
    public static class RoguePulseUrpEmergencyFix
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RenderingFolder = "Assets/Settings/Rendering";
        private const string PipelineAssetPath = "Assets/Settings/Rendering/RoguePulse_URP.asset";
        private const string RendererAssetPath = "Assets/Settings/Rendering/RoguePulse_URP_Renderer.asset";
        [MenuItem("Tools/RoguePulse/Emergency Fix URP (Recover Pink/Blank Scene)")]
        public static void EmergencyFixNow()
        {
            TryRecoverUrp(verbose: true);
        }

        private static void TryRecoverUrp(bool verbose)
        {
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
            if (!IsValidUrpPipeline(pipeline))
            {
                pipeline = RebuildUrpPipelineAsset();
            }

            if (!IsValidUrpPipeline(pipeline))
            {
                Debug.LogError("URP recovery failed: unable to create a valid URP pipeline asset.");
                return;
            }

            bool changed = BindPipelineToProjectSettings(pipeline);
            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (verbose || changed)
            {
                Debug.Log($"URP recovered. Pipeline = {AssetDatabase.GetAssetPath(pipeline)}");
            }
        }

        private static bool IsValidUrpPipeline(RenderPipelineAsset asset)
        {
            if (asset == null)
            {
                return false;
            }

            Type type = asset.GetType();
            if (type.FullName == null || !type.FullName.Contains("UniversalRenderPipelineAsset"))
            {
                return false;
            }

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0)
            {
                return false;
            }

            SerializedProperty first = list.GetArrayElementAtIndex(0);
            return first != null && first.objectReferenceValue != null;
        }

        private static RenderPipelineAsset RebuildUrpPipelineAsset()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(RenderingFolder);

            if (AssetDatabase.LoadMainAssetAtPath(PipelineAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(PipelineAssetPath);
            }

            if (AssetDatabase.LoadMainAssetAtPath(RendererAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(RendererAssetPath);
            }

            Type urpAssetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            Type rendererDataType = Type.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (urpAssetType == null || rendererDataType == null)
            {
                return null;
            }

            object rendererData = CreateRendererDataAsset(urpAssetType);
            if (rendererData == null)
            {
                return null;
            }

            MethodInfo create = urpAssetType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { rendererDataType }, null);
            if (create == null)
            {
                return null;
            }

            object created = create.Invoke(null, new[] { rendererData });
            RenderPipelineAsset pipeline = created as RenderPipelineAsset;
            if (pipeline == null)
            {
                return null;
            }

            AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            AssetDatabase.ImportAsset(PipelineAssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
        }

        private static object CreateRendererDataAsset(Type urpAssetType)
        {
            MethodInfo createRendererAsset = urpAssetType.GetMethod(
                "CreateRendererAsset",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (createRendererAsset == null)
            {
                return null;
            }

            Type rendererTypeEnum = urpAssetType.GetNestedType("RendererType", BindingFlags.NonPublic | BindingFlags.Public);
            if (rendererTypeEnum == null)
            {
                return null;
            }

            object rendererType = Enum.Parse(rendererTypeEnum, "UniversalRenderer");
            return createRendererAsset.Invoke(null, new object[] { PipelineAssetPath, rendererType, true, "Renderer" });
        }

        private static bool BindPipelineToProjectSettings(RenderPipelineAsset pipeline)
        {
            bool changed = false;

            if (SetStaticProperty(typeof(GraphicsSettings), "defaultRenderPipeline", pipeline))
            {
                changed = true;
            }

            if (SetStaticProperty(typeof(GraphicsSettings), "renderPipelineAsset", pipeline))
            {
                changed = true;
            }

            MethodInfo getAt = typeof(QualitySettings).GetMethod("GetRenderPipelineAssetAt", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setAt = typeof(QualitySettings).GetMethod("SetRenderPipelineAssetAt", BindingFlags.Public | BindingFlags.Static);
            if (getAt != null && setAt != null)
            {
                int count = QualitySettings.names.Length;
                for (int i = 0; i < count; i++)
                {
                    object current = getAt.Invoke(null, new object[] { i });
                    if (ReferenceEquals(current, pipeline))
                    {
                        continue;
                    }

                    setAt.Invoke(null, new object[] { i, pipeline });
                    changed = true;
                }
            }
            else if (!ReferenceEquals(QualitySettings.renderPipeline, pipeline))
            {
                QualitySettings.renderPipeline = pipeline;
                changed = true;
            }

            return changed;
        }

        private static bool SetStaticProperty(Type owner, string propertyName, object value)
        {
            PropertyInfo property = owner.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            object current = property.GetValue(null, null);
            if (ReferenceEquals(current, value))
            {
                return false;
            }

            property.SetValue(null, value, null);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folder))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
