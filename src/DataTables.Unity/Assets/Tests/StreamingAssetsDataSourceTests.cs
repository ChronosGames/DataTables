using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace DataTables.Tests
{
    public sealed class StreamingAssetsDataSourceTests
    {
        [Test]
        public void ResourceLocation_EscapesSegmentsAndRejectsTraversal()
        {
            var source = new StreamingAssetsDataSource("jar:file:///app/base!/assets");
            var method = typeof(StreamingAssetsDataSource).GetMethod("GetResourceLocation", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(source, new object[] { "folder name/配置.bytes" }),
                Is.EqualTo("jar:file:///app/base!/assets/folder%20name/%E9%85%8D%E7%BD%AE.bytes"));
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(source, new object[] { "../escape.bytes" }));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public async Task PackagedManifest_CompletesThroughStreamingAssetsTransport()
        {
            var source = new StreamingAssetsDataSource(AppendPath(Application.streamingAssetsPath, "DataTablesTests"));

            var manifest = await source.GetManifestAsync(CancellationToken.None);

            Assert.That(manifest.Version, Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
            Assert.That(manifest.Entries, Is.Empty);
        }

        [Test]
        public async Task MissingResource_MapsToStructuredDataSourceException()
        {
            var source = new StreamingAssetsDataSource(AppendPath(Application.streamingAssetsPath, "DataTablesTests"));

            var exception = await ThrowsAsync<DataSourceException>(() => source.OpenReadAsync("Missing", CancellationToken.None).AsTask());

            Assert.That(exception.SourceType, Is.EqualTo(DataSourceType.StreamingAssets));
            Assert.That(exception.Operation, Is.EqualTo(DataSourceOperation.OpenRead));
            Assert.That(exception.LogicalName, Is.EqualTo("Missing"));
            Assert.That(exception.Location, Does.EndWith("/Missing.bytes"));
        }

#if UNITY_ANDROID || UNITY_WEBGL
        [Test]
        public async Task InFlightRequestCancellation_AbortsAndRemainsOperationCanceledException()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var source = new StreamingAssetsDataSource("https://example.invalid/datatables-tests");
            Task pending = source.OpenReadAsync("Never", cancellation.Token).AsTask();
            cancellation.Cancel();

            await ThrowsAsync<OperationCanceledException>(() => pending);
        }
#endif

        private static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail($"Expected {typeof(TException).Name} but caught {exception.GetType().Name}.");
            }

            Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
            throw new AssertionException("Unreachable");
        }

        private static string AppendPath(string root, string segment)
            => root.Replace('\\', '/').TrimEnd('/') + "/" + Uri.EscapeDataString(segment);
    }
}
