using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    /// <summary>
    /// Modern asynchronous contract for loading and querying a data-table context.
    /// </summary>
    public interface IDataTableContext
    {
        int Count { get; }

        /// <summary>
        /// Loads a table or joins the existing single-flight load for the same type and name.
        /// </summary>
        /// <remarks>
        /// The caller token cancels only this caller's wait. The shared read is canceled when the context
        /// lifecycle is reset by changing the source, clearing the cache, or disposing the context.
        /// </remarks>
        ValueTask<T?> LoadAsync<T>(string name = "", CancellationToken cancellationToken = default)
            where T : DataTableBase;

        T? GetCached<T>(string name = "") where T : DataTableBase;

        bool IsLoaded<T>(string name = "") where T : DataTableBase;
    }
}
