#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Fixes issues after importing external packages (like JU TPS 3) that
    /// overwrite URP project settings and bring in non-URP materials.
    /// </summary>
    public static class RoguePulsePostImportFix
    {
        private const string PipelineAssetPath = "Assets/Settings/Rendering/RoguePulse_URP.asset";

        [MenuItem("RoguePulse/Fix After Package Import/1. Fix All (URP + Materials)", priority = 0)]
        public static void FixAll()
        {
            Debug.Log("[RoguePulse] === Starting full post-import fix ===");

            FixURPSettings();
            ConvertAllMaterialsToURP();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RoguePulse] === Post-import fix complete ===");
            EditorUtility.DisplayDialog("RoguePulse",
                "修复完成！\n\n" +
                "1. URP 渲染管线已恢复\n" +
                "2. 材质已转换为 URP\n\n" +
                "如果仍有粉红色材质，请再次运行此工具。",
                "OK");
        }

        [MenuItem("RoguePulse/Fix After Package Import/2. Fix URP Pipeline Only", priority = 1)]
        public static void FixURPSettings()
        {
            Debug.Log("[RoguePulse] Fixing URP pipeline settings...");

            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                Debug.LogError($"[RoguePulse] URP pipeline asset not found at {PipelineAssetPath}");
                // Fall back to the emergency fix which can rebuild the asset.
                RoguePulseUrpEmergencyFix.EmergencyFixNow();
                return;
            }

            // Ensure renderer data list is valid. If the default package renderer
            // reference got broken, rebuild the pipeline entirely.
            SerializedObject so = new SerializedObject(pipeline);
            SerializedProperty rendererList = so.FindProperty("m_RendererDataList");
            bool rendererValid = false;
            if (rendererList != null && rendererList.arraySize > 0)
            {
                SerializedProperty first = rendererList.GetArrayElementAtIndex(0);
                rendererValid = first != null && first.objectReferenceValue != null;
            }

            if (!rendererValid)
            {
                Debug.LogWarning("[RoguePulse] Renderer data reference is broken. Running emergency rebuild...");
                RoguePulseUrpEmergencyFix.EmergencyFixNow();
                pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
                if (pipeline == null)
                {
                    Debug.LogError("[RoguePulse] Failed to rebuild URP pipeline.");
                    return;
                }
            }

            // Bind to Graphics and Quality settings.
            GraphicsSettings.defaultRenderPipeline = pipeline;

            int qualityCount = QualitySettings.names.Length;
            MethodInfo getAt = typeof(QualitySettings).GetMethod(
                "GetRenderPipelineAssetAt", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setAt = typeof(QualitySettings).GetMethod(
                "SetRenderPipelineAssetAt", BindingFlags.Public | BindingFlags.Static);

            if (getAt != null && setAt != null)
            {
                for (int i = 0; i < qualityCount; i++)
                {
                    setAt.Invoke(null, new object[] { i, pipeline });
                }
            }
            else
            {
                QualitySettings.renderPipeline = pipeline;
            }

            Debug.Log($"[RoguePulse] URP pipeline bound to all {qualityCount} quality levels.");
        }

        [MenuItem("RoguePulse/Fix After Package Import/3. Convert Materials to URP", priority = 2)]
        public static void ConvertAllMaterialsToURP()
        {
            Debug.Log("[RoguePulse] Converting non-URP materials...");

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int converted = 0;
            int skipped = 0;
            int total = materialGuids.Length;

            for (int i = 0; i < total; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                {
                    continue;
                }

                if (i % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("Converting Materials",
                        $"Processing {i}/{total}: {path}", (float)i / total);
                }

                string shaderName = mat.shader.name;

                // Skip materials already using URP shaders.
                if (shaderName.StartsWith("Universal Render Pipeline/") ||
                    shaderName.StartsWith("Shader Graphs/") ||
                    shaderName.StartsWith("Hidden/") ||
                    shaderName.StartsWith("Particles/") && shaderName.Contains("Universal") ||
                    shaderName.StartsWith("UI/"))
                {
                    skipped++;
                    continue;
                }

                // Convert Standard and legacy shaders to URP/Lit.
                if (shaderName == "Standard" ||
                    shaderName == "Standard (Specular setup)" ||
                    shaderName.StartsWith("Legacy Shaders/") ||
                    shaderName.StartsWith("Mobile/") ||
                    shaderName == "Diffuse" ||
                    shaderName == "Specular" ||
                    shaderName == "Bumped Diffuse" ||
                    shaderName == "Bumped Specular")
                {
                    ConvertStandardToURPLit(mat, path);
                    converted++;
                    continue;
                }

                // Convert particle shaders.
                if (shaderName.StartsWith("Particles/"))
                {
                    ConvertParticleToURP(mat, path);
                    converted++;
                    continue;
                }

                // Convert Unlit.
                if (shaderName == "Unlit/Texture" ||
                    shaderName == "Unlit/Color" ||
                    shaderName == "Unlit/Transparent" ||
                    shaderName == "Unlit/Transparent Cutout")
                {
                    ConvertUnlitToURP(mat, path);
                    converted++;
                    continue;
                }

                // If shader is broken (pink), try to recover.
                if (shaderName == "Hidden/InternalErrorShader" || shaderName == "Error")
                {
                    RecoverBrokenMaterial(mat, path);
                    converted++;
                    continue;
                }

                skipped++;
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"[RoguePulse] Material conversion done: {converted} converted, {skipped} skipped (already URP or non-convertible).");
        }

        private static void ConvertStandardToURPLit(Material mat, string path)
        {
            // Capture properties from Standard shader before switching.
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture metallicGlossMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = Color.black;
            bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
            if (hasEmission && mat.HasProperty("_EmissionColor"))
            {
                emissionColor = mat.GetColor("_EmissionColor");
            }

            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;

            // Determine render mode from Standard shader.
            float mode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning($"[RoguePulse] URP/Lit shader not found. Cannot convert: {path}");
                return;
            }

            mat.shader = urpLit;

            // Apply captured properties.
            mat.SetColor("_BaseColor", color);
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
            }
            if (metallicGlossMap != null) mat.SetTexture("_MetallicGlossMap", metallicGlossMap);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", glossiness);
            if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);

            if (hasEmission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
            }

            // Set surface type based on Standard mode.
            // Standard: 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
            switch ((int)mode)
            {
                case 0: // Opaque
                    mat.SetFloat("_Surface", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    break;
                case 1: // Cutout
                    mat.SetFloat("_Surface", 0);
                    mat.SetFloat("_AlphaClip", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case 2: // Fade
                case 3: // Transparent
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }

            EditorUtility.SetDirty(mat);
        }

        private static void ConvertParticleToURP(Material mat, string path)
        {
            Color color = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") :
                           mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

            Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticle == null)
            {
                Debug.LogWarning($"[RoguePulse] URP/Particles/Unlit shader not found. Cannot convert: {path}");
                return;
            }

            mat.shader = urpParticle;
            mat.SetColor("_BaseColor", color);
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);

            // Particles are typically transparent.
            mat.SetFloat("_Surface", 1);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
        }

        private static void ConvertUnlitToURP(Material mat, string path)
        {
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit == null)
            {
                Debug.LogWarning($"[RoguePulse] URP/Unlit shader not found. Cannot convert: {path}");
                return;
            }

            mat.shader = urpUnlit;
            mat.SetColor("_BaseColor", color);
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);

            EditorUtility.SetDirty(mat);
        }

        private static void RecoverBrokenMaterial(Material mat, string path)
        {
            // Try to recover from error shader by assigning URP/Lit as a fallback.
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return;
            }

            mat.shader = urpLit;
            EditorUtility.SetDirty(mat);
            Debug.Log($"[RoguePulse] Recovered broken material: {path}");
        }
    }
}
#endif
