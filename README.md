# GameSXCG

Unity 2022.3 LTS 游戏项目

## 目录结构

```
game_sxcg/
│
├── Assets/                 # Unity 资源目录
│   ├── Art/                 # 美术资源（Sprite、Audio、Animation）
│   ├── Prefabs/             # 预制体
│   ├── Resources/           # 运行时动态加载资源
│   ├── Scenes/              # 场景文件
│   └── Scripts/             # C# 脚本
│       └── autoload/        # 全局管理器
│
├── Packages/               # UPM 包管理
├── ProjectSettings/        # 项目配置
├── docs/                   # 项目文档
│   ├── planning/          # 规划文档
│   ├── versions/          # 版本记录
│   └── management/        # 管理流程
└── exports/                # 构建输出（CI/CD）
```

## 技术栈

- **引擎**: Unity 2022.3 LTS
- **编辑器**: VS 2022 / VS Code + C# Dev Kit
- **版本控制**: Git + 分支模型

## 分支规范

| 分支 | 说明 |
|------|------|
| `main` | 发布/稳定分支 |
| `dev`  | 日常开发（所有功能合并到此） |
| `feat/*` | 功能分支（从 dev 切出） |
| `fix/*`  | Bug 修复分支 |
| `hotfix/*` | 紧急修复（从 main 切出） |

## 快速开始

1. 安装 Unity Hub + Unity 2022.3 LTS
2. 克隆本仓库：`git clone git@github.com:lveniu/game_sxcg.git`
3. 用 Unity Hub 打开 `game_sxcg` 文件夹
4. 打开 `Assets/Scenes/MainScene`
5. 点击 Play

## 相关文档

- [游戏规划](docs/planning/GAME_DESIGN.md)
- [版本记录](docs/versions/CHANGELOG.md)
- [开发管理](docs/management/WORKFLOW.md)
