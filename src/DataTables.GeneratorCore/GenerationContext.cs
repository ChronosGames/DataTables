using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore;

public class GenerationContext
{
    public string FileName { get; set; } = string.Empty;

    public string SheetName { get; set; } = string.Empty;

    public string[] UsingStrings { get; set; } = Array.Empty<string>();

    public string Namespace { get; set; } = string.Empty;

    public string PrefixClassName { get; set; } = string.Empty;

    public string DataSetType { get; set; } = "table";

    public string Title { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string RealClassName => PrefixClassName + ClassName;

    public bool EnableTagsFilter { get; set; }

    public XField[] Fields { get; set; } = Array.Empty<XField>();

    /// <summary>按特定字段进行单体索引列表</summary>
    public readonly List<string[]> Indexs = new List<string[]>();

    /// <summary>按特定字段进行分组索引列表</summary>
    public readonly List<string[]> Groups = new List<string[]>();

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
        return this.RealClassName + (string.IsNullOrEmpty(this.Child) ? string.Empty : '.' + this.Child) + ".bytes";
    }

    /// <summary>
    /// 拼接索引列表的日志输出格式串
    /// </summary>
    /// <param name="fields"></param>
    /// <returns></returns>
    public string BuildIndexsLogFormat(string[] fields)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.AppendFormat("{0}={{{1}}}", fields[i], i);
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
            result[i] = $"{DataTableProcessor.GetLanguageKeyword(GetField(fields[i]))} {fields[i]}";
        }

        return string.Join(", ", result);
    }

    public string BuildIndexDictDefine(string[] fields)
    {
        StringBuilder sb = new StringBuilder();

        if (fields.Length > 1)
        {
            sb.Append("MultiDictionary<");
        }
        else
        {
            sb.Append("Dictionary<");
        }

        foreach (var fieldName in fields)
        {
            sb.Append(DataTableProcessor.GetLanguageKeyword(GetField(fieldName)));
            sb.Append(", ");
        }

        sb.Append(RealClassName);
        sb.Append('>');

        return sb.ToString();
    }

    public string BuildGroupDictDefine(string[] fields)
    {
        StringBuilder sb = new StringBuilder();

        if (fields.Length > 1)
        {
            sb.Append("MultiDictionary<");
        }
        else
        {
            sb.Append("Dictionary<");
        }

        foreach (var fieldName in fields)
        {
            sb.Append(DataTableProcessor.GetLanguageKeyword(GetField(fieldName)));
            sb.Append(", ");
        }

        sb.Append("List<");
        sb.Append(RealClassName);
        sb.Append(">>");

        return sb.ToString();
    }
}

public class XField
{
    /// <summary>
    /// 列序号
    /// </summary>
    public int Index { get; private set; }

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
    public bool IsIgnore { get; set; } = false;

    public XField(int index)
    {
        Index = index;
    }
}

