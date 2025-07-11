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

    public string DataSetType { get; set; } = "table";

    public string Title { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string DataTableClassFullName => string.IsNullOrEmpty(Namespace) ? DataTableClassPrefix + ClassName : Namespace + '.' + DataTableClassPrefix + ClassName;

    public string DataTableClassName => DataTableClassPrefix + ClassName;

    public string DataRowClassName => DataRowClassPrefix + ClassName;

    public bool DisableTagsFilter { get; set; }

    /// <summary>
    /// 实际列
    /// </summary>
    public XField[] Fields { get; set; } = [];

    /// <summary>按特定字段进行单体索引列表</summary>
    public readonly List<string[]> Indexs = [];

    /// <summary>按特定字段进行分组索引列表</summary>
    public readonly List<string[]> Groups = [];

    /// <summary>子表的名称</summary>
    public string Child = string.Empty;

    /// <summary>
    /// 默认值
    /// <para>Matrix模式专用</para>
    /// </summary>
    public string MatrixDefaultValue { get; set; } = string.Empty;

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

            sb.Append($"{fields[i]}={{{i}}}");
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
            result[i] = $"{DataTableProcessor.GetLanguageKeyword(GetField(fields[i])!)} {fields[i]}";
        }

        return string.Join(", ", result);
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
}

