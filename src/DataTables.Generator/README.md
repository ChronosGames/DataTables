# DataTables.Generator
数据表生成工具 - 适用于C#和Unity。

可用于将游戏数据表（Excel格式）转化为代码文件与数据文件，然后，在游戏内直接读取二进制数据文件，自动解析为对应类，后续可直接使用。

## 数据布局类型

- RowTable: `dtgen=table` 行为记录，列为字段（默认）
- MatrixTable: `dtgen=matrix` 二维键值矩阵
- ColumnTable: `dtgen=column` 列为记录，行为字段

### ColumnTable 规范

- 第1行：信息行，如 `dtgen=column, class=ItemConfig, title=Item Table`
- 第2行开始：每一行一个字段，A=注释(可含@标签)、B=字段名、C=类型；D..列为每条记录该字段的值
- 可选：`#列注释标志` 行，用于按列跳过导出（该行对应列以 `#` 开头即跳过该列）

示例参见 `templates/ColumnTable.Sample.csv`。
