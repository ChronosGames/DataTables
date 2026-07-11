using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore;

public class GenerationContext
{
    public string FileName { get; set; } = string.Empty;

    public string SheetName { get; set; } = string.Empty;

    public string[] UsingStrings { get; set; } = [];

    public string Namespace { get; set; } = string.Empty;

    public string DataTableClassPrefix { get; set; } = "DT";

    public string DataRowClassPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 表结构类型：table / matrix / column。
    /// 默认为空字符串；仅在 A1 单元格中声明了 DTGen= 时才会被赋值。
    /// CreateGenerationContext 解析完 A1 后会检查此值是否为空，若为空则认为该 Sheet
    /// 不是 DataTables 格式并跳过后续解析。
    /// </summary>
    public string DataSetType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string DataTableClassFullName => string.IsNullOrEmpty(Namespace) ? DataTableClassPrefix + ClassName : Namespace + '.' + DataTableClassPrefix + ClassName;

    public string DataTableClassName => DataTableClassPrefix + ClassName;

    public string DataRowClassName => DataRowClassPrefix + ClassName;

    public bool DisableTagsFilter { get; set; }

    /// <summary>
    /// 表预热优先级：Critical/Normal/Lazy（默认Normal）
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// 实际列
    /// </summary>
    public XField[] Fields { get; set; } = [];

    /// <summary>按特定字段进行唯一索引列表（兼容旧模板/调用点）。</summary>
    public readonly List<string[]> Indexs = [];

    /// <summary>按特定字段进行分组索引列表（兼容旧模板/调用点）。</summary>
    public readonly List<string[]> Groups = [];

    /// <summary>明确的唯一索引元数据。</summary>
    public readonly List<IndexDefinition> IndexDefinitions = [];

    /// <summary>明确的分组索引元数据。</summary>
    public readonly List<GroupIndexDefinition> GroupIndexDefinitions = [];

    /// <summary>唯一约束元数据；当前由 index= 声明隐式产生。</summary>
    public readonly List<UniqueConstraint> UniqueConstraints = [];

    /// <summary>子表的名称</summary>
    public string Child = string.Empty;

    /// <summary>
    /// 默认值
    /// <para>Matrix模式专用</para>
    /// </summary>
    public string MatrixDefaultValue { get; set; } = string.Empty;

		/// <summary>
		/// 数据起始列（Column 模式专用），通常为 D 列（索引 3）
		/// </summary>
		public int ColumnFirstDataColIndex { get; set; } = -1;

		/// <summary>
		/// 列注释标记所在的行索引（Column 模式专用），用于按列跳过导出
		/// </summary>
		public int ColumnCommentRowIndex { get; set; } = -1;

    /// <summary>
    /// 记录列编号与名称的对照关系
    /// <para>Matrix模式专用</para>
    /// </summary>
    public readonly Dictionary<int, string> ColumnIndexToKey2 = new Dictionary<int, string>();

    /// <summary>
    /// 导出时是否出错
    /// </summary>
    public bool Failed;

    /// <summary>
    /// 导出时是否跳过
    /// </summary>
    public bool Skiped;


    public void AddIndex(string[] fields)
    {
        var normalized = NormalizeFieldList(fields);
        Indexs.Add(normalized);
        IndexDefinitions.Add(new IndexDefinition(normalized));
        UniqueConstraints.Add(new UniqueConstraint(normalized));
    }

    public void AddGroup(string[] fields)
    {
        var normalized = NormalizeFieldList(fields);
        Groups.Add(normalized);
        GroupIndexDefinitions.Add(new GroupIndexDefinition(normalized));
    }

    public void RefreshIndexDefinitionsFromLegacyLists()
    {
        IndexDefinitions.Clear();
        UniqueConstraints.Clear();
        foreach (var fields in Indexs)
        {
            var normalized = NormalizeFieldList(fields);
            IndexDefinitions.Add(new IndexDefinition(normalized));
            UniqueConstraints.Add(new UniqueConstraint(normalized));
        }

        GroupIndexDefinitions.Clear();
        foreach (var fields in Groups)
        {
            GroupIndexDefinitions.Add(new GroupIndexDefinition(NormalizeFieldList(fields)));
        }
    }

    private static string[] NormalizeFieldList(string[] fields)
    {
        return fields.Select(x => (x ?? string.Empty).Trim()).Where(x => x.Length > 0).ToArray();
    }

    public XField? GetField(string field)
    {
        return Fields.FirstOrDefault(x => !x.IsIgnore && x.Name == field);
    }

    /// <summary>
    /// 获取输出的二进制文件相对路径
    /// </summary>
    /// <returns></returns>
    public string GetDataOutputFilePath()
    {
        return this.DataTableClassFullName + (string.IsNullOrEmpty(this.Child) ? string.Empty : '.' + this.Child) + ".bytes";
    }

    /// <summary>
    /// 拼接索引列表的日志输出格式串
    /// </summary>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static string BuildIndexsLogFormat(string[] fields)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append($"{ToCamelCase(fields[i])}={{{i}}}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 拼接索引列表的函数形参定义
    /// </summary>
    /// <returns></returns>
    public string BuildMethodParameters(string[] fields)
    {
        string[] result = new string[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            string paramName = ToCamelCase(fields[i]);
            result[i] = $"{DataTableProcessor.GetLanguageKeyword(GetField(fields[i])!)} {paramName}";
        }

        return string.Join(", ", result);
    }

    /// <summary>
    /// 将字符串转换为小驼峰命名
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        if (str.Length == 1)
            return str.ToLowerInvariant();

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    /// <summary>
    /// 获取小驼峰形式的参数名列表
    /// </summary>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static string BuildCamelCaseParameters(string[] fields)
    {
        return string.Join(", ", fields.Select(ToCamelCase));
    }

    public string BuildIndexDictDefine(string[] fields)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Dictionary<");
        if (fields.Length > 1)
        {
            sb.Append("ValueTuple<");
        }

        sb.AppendJoin(", ", fields.Select(x => DataTableProcessor.GetLanguageKeyword(GetField(x)!)));

        if (fields.Length > 1)
        {
            sb.Append('>');
        }

        sb.Append(", ");
        sb.Append(DataRowClassName);
        sb.Append('>');

        return sb.ToString();
    }

    public string BuildReadOnlyIndexDictDefine(string[] fields) => BuildIndexDictDefine(fields).Replace("Dictionary<", "IReadOnlyDictionary<");

    public string BuildReadOnlyGroupDictDefine(string[] fields) => BuildGroupDictDefine(fields).Replace("Dictionary<", "IReadOnlyDictionary<");

    public string BuildGroupDictDefine(string[] fields)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("Dictionary<");
        if (fields.Length > 1)
        {
            sb.Append("ValueTuple<");
        }

        sb.AppendJoin(", ", fields.Select(x => DataTableProcessor.GetLanguageKeyword(GetField(x)!)));

        if (fields.Length > 1)
        {
            sb.Append('>');
        }

        sb.Append(", List<");
        sb.Append(DataRowClassName);
        sb.Append(">>");

        return sb.ToString();
    }
}

public abstract record IndexDefinitionBase(string[] Fields)
{
    public string NameSuffix => string.Join("And", Fields);
}

public sealed record IndexDefinition(string[] Fields) : IndexDefinitionBase(Fields);

public sealed record GroupIndexDefinition(string[] Fields) : IndexDefinitionBase(Fields);

public sealed record UniqueConstraint(string[] Fields);

public class XField(int index)
{
    /// <summary>
    /// 列序号
    /// </summary>
    public int Index { get; private set; } = index;

    /// <summary>
    /// 中文名称行上的单元格的文本内容
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型所在单元格（例如 C3），用于诊断定位。
    /// </summary>
    public string TypeCell { get; set; } = string.Empty;

    /// <summary>
    /// 字段名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 中文名称行上单元格的批注信息
    /// </summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// 是否忽略
    /// </summary>
    public bool IsIgnore { get; set; }

    /// <summary>
    /// 是否注释
    /// </summary>
    public bool IsComment { get; set; }

    /// <summary>
    /// 是否因标签过滤而被忽略
    /// </summary>
    public bool IsTagFiltered { get; set; }
}
