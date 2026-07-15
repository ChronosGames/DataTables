using System;
using System.IO;

namespace DataTables
{
    public enum DataSourceOperation
    {
        OpenRead,
        Exists,
        GetManifest,
        IsAvailable
    }

    public sealed class DataSourceException : IOException
    {
        public DataSourceException(
            DataSourceType sourceType,
            DataSourceOperation operation,
            string name,
            string location,
            string platform,
            long? httpStatusCode = null,
            string? transportError = null,
            Exception? innerException = null)
            : base(BuildMessage(sourceType, operation, name, location, platform, httpStatusCode, transportError), innerException)
        {
            SourceType = sourceType;
            Operation = operation;
            LogicalName = name ?? string.Empty;
            Location = location ?? string.Empty;
            Platform = platform ?? string.Empty;
            HttpStatusCode = httpStatusCode;
            TransportError = transportError;
        }

        public DataSourceType SourceType { get; }
        public DataSourceOperation Operation { get; }
        public string LogicalName { get; }
        public string Name => LogicalName;
        public string Location { get; }
        public string Platform { get; }
        public long? HttpStatusCode { get; }
        public string? TransportError { get; }

        private static string BuildMessage(DataSourceType sourceType, DataSourceOperation operation, string name, string location, string platform, long? statusCode, string? transportError)
        {
            var status = statusCode.HasValue ? $", status={statusCode.Value}" : string.Empty;
            var error = string.IsNullOrWhiteSpace(transportError) ? string.Empty : $", error={transportError}";
            return $"Data source {operation} failed: source={sourceType}, name='{name}', location='{location}', platform='{platform}'{status}{error}.";
        }
    }
}
