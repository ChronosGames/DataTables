using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DataTables.GeneratorCore;

internal static class DataTableSchemaHash
{
    public static ulong Compute(GenerationContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var builder = new StringBuilder();
        builder.Append("table=").Append(context.DataSetType).Append('\n');
        builder.Append("fullName=").Append(context.DataTableClassFullName).Append('\n');

        var ordinal = 0;
        foreach (var field in context.Fields.Where(x => !x.IsIgnore).OrderBy(x => x.Index))
        {
            builder.Append(ordinal++)
                .Append('|')
                .Append(field.Name)
                .Append('|')
                .Append(field.TypeName)
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return BitConverter.ToUInt64(hash, 0);
    }
}
