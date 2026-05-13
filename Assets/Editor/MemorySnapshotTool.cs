using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System;
using UnityEngine.UI;

/// <summary>
/// FE-30 内存快照工具 — Editor 运行时监控
/// 
/// 菜单入口: Tools > FE-30 > 内存快照
/// 
/// 功能:
/// 1. 运行时采样: Texture/Mesh/Material/GameObject 数量与内存
/// 2. 每 5 秒自动采样，Console 输出趋势
/// 3. 快照对比: 取两个时间点的 diff
/// 4. 标注泄漏嫌疑: 数量持续增长不回落
/// </summary>
public class MemorySnapshotTool : EditorWindow
{
    private bool isMonitoring = false;
    private float lastSampleTime = 0f;
    private const float SAMPLE_INTERVAL = 5f; // 5秒采样一次

    private List<MemorySample> snapshotHistory = new();
    private int sampleCount = 0;

    [MenuItem("Tools/FE-30/内存快照工具")]
    public static void ShowWindow()
    {
        GetWindow<MemorySnapshotTool>("FE-30 内存快照");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("FE-30 内存快照工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("运行时内存监控与泄漏检测工具。启动后每 5 秒自动采样一次。", MessageType.Info);

        EditorGUILayout.Space(10);

        // 控制按钮
        EditorGUILayout.BeginHorizontal();
        if (isMonitoring)
        {
            if (GUILayout.Button("⏹ 停止监控", GUILayout.Height(30)))
            {
                StopMonitoring();
            }
        }
        else
        {
            if (GUILayout.Button("▶ 启动监控", GUILayout.Height(30)))
            {
                StartMonitoring();
            }
        }
        if (GUILayout.Button("📸 手动采样", GUILayout.Height(30)))
        {
            TakeSnapshot("手动");
        }
        if (GUILayout.Button("📊 输出报告", GUILayout.Height(30)))
        {
            PrintReport();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 统计信息
        EditorGUILayout.LabelField($"已采样次数: {sampleCount}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"监控状态: {(isMonitoring ? "运行中" : "已停止")}", EditorStyles.miniLabel);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("最近采样记录", EditorStyles.boldLabel);

        // 表格头
        EditorGUILayout.BeginHorizontal(EditorStyles.miniLabel);
        EditorGUILayout.LabelField("#", GUILayout.Width(30));
        EditorGUILayout.LabelField("时间", GUILayout.Width(60));
        EditorGUILayout.LabelField("Texture", GUILayout.Width(60));
        EditorGUILayout.LabelField("Texture MB", GUILayout.Width(70));
        EditorGUILayout.LabelField("Mesh", GUILayout.Width(40));
        EditorGUILayout.LabelField("Material", GUILayout.Width(55));
        EditorGUILayout.LabelField("GameObject", GUILayout.Width(70));
        EditorGUILayout.LabelField("Graphic", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        // 采样记录（倒序显示最近的）
        var history = snapshotHistory.AsEnumerable();
        foreach (var sample in history.Reverse().Take(20))
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"{sample.id}", GUILayout.Width(30));
            EditorGUILayout.LabelField(sample.timeStr, GUILayout.Width(60));
            EditorGUILayout.LabelField($"{sample.textureCount}", GUILayout.Width(60));
            EditorGUILayout.LabelField($"{sample.textureMemoryMB:F1}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{sample.meshCount}", GUILayout.Width(40));
            EditorGUILayout.LabelField($"{sample.materialCount}", GUILayout.Width(55));
            EditorGUILayout.LabelField($"{sample.gameObjectCount}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{sample.graphicCount}", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("泄漏检测建议", EditorStyles.boldLabel);

        // 自动检测泄漏风险
        if (snapshotHistory.Count >= 3)
        {
            var warnings = new List<string>();

            // 检测 Texture 持续增长
            if (CheckTrend(sample => sample.textureCount))
            {
                warnings.Add("⚠️ Texture 数量持续增长 — 可能存在纹理泄漏");
            }

            // 检测 Texture 内存持续增长
            if (CheckTrend(sample => sample.textureMemoryMB))
            {
                warnings.Add("⚠️ Texture 内存持续增长 — 可能存在纹理泄漏或加载后未卸载");
            }

            // 检测 GameObject 持续增长
            if (CheckTrend(sample => sample.gameObjectCount))
            {
                warnings.Add("⚠️ GameObject 数量持续增长 — 可能存在对象未销毁");
            }

            // 检测 Material 持续增长
            if (CheckTrend(sample => sample.materialCount))
            {
                warnings.Add("⚠️ Material 数量持续增长 — 可能存在材质泄漏");
            }

            if (warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("✅ 未发现明显的内存泄漏趋势", MessageType.Info);
            }
            else
            {
                foreach (var w in warnings)
                {
                    EditorGUILayout.HelpBox(w, MessageType.Warning);
                }
            }
        }
        else if (isMonitoring)
        {
            EditorGUILayout.LabelField("至少采样 3 次才能检测泄漏趋势...", EditorStyles.miniLabel);
        }
    }

    private void Update()
    {
        if (!isMonitoring) return;

        if (EditorApplication.timeSinceStartup - lastSampleTime >= SAMPLE_INTERVAL)
        {
            lastSampleTime = EditorApplication.timeSinceStartup;
            TakeSnapshot($"采样 {sampleCount + 1}");
            Repaint();
        }
    }

    private void StartMonitoring()
    {
        isMonitoring = true;
        lastSampleTime = EditorApplication.timeSinceStartup;
        Debug.Log("[FE-30 内存快照] 监控已启动，每 5 秒采样一次");
        TakeSnapshot("初始");
    }

    private void StopMonitoring()
    {
        isMonitoring = false;
        Debug.Log($"[FE-30 内存快照] 监控已停止，共 {sampleCount} 次采样");
    }

    private void TakeSnapshot(string label)
    {
        // 在 Editor 模式下，我们统计当前 Scene 中所有对象
        // 实际运行时需要在 Player 中运行 RuntimeMemorySampler

        var textures = FindObjectsByType<Texture>(FindObjectsSortMode.None).Where(t => t != null).ToList();
        var meshes = FindObjectsByType<Mesh>(FindObjectsSortMode.None).Where(m => m != null).ToList();
        var materials = FindObjectsByType<Material>(FindObjectsSortMode.None).Where(m => m != null && !m.isEditorResources).ToList();
        var gameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g != null).ToList();
        var graphics = FindObjectsByType<Graphic>(FindObjectsSortMode.None).Where(g => g != null).ToList();

        // 计算纹理内存
        long totalTexMemory = 0;
        foreach (var tex in textures)
        {
            if (tex is Texture2D t2d)
            {
                totalTexMemory += (long)t2d.width * t2d.height * 4; // RGBA32
            }
        }

        sampleCount++;
        var sample = new MemorySample
        {
            id = sampleCount,
            label = label,
            time = EditorApplication.timeSinceStartup,
            timeStr = EditorApplication.timeSinceStartup.ToString("F1") + "s",
            textureCount = textures.Count,
            textureMemoryMB = totalTexMemory / (1024f * 1024f),
            meshCount = meshes.Count,
            materialCount = materials.Count,
            gameObjectCount = gameObjects.Count,
            graphicCount = graphics.Count
        };

        snapshotHistory.Add(sample);

        Debug.Log($"[FE-30 内存快照 #{sampleCount}] {label}: " +
            $"Tex={sample.textureCount}({sample.textureMemoryMB:F1}MB) " +
            $"Mesh={sample.meshCount} " +
            $"Mat={sample.materialCount} " +
            $"GO={sample.gameObjectCount} " +
            $"Gfx={sample.graphicCount}");
    }

    /// <summary>
    /// 检测某个指标是否持续增长（至少连续 2 次增加）
    /// </summary>
    private bool CheckTrend(Func<MemorySample, double> getValue)
    {
        if (snapshotHistory.Count < 3) return false;

        var recent = snapshotHistory.TakeLast(4).ToList();
        bool increasingCount = 0;
        for (int i = 1; i < recent.Count; i++)
        {
            if (getValue(recent[i]) > getValue(recent[i - 1]))
                increasingCount++;
        }
        // 如果连续 2 次以上增长，标记为可疑
        return increasingCount >= 2;
    }

    private void PrintReport()
    {
        if (snapshotHistory.Count == 0)
        {
            Debug.LogWarning("[FE-30 内存快照] 没有采样数据可输出");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("  内存快照报告 — FE-30");
        sb.AppendLine($"  采样次数: {sampleCount}");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        sb.AppendLine($"{"#",-4} {"时间",-8} {"Texture",-10} {"TexMB",-8} {"Mesh",-6} {"Material",-8} {"GameObject",-10} {"Graphic",-8}");
        sb.AppendLine(new string('-', 70));

        foreach (var s in snapshotHistory)
        {
            sb.AppendLine($"{s.id,-4} {s.timeStr,-8} {s.textureCount,-10} {s.textureMemoryMB,-8:F1} {s.meshCount,-6} {s.materialCount,-8} {s.gameObjectCount,-10} {s.graphicCount,-8}");
        }

        // 趋势分析
        if (snapshotHistory.Count >= 2)
        {
            var first = snapshotHistory[0];
            var last = snapshotHistory[snapshotHistory.Count - 1];

            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("  趋势分析（首次 vs 最近）");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            void PrintDiff(string name, double firstVal, double lastVal)
            {
                var diff = lastVal - firstVal;
                var sign = diff >= 0 ? "+" : "";
                var arrow = diff > 0 ? "📈" : diff < 0 ? "📉" : "➡️";
                sb.AppendLine($"  {arrow} {name,-12} {firstVal,-10:F1} → {lastVal,-10:F1} ({sign}{diff:F1})");
            }

            PrintDiff("Texture 数量", first.textureCount, last.textureCount);
            PrintDiff("Texture 内存(MB)", first.textureMemoryMB, last.textureMemoryMB);
            PrintDiff("Mesh 数量", first.meshCount, last.meshCount);
            PrintDiff("Material 数量", first.materialCount, last.materialCount);
            PrintDiff("GameObject 数量", first.gameObjectCount, last.gameObjectCount);
            PrintDiff("Graphic 数量", first.graphicCount, last.graphicCount);
        }

        sb.AppendLine();
        var report = sb.ToString();
        Debug.Log(report);
        EditorGUIUtility.systemCopyBuffer = report;
        Debug.Log("[FE-30] 报告已复制到剪贴板");
    }

    private class MemorySample
    {
        public int id;
        public string label;
        public double time;
        public string timeStr;
        public int textureCount;
        public double textureMemoryMB;
        public int meshCount;
        public int materialCount;
        public int gameObjectCount;
        public int graphicCount;
    }
}
