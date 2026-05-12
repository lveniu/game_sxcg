using System;
using System.Collections.Generic;

/// <summary>
/// 配置化随机事件数据 — 用于新事件系统的结构化定义
/// 每个事件包含标题、描述、风味文字、选项列表和关卡范围
/// </summary>
[Serializable]
public class RandomEventData
{
    /// <summary>事件唯一标识符</summary>
    public string eventId;

    /// <summary>事件标题（显示在面板顶部）</summary>
    public string title;

    /// <summary>事件描述（主要内容文本）</summary>
    public string description;

    /// <summary>风味文字（补充叙事/氛围文字）</summary>
    public string flavorText;

    /// <summary>事件选项列表（玩家可选择的行动）</summary>
    public List<EventChoice> choices = new List<EventChoice>();

    /// <summary>最小出现关卡（含），0表示无限制</summary>
    public int minLevel;

    /// <summary>最大出现关卡（含），0表示无限制</summary>
    public int maxLevel;

    /// <summary>出现权重（用于加权随机选择，值越大出现概率越高）</summary>
    public float weight = 1f;
}

/// <summary>
/// 事件选项 — 玩家在事件中可选择的具体行动
/// 每个选项包含文字描述和触发效果列表
/// </summary>
[Serializable]
public class EventChoice
{
    /// <summary>选项显示文字</summary>
    public string choiceText;

    /// <summary>选择后触发的效果列表（按顺序依次执行）</summary>
    public List<EventEffect> effects = new List<EventEffect>();
}

/// <summary>
/// 事件效果 — 选项触发后的具体效果定义
/// 支持 gold/heal/damage/card/relic/dice/enhance/exp 等类型
/// 每个效果有独立概率判定
/// </summary>
[Serializable]
public class EventEffect
{
    /// <summary>
    /// 效果类型，支持以下值：
    /// gold    — 增减金币
    /// heal    — 治疗英雄
    /// damage  — 对英雄造成伤害
    /// card    — 获得随机卡牌
    /// relic   — 获得随机遗物
    /// dice    — 骰子面升级
    /// enhance — 装备强化
    /// exp     — 英雄获得经验
    /// buff_atk  — 攻击力增益
    /// buff_def  — 防御力增益
    /// </summary>
    public string type;

    /// <summary>效果数值（金币数量/治疗量/伤害值/经验值等）</summary>
    public int value;

    /// <summary>
    /// 效果目标，支持以下值：
    /// self        — 当前/随机一个英雄
    /// all_heroes  — 全体英雄
    /// random_hero — 随机一个存活英雄
    /// inventory   — 玩家背包（默认）
    /// </summary>
    public string target = "self";

    /// <summary>触发概率（0~1），默认1表示必定触发</summary>
    public float probability = 1f;

    /// <summary>概率判定失败时显示的文字</summary>
    public string failText;
}
