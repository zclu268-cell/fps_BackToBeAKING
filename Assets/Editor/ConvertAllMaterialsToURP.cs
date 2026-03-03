using UnityEngine;
using UnityEditor;
using System.IO;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Batch-converts ALL Built-in Standard and legacy particle materials in
    /// the project to their URP equivalents.
    /// Menu: Assets > RoguePulse > Convert ALL Materials to URP
    /// </summary>
    public static class ConvertAllMaterialsToURP
    {
        [MenuItem("Assets/RoguePulse/Convert ALL Materials to URP")]
        public static void ConvertAll()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpParticleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Shader urpParticleLit = Shader.Find("Universal Render Pipeline/Particles/Lit");

            if (urpLit == null)
            {
                Debug.LogError("[MatConvert] URP/Lit shader not found. Is URP installed?");
                return;
            }

            Shader builtinStandard = Shader.Find("Standard");
            Shader builtinStandardSpec = Shader.Find("Standard (Specular setup)");

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int convertedLit = 0;
            int convertedParticle = 0;
            int skipped = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar(
                        "Converting ALL Materials to URP",
                        Path.GetFileName(path),
                        (float)i / guids.Length);

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;

                    string shaderName = mat.shader != null ? mat.shader.name : "";

                    // Already URP — skip
                    if (shaderName.StartsWith("Universal Render Pipeline"))
                    {
                        skipped++;
                        continue;
                    }

                    // Built-in Standard → URP/Lit
                    if (mat.shader == builtinStandard ||
                        mat.shader == builtinStandardSpec ||
                        shaderName == "Standard" ||
                        shaderName == "Standard (Specular setup)")
                    {
                        ConvertStandardToUrpLit(mat, urpLit);
                        convertedLit++;
                        continue;
                    }

                    // Built-in Particle shaders → URP Particle
                    if (shaderName.Contains("Particles/"))
                    {
                        Shader target = shaderName.Contains("Unlit")
                            ? urpParticleUnlit
                            : (urpParticleLit != null ? urpParticleLit : urpParticleUnlit);
                        if (target != null)
                        {
                            ConvertParticleToUrp(mat, target);
                            convertedParticle++;
                            continue;
                        }
                    }

                    // Hidden/InternalErrorShader or completely missing → URP/Lit fallback
                    if (shaderName == "Hidden/InternalErrorShader" ||
                        shaderName == "" ||
                        mat.shader == null)
                    {
                        ConvertBrokenToUrpLit(mat, urpLit);
                        convertedLit++;
                        continue;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[MatConvert] Done — converted {convertedLit} Standard + {convertedParticle} Particle materials to URP. " +
                $"Skipped {skipped} already-URP materials.");
        }

        private static void ConvertStandardToUrpLit(Material mat, Shader urpLit)
        {
            // Capture old properties before shader switch
            Texture mainTex = GetTex(mat, "_MainTex");
            Texture bumpMap = GetTex(mat, "_BumpMap");
            Texture metallicMap = GetTex(mat, "_MetallicGlossMap");
            Texture occlusionMap = GetTex(mat, "_OcclusionMap");
            Texture emissionMap = GetTex(mat, "_EmissionMap");
            Color color = GetColor(mat, "_Color");
            Color emission = GetColor(mat, "_EmissionColor");
            float metallic = GetFloat(mat, "_Metallic", 0f);
            float smoothness = GetFloat(mat, "_Glossiness", 0.5f);
            float bumpScale = GetFloat(mat, "_BumpScale", 1f);
            float occlusionStrength = GetFloat(mat, "_OcclusionStrength", 1f);

            mat.shader = urpLit;

            // Restore properties
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color);

            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metallicMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (occlusionMap != null)
            {
                mat.SetTexture("_OcclusionMap", occlusionMap);
                mat.SetFloat("_OcclusionStrength", occlusionStrength);
            }

            if (emissionMap != null)
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else if (emission != Color.black && emission.maxColorComponent > 0.01f)
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
        }

        private static void ConvertParticleToUrp(Material mat, Shader urpParticle)
        {
            Texture mainTex = GetTex(mat, "_MainTex");
            Color color = GetColor(mat, "_Color", "_TintColor");

            mat.shader = urpParticle;

            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color);

            EditorUtility.SetDirty(mat);
        }

        private static void ConvertBrokenToUrpLit(Material mat, Shader urpLit)
        {
            // Try to salvage any properties
            Texture mainTex = GetTex(mat, "_BaseMap", "_MainTex", "_Albedo");
            Color color = GetColor(mat, "_BaseColor", "_Color", "_Tint");

            mat.shader = urpLit;

            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(mat);
        }

        // ── Utility ────────────────────────────────────────────────────

        private static Texture GetTex(Material mat, params string[] names)
        {
            foreach (string n in names)
            {
                if (mat.HasProperty(n))
                {
                    Texture t = mat.GetTexture(n);
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static Color GetColor(Material mat, params string[] names)
        {
            foreach (string n in names)
            {
                if (mat.HasProperty(n))
                    return mat.GetColor(n);
            }
            return Color.white;
        }

        private static float GetFloat(Material mat, string name, float fallback)
        {
            return mat.HasProperty(name) ? mat.GetFloat(name) : fallback;
        }
    }
}
