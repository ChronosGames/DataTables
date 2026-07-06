# localized 表类型设计

`localized` 是计划中的多语言文本资源表类型。本文档仅描述原型设计；在实现完成前，当前生成器仍应对 `DTGen=localized` 给出“预留类型”诊断，而不是生成代码或数据。

## 目标

- 把多语言翻译集中在一张便于策划和本地化团队维护的表中。
- 生成强类型查询 API，减少业务代码中的裸字符串调用。
- 支持确定性的语言 fallback。
- 支持客户端只加载当前语言，也支持工具或服务端一次性加载全部语言。
- 支持服务端按单语言导出或按全语言导出。

## 推荐 Excel 格式

| Key | zh-CN | en-US | ja-JP | Comment |
| --- | --- | --- | --- | --- |
| Ui.Start | 开始 | Start | スタート | 主菜单开始按钮 |
| Ui.Quit | 退出 | Quit | 終了 | 主菜单退出按钮 |
| Item.Sword.Name | 铁剑 | Iron Sword | 鉄の剣 | 道具显示名 |

规则：

- `Key` 必填、唯一，并且应在版本迭代中保持稳定。
- 语言列使用 BCP-47 风格命名，例如 `en-US`、`zh-CN`。
- `Comment` 为可选说明列；除非启用调试导出模式，否则不进入运行时 payload。
- 翻译单元格允许为空，但前提是 fallback 链能解析出可用文本。

## 生成代码形态

生成表应同时提供基于 key 的查询 API 和便捷查询 API：

```csharp
DTLocalization.Get("Ui.Start");
DTLocalization.Get("Ui.Start", language: "en-US");
DTLocalization.TryGet("Ui.Start", out var value);
```

可选生成常量用于减少调用处的裸字符串：

```csharp
DTLocalization.Keys.Ui_Start
```

## 运行时查询与 fallback

运行时应按以下顺序解析文本：

1. 查询调用中显式传入的语言。
2. localization 表或 manager 当前配置的运行时语言。
3. 项目默认语言。
4. 如果项目配置了 invariant fallback 列，则使用该列。

严格模式下，缺失 key 应视为错误；缺失某语言翻译时，只有在 fallback 链无法解析出文本时才视为错误。

## 加载模式

### 当前语言加载

客户端构建可以只加载一个语言 payload，以降低内存占用。该模式推荐用于移动端和文本量较大的游戏客户端。

### 全语言加载

工具、测试、专用服务器和构建校验可以加载全部语言，用于完整性检查或下游资源导出。

## 服务端导出模式

服务端流水线应支持：

- `--language en-US`：只导出单个语言。
- `--all-languages`：导出所有语言列。
- `--validate-fallbacks`：当配置的 fallback 链无法产生可用文本时让构建失败。

## 诊断

实现时应输出结构化诊断，并至少包含文件、Sheet、单元格、key、语言、错误码和消息。重要诊断包括：

- 本地化 key 重复。
- 语言列名非法。
- 默认语言文本缺失。
- 翻译缺失且没有 fallback 可用。
- 不同语言之间的占位符不一致。

## 实现说明

`localized` 应作为普通注册式表 parser、validator 和 writer 实现。新增该类型时，不应在 `DataTableProcessor` 编排路径中加入特殊分支。
