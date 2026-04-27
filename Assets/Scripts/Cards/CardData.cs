using UnityEngine;

public enum CardType
{
    Hero,      // 英雄卡
    Attribute, // 属性卡
    Battle,    // 战斗卡
    Evolution  // 进化卡
}

public enum CardRarity
{
    White,  // 白
    Blue,   // 蓝
    Purple, // 紫
    Gold    // 金
}

public enum CardEffectId
{
    None,
    PowerTraining,    // 力量训练
    ArmorTraining,    // 坚固护甲
    SpeedTraining,    // 灵敏训练
    Slash,            // 斩击
    Reroll,           // 重摇
    ShieldBash,       // 护盾冲击
    FindWeakness,     // 寻找弱点
    EvolutionAwaken   // 进化觉醒
}

[CreateAssetMenu(fileName = "CardData", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public CardType cardType;
    public CardRarity rarity;
    public CardEffectId effectId;

    [Header("消耗")]
    public int cost; // 消耗的骰子点数（MVP中战斗卡/进化卡有消耗，属性卡无消耗）

    [Header("效果")]
    public int effectValue; // 属性加成值 / 伤害倍率 * 100

    [Header("骰子联动")]
    public DiceCombinationType requiredCombo; // 需要的组合类型（None=不需要）
    public float comboMultiplier = 1.5f;      // 联动时的倍率

    [Header("外观")]
    public Sprite icon;

    [TextArea]
    public string description;
}
