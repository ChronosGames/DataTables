# DataTables.Generator
数据表生成工具 - 适用于C#和Unity。

可用于将游戏数据表（Excel格式）转化为代码文件与数据文件，然后，在游戏内直接读取二进制数据文件，自动解析为对应类，后续可直接使用。

## 数据布局类型

- RowTable: `dtgen=table` 行为记录，列为字段（默认）
- MatrixTable: `dtgen=matrix` 二维键值矩阵
- ColumnTable: `dtgen=column` 列为记录，行为字段
- Kv/Graph/Tree: `dtgen=kv|graph|tree`

## 表级标签

A1 可声明 `tags=`，例如 `dtgen=table,class=Item,tags=S&C`。命令行 `-t` 是布尔表达式，例如 `C && !S`；未声明标签的表始终导出，每个 Sheet/child 独立判断。`disabletagsfilter` 会同时关闭表级标签和字段 `@tag` 过滤。非法声明定位到 A1，非法全局表达式在导出和 `validate` 中都会产生 Error diagnostic。

### ColumnTable 规范

- 第1行：信息行，如 `dtgen=column, class=ItemConfig, title=Item Table`
- 第2行开始：每一行一个字段，A=注释(可含@标签)、B=字段名、C=类型；D..列为每条记录该字段的值
- 可选：`#列注释标志` 行，用于按列跳过导出（该行对应列以 `#` 开头即跳过该列）

示例参见 `templates/ColumnTable.Sample.csv`。


## Validate-only / CI 校验

使用 `validate` 子命令可以解析 Excel、执行 schema/诊断校验并渲染代码模板，但不写入 C#、`.bytes` 或 manifest，适合 CI 或预提交检查：

```bash
dotnet dtgen validate -i ./Tables -patterns "*.xlsx" -n MyGame -p DR --diagnostics-json-output ./artifacts/diagnostics.json
```

普通导出与 `data` 子命令会在数据目录事务性写出运行时 `manifest.json`。部署时把它和 `.bytes` 一起复制；生成失败或输出提交冲突时保留上一版。生成器自己的 `.dtgen-manifest.json` / `.dtgen-data-manifest.json` 使用 v2 稳定相对标识，不应作为运行时资源。
