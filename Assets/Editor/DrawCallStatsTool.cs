using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// FE-30 DrawCall 统计面板 — Editor 工具
/// 
/// 菜单入口: Tools > FE-30 > 统计 DrawCall
/// 
/// 功能:
/// 1. 统计当前场景所有 Canvas 下的 Graphic 组件数量
/// 2. 按 Canvas 分组统计 DrawCall 估算值
/// 3. 标注潜在优化点: RaycastTarget/Image 对、独立材质、未合批元素
/// 4. 输出可复制的文本报告到 Console
/// </summary>
public class DrawCallStatsTool
{
    [MenuItem("Tools/FE-30/统计 DrawCall")]
    public static void AnalyzeDrawCalls()
    {
        // 清除之前的统计结果
        CanvasStats.Clear();

        // 遍历所有 Canvas
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c != null && c.isActiveAndEnabled)
            .ToList();

        if (canvases.Count == 0)
        {
            Debug.LogWarning("[FE-30 DrawCall] 场景中没有找到任何 Canvas 组件");
            return;
        }

        // 遍历每个 Canvas，统计其下的所有 Graphic
        foreach (var canvas in canvases)
        {
            var stats = new CanvasStats();
            stats.canvasName = canvas.name;
            stats.worldCanvas = canvas.renderMode != RenderMode.ScreenSpaceOverlay 
                && canvas.renderMode != RenderMode.ScreenSpaceCamera;

            // 获取 Canvas 下的所有 Graphic 组件
            var graphics = canvas.GetComponentsInChildren(true).SelectMany(c => c.GetComponents<Graphic>());
            var allGraphics = new List<Graphic>(graphics);

            stats.totalGraphics = allGraphics.Count;
            stats.totalImages = allGraphics.Count(g => g is Image);
            stats.totalTexts = allGraphics.Count(g => g is Text);
            stats.totalSliders = allGraphics.Count(g => g is Slider);
            stats.totalMask = allGraphics.Count(g => g is Mask);

            // 统计 RaycastTarget（每个 UI 元素可能有多个组件设置 RaycastTarget）
            var raycastTargets = canvas.GetComponentsInChildren(true)
                .Where(c => c is Behaviour b && b.enabled)
                .Count(c => c is RectTransform rt && rt.GetComponent<UnityEngine.UI.Button>() != null
                    || c is Image img && img.raycastTarget
                    || c is Button
                    || c is Slider
                    || c is Toggle);
            stats.totalRaycastTargetElements = raycastTargets;

            // 收集独立材质
            var materials = new HashSet<Material>();
            foreach (var g in allGraphics)
            {
                if (g.material != null) materials.Add(g.material);
            }
            stats.uniqueMaterials = materials.Count;
            stats.materialsList = materials.Select(m => m?.name ?? "(null)").Distinct().ToList();

            // 收集 Sprite（每个 Sprite + Material 组合可能产生独立 DrawCall）
            var spriteDrawCalls = new HashSet<string>();
            foreach (var img in allGraphics.OfType<Image>())
            {
                if (img.sprite != null && img.material != null)
                {
                    var key = $"{img.sprite.name}_{img.material.name}";
                    spriteDrawCalls.Add(key);
                }
            }
            stats.estimatedSpriteDrawCalls = spriteDrawCalls.Count;

            // 检查是否有不共享图集的 Image（独立材质 Image）
            var independentImages = allGraphics.OfType<Image>()
                .Where(img => img.sprite != null && img.raycastTarget)
                .ToList();

            // 检查独立材质（非共享图集）
            var sharedTextures = new HashSet<Texture>();
            var independentTextureImages = new List<Image>();

            foreach (var img in allGraphics.OfType<Image>())
            {
                if (img.sprite != null)
                {
                    var tex = img.sprite.texture;
                    if (tex != null && !sharedTextures.Contains(tex))
                    {
                        sharedTextures.Add(tex);
                    }
                    else if (tex != null && sharedTextures.Contains(tex))
                    {
                        // 共享图集
                    }
                    else
                    {
                        // 没有纹理的独立 Image（纯色/渐变背景等）
                        if (img.raycastTarget)
                            independentTextureImages.Add(img);
                    }
                }
            }
            stats.independentImageCount = independentTextureImages.Count;

            // 记录所有 Canvas 统计
            CanvasStats.CanvasStatsList[canvas.name] = stats;
        }

        // 输出报告
        PrintReport();
    }

    /// <summary>
    /// 统计当前所有场景的资源概况
    /// </summary>
    [MenuItem("Tools/FE-30/统计资源概况")]
    public static void AnalyzeResources()
    {
        var textures = FindObjectsByType<Texture>(FindObjectsSortMode.None).Where(t => t != null).ToList();
        var meshes = FindObjectsByType<Mesh>(FindObjectsSortMode.None).Where(m => m != null).ToList();
        var materials = FindObjectsByType<Material>(FindObjectsSortMode.None).Where(m => m != null).ToList();
        var gameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g != null).ToList();

        // 估算纹理内存
        long totalTextureMemory = 0;
        foreach (var tex in textures)
        {
            if (tex is Texture2D t2d)
            {
                totalTextureMemory += (long)t2d.width * t2d.height * 4; // RGBA32
            }
            else if (tex is Texture2DArray)
            {
                // 简略估算
            }
        }

        var activeCanvasCount = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Count(c => c != null && c.isActiveAndEnabled);
        var activeGraphicCount = FindObjectsByType<Graphic>(FindObjectsSortMode.None)
            .Count(g => g != null && g.isActiveAndEnabled);

        Debug.Log($"[FE-30 资源概况]");
        Debug.Log($"  纹理: {textures.Count} ({FormatBytes(totalTextureMemory)})");
        Debug.Log($"  Mesh: {meshes.Count}");
        Debug.Log($"  Material: {materials.Count}");
        Debug.Log($"  GameObject: {gameObjects.Count}");
        Debug.Log($"  Canvas: {activeCanvasCount}");
        Debug.Log($"  Graphic: {activeGraphicCount}");
    }

    private static void PrintReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("  DrawCall 统计报告 — FE-30");
        sb.AppendLine("  生成时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        int totalGraphics = 0, totalImages = 0, totalTexts = 0;
        int totalMaterials = 0, totalSpriteDC = 0;

        foreach (var kvp in CanvasStats.CanvasStatsList)
        {
            var stats = kvp.Value;
            sb.AppendLine();
            sb.AppendLine($"【Canvas: {kvp.Key}】");
            sb.AppendLine($"  渲染模式: {(stats.worldCanvas ? "WorldSpace" : "ScreenSpace")}");
            sb.AppendLine($"  Graphic 总数: {stats.totalGraphics}");
            sb.AppendLine($"    ├─ Image: {stats.totalImages}");
            sb.AppendLine($"    ├─ Text: {stats.totalTexts}");
            sb.AppendLine($"    ├─ Slider: {stats.totalSliders}");
            sb.AppendLine($"    └─ Mask: {stats.totalMask}");
            sb.AppendLine($"  RaycastTarget 元素: {stats.totalRaycastTargetElements}");
            sb.AppendLine($"  独立材质数: {stats.uniqueMaterials}");
            sb.AppendLine($"  Sprite DrawCall 估算: {stats.estimatedSpriteDrawCalls}");
            sb.AppendLine($"  非图集 Image: {stats.independentImageCount}");

            totalGraphics += stats.totalGraphics;
            totalImages += stats.totalImages;
            totalTexts += stats.totalTexts;
            totalMaterials += stats.uniqueMaterials;
            totalSpriteDC += stats.estimatedSpriteDrawCalls;
        }

        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"  汇总: Graphic={totalGraphics} Image={totalImages} Text={totalTexts}");
        sb.AppendLine($"  预估 DrawCall 范围: {totalSpriteDC}~{totalSpriteDC + totalMaterials}");
        sb.AppendLine($"  建议: DrawCall < 50 良好, 50~100 一般, > 100 需优化");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var report = sb.ToString();
        Debug.Log(report);
        EditorGUIUtility.systemCopyBuffer = report; // 自动复制到剪贴板
        Debug.Log("[FE-30] 报告已复制到剪贴板，可直接粘贴。");
    }

    private static string FormatBytes(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return $"{mb:F1} MB";
    }

    // ==================== 数据统计类 ====================

    private class CanvasStats
    {
        public string canvasName;
        public bool worldCanvas;
        public int totalGraphics;
        public int totalImages;
        public int totalTexts;
        public int totalSliders;
        public int totalMask;
        public int totalRaycastTargetElements;
        public int uniqueMaterials;
        public int estimatedSpriteDrawCalls;
        public int independentImageCount;
        public List<string> materialsList = new();

        public static Dictionary<string, CanvasStats> CanvasStatsList = new();
        public static void Clear() => CanvasStatsList.Clear();
    }
}
