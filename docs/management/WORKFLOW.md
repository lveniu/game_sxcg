# 开发管理流程

## Git 工作流

```
main  ------o-----------o-----------o----
             \         /           /
dev    ------o---o---o---o---o---o---
                  \     /     /
feat/xxx           o---o     /
                              \
fix/xxx                        o---o
```

### 分支规范

1. **main**: 只接受从 dev 合并的 PR，用于发布
2. **dev**: 日常开发主分支
3. **feat/名称**: 新功能开发（从 dev 切出）
4. **fix/名称**: Bug 修复（从 dev 切出）
5. **hotfix/名称**: 紧急修复（从 main 切出，修复后合并到 main 和 dev）

### 提交规范

提交信息格式：

```
<type>(<scope>): <subject>

<body>

<footer>
```

**type 类型:**

| 类型 | 说明 |
|------|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档更新 |
| `style` | 代码格式（不影响功能） |
| `refactor` | 重构 |
| `perf` | 性能优化 |
| `test` | 测试相关 |
| `chore` | 构建/工具更新 |

**示例:**

```
feat(player): 添加角色移动控制

- 实现 WASD 移动
- 添加跑步加速
- 添加角色动画

Closes #12
```

## 发布流程

1. 确认 dev 分支稳定
2. 修改 `ProjectSettings` 中的版本号
3. 更新 `CHANGELOG.md`
4. 从 dev 发起 PR 到 main
5. 合并后打 Tag: `git tag -a v1.0.0 -m "Release v1.0.0"`
6. 推送 Tag: `git push origin v1.0.0`

## 代码审查

- 所有 PR 需要至少1人 review
- 通过 CI 检查
- 解决所有冲突后合并
