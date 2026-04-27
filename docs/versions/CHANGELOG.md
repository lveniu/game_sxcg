# 版本变更日志

格式基于 [Keep a Changelog](https://keepachangelog.com/)

---

## [Unreleased]

### Added
- 初始化 Unity 2022.3 LTS 项目结构
- 配置 Git 分支模型（main / dev / feat / fix）
- 添加基础场景 MainScene
- 添加 GameManager 全局单例
- 完整游戏设计文档 (GDD.md)
- 完整MVP范围文档 (MVP.md)
- 完整UI/UX设计文档 (UI_UX_DESIGN.md)
- 5个基础英雄 + 5个进化形态
- 21张卡牌（属性卡/战斗卡/进化卡）
- 11种敌人（含Boss/精英/特殊性敌人）
- 装备系统（武器/防具/饰品，品质阶级）
- 商店系统（装备+卡牌购买，随机折扣）
- 随机事件系统（4种事件类型）
- 连携技系统（5种职业连携技）
- 复活机制（缺省复活卡牌）
- 伤害数字飘字效果

### Changed
- 优化 GameData.cs 结构：提取了3个辅助方法（CreateHeroFromTemplate/CreateEnemyFromTemplate/CreateCard）
- 减少重复代码：GameData.cs 从 972 行 → 558 行（-43%）
- 更新设计文档以反映当前完整项目范围

### Fixed
- 修复 AutoChessAI 中辅助单位的行动逻辑
- 修复数个编译错问题（方法签名、缺失定义等）

---

## 版本约定

- **MAJOR** (主版本) - 不兼容更新
- **MINOR** (次版本) - 功能增加（向后兼容）
- **PATCH** (补丁) - Bug 修复

## 标签约定

- `Added` - 新功能
- `Changed` - 现有功能变更
- `Deprecated` - 即将移除的功能
- `Removed` - 已移除的功能
- `Fixed` - Bug 修复
- `Security` - 安全相关
