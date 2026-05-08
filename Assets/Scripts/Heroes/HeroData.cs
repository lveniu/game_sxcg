using UnityEngine;

public enum HeroClass
{
    Warrior,   // 战士 — 高防高血，近战输出
    Mage,      // 法师 — 远程AOE法术输出
    Assassin   // 刺客 — 高速爆发，闪避背刺
}

[CreateAssetMenu(fileName = "HeroData", menuName = "Game/Hero Data")]
public class HeroData : ScriptableObject
{
    public string heroName;
    public HeroClass heroClass;

    [Header("基础属性")]
    public int baseHealth = 100;
    public int baseAttack = 10;
    public int baseDefense = 5;
    public int baseSpeed = 10;
    public float baseCritRate = 0.05f;

    [Header("召唤消耗")]
    public int summonCost = 2; // 战士/法师2点，刺客1点

    [Header("技能")]
    public SkillData normalAttack;
    public SkillData activeSkill;

    [Header("外观")]
    public Sprite icon;
    public GameObject prefab;

    [TextArea]
    public string description;
}
