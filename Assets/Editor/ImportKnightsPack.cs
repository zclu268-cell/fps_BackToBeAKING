
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ImportKnightsPack
{
    [MenuItem("RoguePulse/Import Knights Pack")]
    public static void Import()
    {
        string path = @"D:\unity素材\Knights(Pack)_V1.0.unitypackage";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError("[ImportKnightsPack] 文件不存在: " + path);
            return;
        }
        AssetDatabase.ImportPackage(path, false);
        AssetDatabase.Refresh();
        Debug.Log("[ImportKnightsPack] 导入完成！");
    }
}
#endif
