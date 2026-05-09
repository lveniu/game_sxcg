using UnityEditor;
using UnityEngine;

/// <summary>
/// WebGL质量档位自动配置工具
/// 菜单 → Build → Configure WebGL Quality
/// 对应规格: docs/planning/WebGL-build-spec.md §5
/// </summary>
public static class WebGLQualityConfig
{
    [MenuItem("Build/Configure WebGL Quality")]
    public static void Configure()
    {
        // 获取QualitySettings资产路径
        const string qualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(qualitySettingsPath);

        if (asset == null)
        {
            Debug.LogError("[WebGL质量] 找不到 QualitySettings.asset");
            return;
        }

        // 使用SerializedObject操作
        var so = new SerializedObject(asset);
        so.Update();

        // 查找qualityLevels数组
        var qualityLevels = so.FindProperty("m_QualitySettings");
        if (qualityLevels == null)
        {
            Debug.LogError("[WebGL质量] 无法访问Quality Settings属性");
            return;
        }

        // 查找是否已有WebGL档位
        int webglIndex = -1;
        for (int i = 0; i < qualityLevels.arraySize; i++)
        {
            var nameProp = qualityLevels.GetArrayElementAtIndex(i).FindPropertyRelative("name");
            if (nameProp != null && nameProp.stringValue == "WebGL")
            {
                webglIndex = i;
                break;
            }
        }

        if (webglIndex < 0)
        {
            // 新增WebGL档位
            qualityLevels.InsertArrayElementAtIndex(qualityLevels.arraySize);
            webglIndex = qualityLevels.arraySize - 1;
            Debug.Log($"[WebGL质量] 新增WebGL档位 (index={webglIndex})");
        }

        var level = qualityLevels.GetArrayElementAtIndex(webglIndex);

        // 设置名称
        var nameP = level.FindPropertyRelative("name");
        if (nameP != null) nameP.stringValue = "WebGL";

        // Pixel Light Count: 0
        SetIntProperty(level, "pixelLightCount", 0);

        // Shadows: No Shadows (value = 0 = None)
        SetIntProperty(level, "shadows", 0);

        // Shadow Resolution: Low
        SetIntProperty(level, "shadowResolution", 0);

        // Shadow Projection: CloseFit
        SetIntProperty(level, "shadowProjection", 0);

        // Shadow Distance: 10
        SetFloatProperty(level, "shadowDistance", 10f);

        // Shadow Cascades: 0
        SetIntProperty(level, "shadowCascades", 0);

        // Anisotropic Textures: Disabled (0)
        SetIntProperty(level, "anisotropicTextures", 0);

        // Anti Aliasing: Disabled (0)
        SetIntProperty(level, "antiAliasing", 0);

        // Soft Particles: Disabled
        SetBoolProperty(level, "softParticles", false);

        // VSync: Disabled
        SetBoolProperty(level, "vSyncCount", false);

        // LOD Bias: 0.5
        SetFloatProperty(level, "lodBias", 0.5f);

        // Particle Raycast Budget: 16
        SetIntProperty(level, "particleRaycastBudget", 16);

        // Streaming Mipmaps: Active
        SetBoolProperty(level, "streamingMipmapsActive", true);

        // Set WebGL as default for WebGL platform
        var defaultQuality = so.FindProperty("m_PerPlatformDefaultQuality");
        if (defaultQuality != null)
        {
            // Check if WebGL entry already exists
            bool found = false;
            for (int i = 0; i < defaultQuality.arraySize; i++)
            {
                var platform = defaultQuality.GetArrayElementAtIndex(i);
                var pName = platform.FindPropertyRelative("m_Platform");
                if (pName != null && pName.stringValue == "WebGL")
                {
                    var pQuality = platform.FindPropertyRelative("m_Quality");
                    if (pQuality != null) pQuality.intValue = webglIndex;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                defaultQuality.InsertArrayElementAtIndex(defaultQuality.arraySize);
                var newEntry = defaultQuality.GetArrayElementAtIndex(defaultQuality.arraySize - 1);
                var pName = newEntry.FindPropertyRelative("m_Platform");
                var pQuality = newEntry.FindPropertyRelative("m_Quality");
                if (pName != null) pName.stringValue = "WebGL";
                if (pQuality != null) pQuality.intValue = webglIndex;
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log("[WebGL质量] WebGL Quality Level 配置完成 ✓\n" +
            "  - Pixel Light Count: 0\n" +
            "  - Shadows: No Shadows\n" +
            "  - Anti Aliasing: Disabled\n" +
            "  - VSync: Disabled\n" +
            "  - LOD Bias: 0.5\n" +
            "  - Particle Raycast Budget: 16");
    }

    private static void SetIntProperty(SerializedProperty parent, string name, int value)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null) prop.intValue = value;
    }

    private static void SetFloatProperty(SerializedProperty parent, string name, float value)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetBoolProperty(SerializedProperty parent, string name, bool value)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null) prop.boolValue = value;
    }
}
