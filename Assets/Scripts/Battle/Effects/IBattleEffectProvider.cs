using UnityEngine;

/// <summary>
/// 战斗特效提供者接口 — 预留后续替换为正式美术资源
/// 默认实现为 BattleEffectFactory（纯代码UI特效）
/// 后续可替换为 ParticleSystem / AssetBundle / Addressables 等方案
/// </summary>
public interface IBattleEffectProvider
{
    /// <summary>
    /// 在指定位置播放指定类型的特效
    /// </summary>
    /// <param name="pos">世界坐标位置</param>
    /// <param name="effectType">特效类型标识（Hit/Heal/Shield/Crit/Death/LevelUp）</param>
    void PlayEffect(Vector3 pos, string effectType);
}
