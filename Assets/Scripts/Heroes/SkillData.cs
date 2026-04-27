using UnityEngine;

public enum SkillTargetType
{
    Single,  // 单体
    AOE,     // 范围
    Self     // 自身
}

public enum SkillEffectType
{
    Damage,  // 伤害
    Heal,    // 治疗
    Shield,  // 护盾
    Buff,    // 增益
    Debuff   // 减益
}

[CreateAssetMenu(fileName = "SkillData", menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public float damageMultiplier = 1.0f;
    public float cooldown = 3.0f;
    public SkillTargetType targetType = SkillTargetType.Single;
    public SkillEffectType effectType = SkillEffectType.Damage;
    public float effectValue = 0;

    [TextArea]
    public string description;
}
