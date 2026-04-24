# 游戏项目

Godot 4.3 项目模板

## 目录结构

```
├── .github/         # GitHub Actions 工作流
├── assets/          # 线索素材
├── docs/            # 项目文档
├── exports/         # 构建/打包输出
├── scenes/          # 场景文件 (.tscn)
│   ├── main.tscn    # 主场景
└── src/             # 源代码 (.gd)
    └── autoload/    # 全局自动加载脚本
```

## 分支规范

- `main`  - 发布/稳定分支
- `dev`   - 开发分支（日常开发）
- `feat/*` - 功能分支（从 dev 切出）
- `fix/*`  - Bug 修复分支

## 快速开始

1. 打开 Godot 4.3
2. 导入本项目
3. 运行 `scenes/main.tscn`
