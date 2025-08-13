# Excel 模板与 Dicts 指南

本目录提供可导入到 Excel 的 VBA 模块，以及示例枚举列表，帮助你快速制作 `.xltm` 带宏模板和 `Dicts` 工作表。

## 文件说明

- `TableTemplate.bas`：校验函数与一键应用数据验证的宏。
- `WorksheetEvents.bas`：第3/4行变更时即时高亮的事件代码（需放入工作表代码窗口）。
- `Dicts.Sample.csv`：示例枚举取值源，可粘贴进 `Dicts` 工作表后建立命名区域。

## 制作表格型模板（.xltm）

1. 新建 Excel，Sheet1 重命名为 `Table`。
2. 第1-4行放置表头：
   - A1：`DTGen=Table, Title=示例表, Class=DTExample, Index=Id`
   - 第2行：列描述，例如 `唯一Id`, `名称@RELEASE`, `类型`...
   - 第3行：字段名，例如 `Id`, `Name`, `Type`...
   - 第4行：字段类型，例如 `int`, `string`, `Enum<SceneType>`...
3. Alt+F11 打开 VBA 编辑器：
   - 导入 `TableTemplate.bas`（菜单：文件 → 导入文件）。
   - 在 `Table` 工作表代码窗口中粘贴 `WorksheetEvents.bas` 的内容。
4. 添加 `Dicts` 工作表：
   - 将 `Dicts.Sample.csv` 的内容粘贴为两列（EnumName, Value）。
   - 通过“公式 → 定义名称”按枚举建立命名区域：如选择 `SceneType` 的值列，命名为 `Enum_SceneType`。
5. 回到 `Table`，在第4行填好各列类型后，运行宏 `ApplyColumnValidation`，为第5行及以下应用数据验证。
6. 冻结窗口到第4行，保护第1-4行（允许用户编辑第5行以下）。
7. 另存为 `.xltm` 模板文件，分发给内容同学使用。

## 使用建议

- Enum 下拉依赖命名区域 `Enum_<类型名>`；修改枚举后需要更新命名区域范围。
- 复杂类型（Array/Map/JSON/Custom）在 Excel 端只做弱校验，最终以生成器校验为准。
- 建议提交前本地执行 `dotnet dtgen` 进行二次校验；在 CI 中同样执行以拦截不合规改动。

