# DataTables 文档中心

本文档中心按“用户路径 + 信息类型”组织，避免把指南、设计说明、参考资料和规划文档混放在同一层级。根目录 `README.md` 只保留项目入口、快速开始和核心示例；细节说明沉淀到本目录。

## 文档组织评审

当前项目原本已经具备较完整的主题文档，但所有 Markdown 文件平铺在 `docs/` 根目录下，存在以下问题：

- **入口不明确**：新用户需要从多个文件名中猜测阅读顺序。
- **信息类型混杂**：使用指南、设计草案、二进制格式参考和迁移说明位于同一层级，维护者难以判断文档状态。
- **扩展成本上升**：后续增加更多表类型、运行时能力或版本迁移文档时，根目录会继续膨胀。
- **相对链接易失效**：平铺结构短期简单，但缺少统一索引后很难发现跨文档链接是否仍然有效。

参考同类 .NET/Unity 开源库常见做法，本项目采用以下规范：

1. **根 README 面向首次使用者**：介绍项目价值、安装、快速开始、常用链接。
2. **docs/README 作为文档导航页**：解释文档结构、推荐阅读路径和维护规则。
3. **按文档目的分区**：指南、设计、参考、迁移、规划分别归档。
4. **长期稳定文档优先**：可执行的使用说明和协议参考优先放在清晰路径；阶段性计划单独放入 planning。
5. **目录名称使用英文小写复数**：便于 URL 稳定、跨平台一致和包站点集成。

## 推荐阅读路径

### 新用户

1. 阅读根目录 [README](../README.md) 完成安装和快速开始。
2. 阅读 [表类型指南](guides/table-types.md) 了解支持的 Excel 建模方式。
3. 按需阅读 [数据源管线](guides/data-source-pipeline.md) 配置本地、远程、缓存和回退链路。

### 内容/策划同学

1. 阅读 [Excel 模板与 Dicts 指南](../templates/README.md)。
2. 阅读 [表类型指南](guides/table-types.md)。
3. 根据使用的表类型阅读对应设计说明。

### 程序/工具链维护者

1. 阅读 [数据源管线](guides/data-source-pipeline.md)。
2. 阅读 [二进制格式 v3](reference/binary-format-v3.md)。
3. 需要升级时阅读 [v3 迁移指南](migration/migration-to-v3.md)。

## 目录结构

```text
docs/
├── README.md                         # 文档中心与维护规则
├── guides/                           # 面向使用者的操作指南
│   ├── data-source-pipeline.md
│   └── table-types.md
├── designs/                          # 表类型与功能设计说明
│   ├── graph-table-design.md
│   ├── kv-table-design.md
│   ├── localized-table-design.md
│   └── tree-table-design.md
├── reference/                        # 稳定协议、格式和 API 参考
│   └── binary-format-v3.md
├── migration/                        # 版本迁移文档
│   └── migration-to-v3.md
├── adr/                              # 长期架构决策记录
│   └── README.md
└── planning/                         # 阶段性计划和历史优化记录
    ├── README.md
    ├── agent-engineering-review-2026-07.md
    └── optimization-plan-2026-07.md
```

## 维护规范

- 新增“如何使用/如何配置”类文档放入 `docs/guides/`。
- 新增表类型、生成策略或架构方案放入 `docs/designs/`。
- 二进制格式、协议、稳定 API 说明放入 `docs/reference/`。
- 版本升级、破坏性变更处理放入 `docs/migration/`。
- 长期架构决策放入 `docs/adr/`。
- Roadmap、优化计划、阶段性调研放入 `docs/planning/`，并在 `docs/planning/README.md` 标注状态。
- 文档间链接使用相对路径；移动文件后必须同步更新链接。
- 根目录只保留项目级入口文档，例如 `README.md`、`CHANGELOG.md`、`ROADMAP.md` 和许可证。
