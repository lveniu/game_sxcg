using UnityEngine;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// UI数值显示桥接层 — 前端面板通过此类获取所有显示用数值
    /// 
    /// 设计原则：
    /// 1. 前端面板不直接读JSON，也不直接依赖GameBalance
    /// 2. 所有显示数值统一从这里获取
    /// 3. 数据源已切换为ConfigLoader，从JSON配置读取
    /// 
    /// 降级策略：JSON加载失败时回退到内置默认值
    /// </summary>
    public static class UIConfigBridge
    {
        // ========== 英雄选择面板数据（从 hero_classes.json 读取） ==========

        /// <summary>
        /// 获取英雄卡片显示数据
        /// </summary>
        public static HeroDisplayData GetHeroDisplayData(HeroClass heroClass)
        {
            // 尝试从JSON读取
            var heroCfg = ConfigLoader.LoadHeroClasses();
            if (heroCfg?.classes != null)
            {
                string classId = heroClass.ToString().ToLower();
                var entry = heroCfg.classes.Find(c => c.role?.ToLower() == classId || c.id == classId);
                if (entry != null)
                {
                    return new HeroDisplayData
                    {
                        heroClass = heroClass,
                        displayName = entry.name_cn,
                        className = entry.name_cn,
                        description = entry.description ?? "",
                        icon = entry.icon_ref ?? $"{classId}_icon",
                        color = HexToColor(entry.color ?? GetDefaultClassColor(heroClass)),
                        stats = new HeroStats
                        {
                            maxHealth = entry.base_stats.max_health,
                            attack = entry.base_stats.attack,
                            defense = entry.base_stats.defense,
                            speed = entry.base_stats.speed,
                            critRate = entry.base_stats.crit_rate
                        },
                        summonCost = entry.summon_cost
                    };
                }
            }

            // 硬编码回退
            return heroClass switch
            {
                HeroClass.Warrior => new HeroDisplayData
                {
                    heroClass = HeroClass.Warrior,
                    displayName = "铁壁战士",
                    className = "战士",
                    description = "高防高血，近战输出，队伍前排坦克",
                    icon = "warrior_icon",
                    color = HexToColor("#4A90D9"),
                    stats = new HeroStats { maxHealth = 150, attack = 8, defense = 10, speed = 6, critRate = 0.02f },
                    summonCost = 2
                },
                HeroClass.Mage => new HeroDisplayData
                {
                    heroClass = HeroClass.Mage,
                    displayName = "奥术法师",
                    className = "法师",
                    description = "远程AOE法术输出，群体伤害专家",
                    icon = "mage_icon",
                    color = HexToColor("#9B59B6"),
                    stats = new HeroStats { maxHealth = 70, attack = 12, defense = 3, speed = 8, critRate = 0.05f },
                    summonCost = 2
                },
                HeroClass.Assassin => new HeroDisplayData
                {
                    heroClass = HeroClass.Assassin,
                    displayName = "暗影刺客",
                    className = "刺客",
                    description = "高速爆发，闪避背刺，单体秒杀",
                    icon = "assassin_icon",
                    color = HexToColor("#E74C3C"),
                    stats = new HeroStats { maxHealth = 70, attack = 16, defense = 3, speed = 14, critRate = 0.12f },
                    summonCost = 1
                },
                _ => null
            };
        }

        /// <summary>
        /// 获取所有可选英雄数据
        /// </summary>
        public static HeroDisplayData[] GetAllHeroDisplayData()
        {
            // 尝试从JSON读取所有英雄
            var heroCfg = ConfigLoader.LoadHeroClasses();
            if (heroCfg?.classes != null)
            {
                var result = new List<HeroDisplayData>();
                foreach (var entry in heroCfg.classes)
                {
                    HeroClass cls = ParseHeroClass(entry.role);
                    result.Add(new HeroDisplayData
                    {
                        heroClass = cls,
                        displayName = entry.name_cn,
                        className = entry.name_cn,
                        description = entry.description ?? "",
                        icon = entry.icon_ref ?? $"{entry.id}_icon",
                        color = HexToColor(entry.color ?? GetDefaultClassColor(cls)),
                        stats = new HeroStats
                        {
                            maxHealth = entry.base_stats.max_health,
                            attack = entry.base_stats.attack,
                            defense = entry.base_stats.defense,
                            speed = entry.base_stats.speed,
                            critRate = entry.base_stats.crit_rate
                        },
                        summonCost = entry.summon_cost
                    });
                }
                if (result.Count > 0) return result.ToArray();
            }

            // 硬编码回退
            return new HeroDisplayData[]
            {
                GetHeroDisplayData(HeroClass.Warrior),
                GetHeroDisplayData(HeroClass.Mage),
                GetHeroDisplayData(HeroClass.Assassin)
            };
        }

        // ========== 骰子组合显示数据（从 dice_system.json 读取） ==========

        /// <summary>
        /// 获取骰子组合的显示信息
        /// </summary>
        public static DiceComboDisplayData GetComboDisplayData(DiceCombinationType comboType)
        {
            var diceCfg = ConfigLoader.LoadDiceSystem();
            if (diceCfg?.combinations != null)
            {
                string comboId = comboType.ToString().ToLower();
                var entry = diceCfg.combinations.Find(c => c.id == comboId);
                if (entry != null)
                {
                    return new DiceComboDisplayData
                    {
                        comboType = comboType,
                        nameCN = entry.name_cn ?? comboId,
                        description = entry.effects?.GetValue("description_cn")?.ToString() ?? "",
                        borderColor = entry.visual != null && entry.visual.GetValue("border_color") != null
                            ? HexToColor(entry.visual.GetValue("border_color").ToString())
                            : GetDefaultComboColor(comboType),
                        glowIntensity = entry.visual != null && entry.visual.GetValue("glow_intensity") != null
                            ? (float)entry.visual.GetValue("glow_intensity")
                            : GetDefaultGlow(comboType),
                        sortPriority = entry.sort_priority
                    };
                }
            }

            // 硬编码回退
            return comboType switch
            {
                DiceCombinationType.ThreeOfAKind => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.ThreeOfAKind,
                    nameCN = "三条",
                    description = "全体攻击+50%，持续3回合",
                    borderColor = HexToColor("#FFD700"),
                    glowIntensity = 1.0f,
                    sortPriority = 1
                },
                DiceCombinationType.Straight => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.Straight,
                    nameCN = "顺子",
                    description = "全体攻速+20%，闪避+15%，持续2回合",
                    borderColor = HexToColor("#3498DB"),
                    glowIntensity = 0.8f,
                    sortPriority = 2
                },
                DiceCombinationType.Pair => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.Pair,
                    nameCN = "对子",
                    description = "对敌方攻击最高单位造成2倍伤害",
                    borderColor = HexToColor("#2ECC71"),
                    glowIntensity = 0.5f,
                    sortPriority = 3
                },
                _ => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.None,
                    nameCN = "散牌",
                    description = "无加成，骰子点数之和提供微量攻击加成",
                    borderColor = HexToColor("#95A5A6"),
                    glowIntensity = 0.0f,
                    sortPriority = 4
                }
            };
        }

        // ========== 骰子特殊面显示数据（从 dice_system.json face_upgrade 读取） ==========

        /// <summary>
        /// 获取骰子特殊面的显示信息
        /// </summary>
        public static DiceFaceDisplayData GetSpecialFaceDisplay(string faceId)
        {
            var diceCfg = ConfigLoader.LoadDiceSystem();
            if (diceCfg?.face_upgrade?.special_faces != null)
            {
                var entry = diceCfg.face_upgrade.special_faces.Find(f => f.id == faceId);
                if (entry != null)
                {
                    return new DiceFaceDisplayData
                    {
                        nameCN = entry.name_cn ?? faceId,
                        color = GetSpecialFaceColor(faceId),
                        description = entry.effect ?? ""
                    };
                }
            }

            // 硬编码回退
            return faceId switch
            {
                "lightning" => new DiceFaceDisplayData { nameCN = "⚡闪电", color = HexToColor("#F1C40F"), description = "连锁闪电x3" },
                "shield" => new DiceFaceDisplayData { nameCN = "🛡护盾", color = HexToColor("#3498DB"), description = "全体护盾+15%" },
                "heal" => new DiceFaceDisplayData { nameCN = "💚治疗", color = HexToColor("#2ECC71"), description = "全体回复10%" },
                "poison" => new DiceFaceDisplayData { nameCN = "☠毒素", color = HexToColor("#9B59B6"), description = "中毒5%/回合" },
                "critical" => new DiceFaceDisplayData { nameCN = "💥暴击", color = HexToColor("#E74C3C"), description = "必暴+50%爆伤" },
                _ => new DiceFaceDisplayData { nameCN = $"骰子{faceId}", color = Color.white, description = "" }
            };
        }

        // ========== 遗物稀有度显示（从 relics.json rarity_weights 读取） ==========

        /// <summary>
        /// 获取遗物稀有度显示信息
        /// </summary>
        public static string GetRarityNameCN(int rarity)
        {
            return rarity switch
            {
                1 => "普通",
                2 => "稀有",
                3 => "史诗",
                4 => "传说",
                _ => "未知"
            };
        }

        /// <summary>
        /// 遗物稀有度字符串转int
        /// </summary>
        public static int RarityToInt(string rarity)
        {
            return rarity?.ToLower() switch
            {
                "common" => 1,
                "rare" => 2,
                "epic" => 3,
                "legendary" => 4,
                _ => 0
            };
        }

        public static Color GetRarityColor(int rarity)
        {
            return rarity switch
            {
                1 => new Color(0.85f, 0.85f, 0.85f),
                2 => new Color(0.26f, 0.53f, 0.96f),
                3 => new Color(0.64f, 0.21f, 0.93f),
                4 => new Color(1f, 0.75f, 0f),
                _ => Color.white
            };
        }

        public static string GetRarityStars(int rarity)
        {
            return rarity switch
            {
                1 => "★",
                2 => "★★",
                3 => "★★★",
                4 => "★★★★",
                _ => ""
            };
        }

        // ========== 战斗显示工具 ==========

        /// <summary>
        /// 获取职业图标Emoji
        /// </summary>
        public static string GetClassIcon(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "⚔",
                HeroClass.Mage => "🔮",
                HeroClass.Assassin => "🗡",
                _ => "❓"
            };
        }

        /// <summary>
        /// 获取职业主题色
        /// </summary>
        public static Color GetClassColor(HeroClass heroClass)
        {
            // 尝试从JSON读取
            var heroCfg = ConfigLoader.LoadHeroClasses();
            if (heroCfg?.classes != null)
            {
                string classId = heroClass.ToString().ToLower();
                var entry = heroCfg.classes.Find(c => c.role?.ToLower() == classId || c.id == classId);
                if (entry != null && !string.IsNullOrEmpty(entry.color))
                    return HexToColor(entry.color);
            }

            return heroClass switch
            {
                HeroClass.Warrior => new Color(0.9f, 0.4f, 0.3f),
                HeroClass.Mage => new Color(0.3f, 0.5f, 0.9f),
                HeroClass.Assassin => new Color(0.7f, 0.3f, 0.9f),
                _ => Color.white
            };
        }

        /// <summary>
        /// 格式化属性值显示（百分比/整数自动选择）
        /// </summary>
        public static string FormatStat(StatType statType, float value)
        {
            return statType switch
            {
                StatType.CritRate => $"{Mathf.RoundToInt(value * 100)}%",
                _ => Mathf.RoundToInt(value).ToString()
            };
        }

        /// <summary>
        /// 格式化属性名称中文
        /// </summary>
        public static string GetStatNameCN(StatType statType)
        {
            return statType switch
            {
                StatType.Health => "生命",
                StatType.Attack => "攻击",
                StatType.Defense => "防御",
                StatType.Speed => "速度",
                StatType.CritRate => "暴击率",
                _ => "属性"
            };
        }

        // ========== 遗物显示桥接（从 relics.json 读取） ==========

        /// <summary>
        /// 从RelicData转为前端显示数据
        /// </summary>
        public static RelicDisplayData GetRelicDisplayData(RelicData data)
        {
            if (data == null) return null;
            return new RelicDisplayData
            {
                relicId = data.relicId,
                relicName = data.relicName,
                description = data.description,
                iconEmoji = GetRelicEmoji(data.effectType),
                rarity = data.rarity,
                rarityName = GetRarityNameCN(data.rarity),
                rarityColor = GetRarityColor(data.rarity),
                effectDescription = GetEffectDescription(data.effectType, data.effectValue)
            };
        }

        /// <summary>
        /// 遗物效果类型 → Emoji图标
        /// </summary>
        public static string GetRelicEmoji(RelicEffectType type)
        {
            return type switch
            {
                RelicEffectType.AttackBoost => "⚔",
                RelicEffectType.DefenseBoost => "🛡",
                RelicEffectType.HealthBoost => "❤",
                RelicEffectType.SpeedBoost => "💨",
                RelicEffectType.CritBoost => "💥",
                RelicEffectType.BattleStartShield => "🔰",
                RelicEffectType.LifeSteal => "🧛",
                RelicEffectType.Thorns => "🌵",
                RelicEffectType.PoisonAttack => "☠",
                RelicEffectType.GiantSlayer => "🗡",
                RelicEffectType.ExtraReroll => "🎲",
                RelicEffectType.ComboBoost => "✨",
                RelicEffectType.DoubleReward => "💰",
                RelicEffectType.Revive => "🕊",
                _ => "🏺"
            };
        }

        /// <summary>
        /// 遗物效果类型 + 数值 → 可读描述
        /// </summary>
        public static string GetEffectDescription(RelicEffectType type, float value)
        {
            return type switch
            {
                RelicEffectType.AttackBoost => $"攻击力+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.DefenseBoost => $"防御力+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.HealthBoost => $"生命值+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.SpeedBoost => $"速度+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.CritBoost => $"暴击率+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.BattleStartShield => $"开局护盾+{Mathf.RoundToInt(value * 100)}%生命",
                RelicEffectType.LifeSteal => $"吸血{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.Thorns => $"反伤{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.PoisonAttack => $"攻击附带中毒{Mathf.RoundToInt(value)}%/回合",
                RelicEffectType.GiantSlayer => $"对高血量敌人额外伤害+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.ExtraReroll => $"重摇次数+{Mathf.RoundToInt(value)}",
                RelicEffectType.ComboBoost => "散牌升级为对子",
                RelicEffectType.DoubleReward => $"{Mathf.RoundToInt(value * 100)}%概率双倍奖励",
                RelicEffectType.Revive => $"每关复活{Mathf.RoundToInt(value)}次",
                _ => $"效果值: {value}"
            };
        }

        // ========== 结算面板数据（从 drop_tables.json gold_rewards + levels.json 读取） ==========

        /// <summary>
        /// 获取关卡金币奖励（从 drop_tables.json gold_rewards 计算）
        /// 公式: base_gold + level * level_bonus + 里程碑加成
        /// </summary>
        public static int GetGoldReward(int level)
        {
            var dropCfg = ConfigLoader.LoadDropTables();
            var goldCfg = dropCfg?.gold_rewards;
            if (goldCfg != null)
            {
                int baseGold = goldCfg.base_gold;
                int levelBonus = goldCfg.level_bonus;
                int total = baseGold + level * levelBonus;

                // 里程碑加成
                if (goldCfg.milestone_bonus != null)
                {
                    string levelKey = level.ToString();
                    if (goldCfg.milestone_bonus.TryGetValue(levelKey, out int bonus))
                        total += bonus;
                }

                return total;
            }

            // 硬编码回退
            return 20 + level * 10;
        }

        /// <summary>
        /// 获取关卡显示数据（关卡名、难度、敌人池等）
        /// </summary>
        public static LevelDisplayData GetLevelDisplayData(int level)
        {
            var levelsCfg = ConfigLoader.LoadLevels();
            if (levelsCfg?.level_templates != null)
            {
                foreach (var kvp in levelsCfg.level_templates)
                {
                    var tier = kvp.Value;
                    if (tier?.range != null && level >= tier.range[0] && level <= tier.range[1])
                    {
                        bool isBoss = tier.boss_levels != null && tier.boss_levels.Contains(level);
                        return new LevelDisplayData
                        {
                            levelId = level,
                            tierName = tier._description ?? kvp.Key,
                            enemyPool = tier.enemy_pool ?? new List<string>(),
                            maxEnemies = tier.max_enemies,
                            isBoss = isBoss,
                            allowElite = tier.allow_elite,
                            allowMechanic = tier.allow_mechanic
                        };
                    }
                }
            }

            // 硬编码回退
            return new LevelDisplayData
            {
                levelId = level,
                tierName = $"第{level}关",
                enemyPool = new List<string>(),
                maxEnemies = 3,
                isBoss = level % 5 == 0,
                allowElite = level >= 3,
                allowMechanic = level >= 6
            };
        }

        /// <summary>
        /// 获取关卡标题文本（"第 N 关 - 章节名"）
        /// </summary>
        public static string GetLevelTitle(int level)
        {
            var data = GetLevelDisplayData(level);
            if (data.isBoss)
                return $"⚔ 第 {level} 关 — BOSS战";
            return $"第 {level} 关 — {data.tierName}";
        }

        /// <summary>
        /// 获取关卡难度系数文本
        /// </summary>
        public static string GetDifficultyText(int level)
        {
            float diff = BalanceProvider.GetLevelDifficulty(level);
            if (diff < 0.5f) return "简单";
            if (diff < 1.0f) return "普通";
            if (diff < 1.5f) return "困难";
            if (diff < 2.0f) return "噩梦";
            return "地狱";
        }

        /// <summary>
        /// 获取关卡难度颜色
        /// </summary>
        public static Color GetDifficultyColor(int level)
        {
            float diff = BalanceProvider.GetLevelDifficulty(level);
            if (diff < 0.5f) return new Color(0.5f, 0.9f, 0.5f);
            if (diff < 1.0f) return new Color(0.9f, 0.9f, 0.3f);
            if (diff < 1.5f) return new Color(1f, 0.6f, 0.2f);
            if (diff < 2.0f) return new Color(0.9f, 0.2f, 0.2f);
            return new Color(0.8f, 0f, 0.8f);
        }

        // ========== 技能显示桥接（从 skills.json 读取） ==========

        /// <summary>
        /// 获取英雄主动技能显示数据
        /// </summary>
        public static SkillDisplayData GetHeroActiveSkillDisplay(HeroClass heroClass)
        {
            var skillsCfg = ConfigLoader.LoadSkills();
            if (skillsCfg?.hero_skills != null)
            {
                string classId = heroClass.ToString().ToLower();
                var entry = skillsCfg.hero_skills.Find(s => s.hero_class == classId);
                if (entry?.active_skill != null)
                {
                    return new SkillDisplayData
                    {
                        nameCN = entry.active_skill.name_cn ?? "",
                        descriptionCN = entry.active_skill.description_cn ?? "",
                        target = entry.active_skill.target_type ?? "single",
                        cooldown = entry.active_skill.cooldown_rounds,
                        multiplier = entry.active_skill.skill_multiplier
                    };
                }
            }

            // 硬编码回退
            return heroClass switch
            {
                HeroClass.Warrior => new SkillDisplayData { nameCN = "盾击", descriptionCN = "对单体造成1.5倍伤害并击晕1回合", target = "single", cooldown = 3, multiplier = 1.5f },
                HeroClass.Mage => new SkillDisplayData { nameCN = "火球术", descriptionCN = "对全体造成0.8倍伤害并附加灼烧", target = "aoe", cooldown = 4, multiplier = 0.8f },
                HeroClass.Assassin => new SkillDisplayData { nameCN = "暗杀", descriptionCN = "对单体造成2倍伤害，必定暴击", target = "single", cooldown = 3, multiplier = 2.0f },
                _ => new SkillDisplayData { nameCN = "普攻", descriptionCN = "", target = "single", cooldown = 0, multiplier = 1.0f }
            };
        }

        /// <summary>
        /// 获取英雄被动技能显示数据
        /// </summary>
        public static SkillDisplayData GetHeroPassiveDisplay(HeroClass heroClass)
        {
            var skillsCfg = ConfigLoader.LoadSkills();
            if (skillsCfg?.hero_skills != null)
            {
                string classId = heroClass.ToString().ToLower();
                var entry = skillsCfg.hero_skills.Find(s => s.hero_class == classId);
                if (entry?.passive != null)
                {
                    return new SkillDisplayData
                    {
                        nameCN = entry.passive.name_cn ?? "",
                        descriptionCN = entry.passive.description_cn ?? "",
                        target = "self",
                        cooldown = entry.passive.cooldown_rounds,
                        multiplier = 0f
                    };
                }
            }

            // 硬编码回退
            return heroClass switch
            {
                HeroClass.Warrior => new SkillDisplayData { nameCN = "坚韧", descriptionCN = "防御+20%，受击时10%概率触发护盾", target = "self", cooldown = 0 },
                HeroClass.Mage => new SkillDisplayData { nameCN = "奥术亲和", descriptionCN = "暴击率+5%，暴击时回复10%血量", target = "self", cooldown = 0 },
                HeroClass.Assassin => new SkillDisplayData { nameCN = "暗影步", descriptionCN = "速度+15%，闪避后下次攻击必暴", target = "self", cooldown = 0 },
                _ => new SkillDisplayData { nameCN = "无", descriptionCN = "", target = "self", cooldown = 0 }
            };
        }

        /// <summary>
        /// 获取骰子组合技能显示数据（从 skills.json dice_combo_skills 读取）
        /// </summary>
        public static SkillDisplayData GetDiceComboSkillDisplay(string comboId)
        {
            var skillsCfg = ConfigLoader.LoadSkills();
            if (skillsCfg?.dice_combo_skills?.skills != null)
            {
                var entry = skillsCfg.dice_combo_skills.skills.Find(s => s.combo_id == comboId);
                if (entry != null)
                {
                    return new SkillDisplayData
                    {
                        nameCN = entry.skill_name_cn ?? comboId,
                        descriptionCN = entry.description_cn ?? "",
                        target = entry.target ?? "all",
                        cooldown = 0,
                        multiplier = entry.damage_multiplier > 0 ? entry.damage_multiplier : entry.attack_bonus_pct
                    };
                }
            }

            return new SkillDisplayData { nameCN = comboId, descriptionCN = "", target = "all", cooldown = 0 };
        }

        /// <summary>
        /// 获取骰子组合技能使用次数限制文本
        /// </summary>
        public static string GetComboSkillLimitText()
        {
            int limit = BalanceProvider.GetDiceComboSkillUsageLimit();
            return $"每场战斗限用{limit}次";
        }

        // ========== 战斗显示工具 ==========

        /// <summary>
        /// 格式化战斗时间显示
        /// </summary>
        public static string FormatBattleTime(float seconds)
        {
            if (seconds < 60f)
                return $"{seconds:F1}秒";
            int min = (int)(seconds / 60f);
            float sec = seconds % 60f;
            return $"{min}分{sec:F0}秒";
        }

        /// <summary>
        /// 获取战斗速度档位显示文本
        /// </summary>
        public static string[] GetSpeedLabels()
        {
            var speeds = BalanceProvider.GetSpeedOptions();
            var labels = new string[speeds.Count];
            for (int i = 0; i < speeds.Count; i++)
                labels[i] = $"{speeds[i]}x";
            return labels;
        }

        // ========== 奖励选项描述 ==========

        /// <summary>
        /// 生成奖励选项的富文本描述（用于卡片和弹窗）
        /// </summary>
        public static string GetRewardRichDescription(RewardOption reward)
        {
            if (reward == null) return "";
            return reward.Type switch
            {
                RewardType.NewUnit => $"招募新英雄加入队伍\n星级: {reward.Rarity}★",
                RewardType.DiceFaceUpgrade => $"骰子{reward.DiceIndex + 1} #{reward.FaceIndex + 1}面 → {reward.NewFaceValue}",
                RewardType.StatBoost => $"{GetStatNameCN(reward.BoostStat)}+{Mathf.RoundToInt(reward.BoostAmount * 100)}%\n({(reward.BoostTarget == StatBoostTarget.AllHeroes ? "全体" : "随机单体")})",
                RewardType.Relic => $"获得遗物\n{reward.Description}",
                _ => reward.Description ?? ""
            };
        }

        // ========== 工具方法 ==========

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.white;
        }

        private static HeroClass ParseHeroClass(string role)
        {
            if (string.IsNullOrEmpty(role)) return HeroClass.Warrior;
            return role.ToLower() switch
            {
                "warrior" or "战士" or "tank" => HeroClass.Warrior,
                "mage" or "法师" or "support" => HeroClass.Mage,
                "assassin" or "刺客" or "dps" => HeroClass.Assassin,
                _ => HeroClass.Warrior
            };
        }

        private static string GetDefaultClassColor(HeroClass cls)
        {
            return cls switch
            {
                HeroClass.Warrior => "#4A90D9",
                HeroClass.Mage => "#9B59B6",
                HeroClass.Assassin => "#E74C3C",
                _ => "#FFFFFF"
            };
        }

        private static Color GetDefaultComboColor(DiceCombinationType combo)
        {
            return combo switch
            {
                DiceCombinationType.ThreeOfAKind => HexToColor("#FFD700"),
                DiceCombinationType.Straight => HexToColor("#3498DB"),
                DiceCombinationType.Pair => HexToColor("#2ECC71"),
                _ => HexToColor("#95A5A6")
            };
        }

        private static float GetDefaultGlow(DiceCombinationType combo)
        {
            return combo switch
            {
                DiceCombinationType.ThreeOfAKind => 1.0f,
                DiceCombinationType.Straight => 0.8f,
                DiceCombinationType.Pair => 0.5f,
                _ => 0.0f
            };
        }

        private static Color GetSpecialFaceColor(string faceId)
        {
            return faceId switch
            {
                "lightning" => HexToColor("#F1C40F"),
                "shield" => HexToColor("#3498DB"),
                "heal" => HexToColor("#2ECC71"),
                "poison" => HexToColor("#9B59B6"),
                "critical" => HexToColor("#E74C3C"),
                _ => Color.white
            };
        }
    }

    // ========== 数据结构 ==========

    /// <summary>
    /// 英雄卡片显示数据（英雄选择面板使用）
    /// </summary>
    public class HeroDisplayData
    {
        public HeroClass heroClass;
        public string displayName;
        public string className;
        public string description;
        public string icon;
        public Color color;
        public HeroStats stats;
        public int summonCost;
    }

    /// <summary>
    /// 英雄属性显示数据
    /// </summary>
    public struct HeroStats
    {
        public int maxHealth;
        public int attack;
        public int defense;
        public int speed;
        public float critRate;
    }

    /// <summary>
    /// 骰子组合显示数据
    /// </summary>
    public class DiceComboDisplayData
    {
        public DiceCombinationType comboType;
        public string nameCN;
        public string description;
        public Color borderColor;
        public float glowIntensity;
        public int sortPriority;
    }

    /// <summary>
    /// 骰子特殊面显示数据
    /// </summary>
    public class DiceFaceDisplayData
    {
        public string nameCN;
        public Color color;
        public string description;
    }

    // ========== 奖励类型图标 ==========

    /// <summary>
    /// 获取奖励类型的图标emoji和主题色
    /// </summary>
    public static class RewardTypeIcons
    {
        public static string GetIcon(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => "👤",
                RewardType.DiceFaceUpgrade => "🎲",
                RewardType.StatBoost => "📈",
                RewardType.Relic => "🏺",
                _ => "❓"
            };
        }

        public static Color GetColor(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => new Color(0.3f, 0.8f, 0.4f),
                RewardType.DiceFaceUpgrade => new Color(0.2f, 0.6f, 1f),
                RewardType.StatBoost => new Color(1f, 0.6f, 0.2f),
                RewardType.Relic => new Color(0.8f, 0.4f, 1f),
                _ => Color.white
            };
        }

        public static string GetTypeLabel(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => "新单位",
                RewardType.DiceFaceUpgrade => "骰子强化",
                RewardType.StatBoost => "属性强化",
                RewardType.Relic => "遗物",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 关卡显示数据（结算面板/战斗面板使用）
    /// </summary>
    public class LevelDisplayData
    {
        public int levelId;
        public string tierName;
        public List<string> enemyPool;
        public int maxEnemies;
        public bool isBoss;
        public bool allowElite;
        public bool allowMechanic;
    }

    /// <summary>
    /// 技能显示数据（英雄选择面板/战斗面板使用）
    /// </summary>
    public class SkillDisplayData
    {
        public string nameCN;
        public string descriptionCN;
        public string target;     // single / aoe / self / all
        public int cooldown;
        public float multiplier;
    }

    /// <summary>
    /// 遗物图标显示数据（BattlePanel遗物栏 / RewardPanel详情弹窗使用）
    /// </summary>
    public class RelicDisplayData
    {
        public string relicId;
        public string relicName;
        public string description;
        public string iconEmoji;
        public int rarity;
        public string rarityName;
        public Color rarityColor;
        public string effectDescription;
    }
}
