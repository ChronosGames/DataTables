using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTables.GeneratorCore
{
    public class GenerationContext
    {
        public string FileName { get; set; }

        public string SheetName { get; set; }

        public string[] UsingStrings { get; set; }

        public string Namespace { get; set; }

        public string PrefixClassName { get; set; }

        public string Title { get; set; }

        public string ClassName { get; set; }

        public string RealClassName => PrefixClassName + ClassName;

        public bool EnableTagsFilter { get; set; }

        public Property[] Properties { get; set; }

        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public object[,] Cells { get; set; }

        public string InputFilePath { get; set; }

        /// <summary>字段索引列表</summary>
        public List<string[]> Indexs { get; private set; } = new List<string[]>();

        /// <summary>
        /// 列分组索引列表
        /// </summary>
        public List<string[]> Groups { get; private set; } = new List<string[]>();

        /// <summary>
        /// 子表的名称
        /// </summary>
        public string Child = string.Empty;

        /// <summary>
        /// 全部子表的名称
        /// </summary>
        public string[] Children = Array.Empty<string>();

        public Property GetField(string field)
        {
            return Properties.FirstOrDefault(x => x.Name == field);
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

    public class Property
    {
        public string TypeName { get; set; }

        public string Name { get; set; }

        public string Comment { get; set; }
    }

    public abstract class KeyBase
    {
        public bool IsNonUnique { get; set; }
        public string StringComparisonOption { get; set; }
        public KeyProperty[] Properties { get; set; }
        public abstract string SelectorName { get; }
        public abstract string TableName { get; }
        public abstract bool IsPrimary { get; }

        public string BuildKeyAccessor(string lambdaArgument)
        {
            if (Properties.Length == 1)
            {
                return lambdaArgument + "." + Properties[0].Name;
            }
            else
            {
                return "(" + string.Join(", ", Properties.Select(x => lambdaArgument + "." + x.Name)) + ")";
            }
        }

        public string BuildTypeName()
        {
            if (Properties.Length == 1)
            {
                return Properties[0].TypeName;
            }
            else
            {
                return "(" + string.Join(", ", Properties.Select(x => x.TypeName + " " + x.Name)) + ")";
            }
        }

        public string BuildMethodName()
        {
            if (Properties.Length == 1)
            {
                return Properties[0].Name;
            }
            else
            {
                return string.Join("And", Properties.Select(x => x.Name));
            }
        }

        public string BuildPropertyTupleName()
        {
            if (Properties.Length == 1)
            {
                return Properties[0].Name;
            }
            else
            {
                return "(" + string.Join(", ", Properties.Select(x => x.Name)) + ")";
            }
        }

        public string BuildFindPrefix()
        {
            return IsNonUnique ? "FindMany" : "FindUnique";
        }

        public string BuildReturnTypeName(string elementName)
        {
            return IsNonUnique ? "RangeView<" + elementName + ">" : elementName;
        }

        public string BuildComparer()
        {
            if (!IsStringType)
            {
                return $"System.Collections.Generic.Comparer<{BuildTypeName()}>.Default";
            }
            else
            {
                if (StringComparisonOption != null)
                {
                    return "System.StringComparer." + StringComparisonOption.Split('.').Last();
                }
                else
                {
                    return "System.StringComparer.Ordinal";
                }
            }
        }

        public bool IsIntType
        {
            get
            {
                if (Properties.Length == 1)
                {
                    var typeName = Properties[0].TypeName;
                    if (typeName == "int" || typeName == "Int32" || typeName == "System.Int32")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public bool IsStringType
        {
            get
            {
                if (Properties.Length == 1)
                {
                    var typeName = Properties[0].TypeName;
                    if (typeName == "string" || typeName == "String" || typeName == "System.String")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public bool IsComparableNumberType
        {
            get
            {
                if (Properties.Length == 1)
                {
                    var typeName = Properties[0].TypeName;
                    if (typeName == "int" || typeName == "Int32" || typeName == "System.Int32"
                     || typeName == "long" || typeName == "Int64" || typeName == "System.Int64"
                     || typeName == "uint" || typeName == "UInt32" || typeName == "System.UInt32"
                     || typeName == "ulong" || typeName == "UInt64" || typeName == "System.UInt64"
                     || typeName == "byte" || typeName == "Byte" || typeName == "System.Byte"
                     || typeName == "sbyte" || typeName == "SByte" || typeName == "System.SByte"
                     )
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
    }

    public class KeyProperty
    {
        public int KeyOrder { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }

        public override string ToString()
        {
            return $"{TypeName} {Name} : {KeyOrder}";
        }
    }
}
