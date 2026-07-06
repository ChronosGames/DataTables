using System;

namespace DataTables.GeneratorCore;

internal static class SheetInfoParser
{
    public static bool Parse(string cellString, GenerationContext context)
    {
        var arr = cellString.Split(',');
        foreach (var pair in arr)
        {
            var args = pair.Split('=');
            if (args.Length == 2)
            {
                switch (args[0].Trim().ToLower())
                {
                    case "dtgen": context.DataSetType = args[1].Trim().ToLower(); break;
                    case "title": context.Title = args[1].Trim(); break;
                    case "class": context.ClassName = args[1].Trim(); break;
                    case "disabletagsfilter": context.DisableTagsFilter = bool.Parse(args[1].Trim()); break;
                    case "index": context.AddIndex(args[1].Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                    case "group": context.AddGroup(args[1].Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                    case "priority":
                        var raw = args[1].Trim();
                        context.Priority = raw.Equals("critical", StringComparison.OrdinalIgnoreCase) ? "Critical"
                            : raw.Equals("normal", StringComparison.OrdinalIgnoreCase) ? "Normal"
                            : raw.Equals("lazy", StringComparison.OrdinalIgnoreCase) ? "Lazy" : "Normal";
                        break;
                    case "child": context.Child = args[1].Trim(); break;
                    case "matrix":
                        var fields = args[1].Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        context.Fields =
                        [
                            new XField(0) { Name = DataMatrixTemplate.kKey1, TypeName = fields[0] },
                            new XField(1) { Name = DataMatrixTemplate.kKey2, TypeName = fields[1] },
                            new XField(2) { Name = DataMatrixTemplate.kValue, TypeName = fields[2] },
                        ];
                        break;
                    case "matrixdefaultvalue": context.MatrixDefaultValue = args[1].Trim(); break;
                }
            }
            else if (args.Length == 1 && args[0].Trim().Equals("disabletagsfilter", StringComparison.OrdinalIgnoreCase))
            {
                context.DisableTagsFilter = true;
            }
        }
        return !string.IsNullOrEmpty(context.ClassName);
    }
}
