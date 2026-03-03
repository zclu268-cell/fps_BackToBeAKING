using UnityEngine;
using UnityEditor;
using System.IO;

namespace RoguePulse.Editor
{
    /// <summary>
    /// 将 HIVEMIND PostApocalypticTown 包中的 HDRP 材质批量转换为 URP/Lit。
    /// 菜单：Assets > RoguePulse > Convert PostApocalyptic Materials to URP
    /// </summary>
    public static class ConvertHivemindMaterialsToURP
    {
        private const string TargetFolder = "Assets/HIVEMIND/PostApocalypticTown";

        [MenuItem("Assets/RoguePulse/Convert PostApocalyptic Materials to URP")]
        public static void Convert()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[MatConvert] URP/Lit shader not found. Is URP installed?");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { TargetFolder });
            int converted = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar(
                        "Converting Materials to URP",
                        Path.GetFileName(path),
                        (float)i / guids.Length);

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;

                    // 已经是 URP/Lit 就跳过
                    if (mat.shader == urpLit) continue;

                    // ── 读取旧属性 ──────────────────────────────────────────
                    Texture baseMap    = GetTex(mat, "_BaseMap", "_MainTex", "_Albedo");
                    Texture normalMap  = GetTex(mat, "_NormalMap", "_BumpMap");
                    Texture ormMap     = GetTex(mat, "_ORM", "_MetallicGlossMap");
                    Color   tint       = GetColor(mat, "_Tint", "_BaseColor", "_Color");
                    float   roughness  = GetFloat(mat, "_Roughness", 0.7f);

                    // ── 换成 URP/Lit ────────────────────────────────────────
                    mat.shader = urpLit;

                    // 基础颜色/贴图
                    if (baseMap != null)  mat.SetTexture("_BaseMap", baseMap);
                    mat.SetColor("_BaseColor", tint);

                    // 法线贴图
                    if (normalMap != null)
                    {
                        mat.SetTexture("_BumpMap", normalMap);
                        mat.SetFloat("_BumpScale", 1f);
                        mat.EnableKeyword("_NORMALMAP");
                    }

                    // ORM → 金属/光滑
                    if (ormMap != null)
                    {
                        // R=AO, G=Roughness, B=Metallic — 用于遮挡+粗糙感
                        mat.SetTexture("_OcclusionMap", ormMap);
                        mat.SetFloat("_OcclusionStrength", 1f);
                    }
                    // Roughness → Smoothness（反转）
                    mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness * 0.6f));
                    mat.SetFloat("_Metallic", 0f);

                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MatConvert] Done — converted {converted}/{guids.Length} materials to URP/Lit.");
        }

        // ── 工具方法 ────────────────────────────────────────────────────────

        private static Texture GetTex(Material mat, params string[] names)
        {
            foreach (string n in names)
                if (mat.HasProperty(n))
                {
                    Texture t = mat.GetTexture(n);
                    if (t != null) return t;
                }
            return null;
        }

        private static Color GetColor(Material mat, params string[] names)
        {
            foreach (string n in names)
                if (mat.HasProperty(n))
                    return mat.GetColor(n);
            return Color.white;
        }

        private static float GetFloat(Material mat, string name, float fallback)
        {
            return mat.HasProperty(name) ? mat.GetFloat(name) : fallback;
        }
    }
}
