using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    internal sealed class HashValidatingReadStream : Stream
    {
        private readonly Stream m_Inner;
        private readonly HashAlgorithm m_Hash = SHA256.Create();
        private readonly string m_Expected;
        private readonly string m_Name;
        private readonly string m_Source;
        private bool m_Validated;
        private InvalidDataException? m_ValidationError;

        public HashValidatingReadStream(Stream inner, string expected, string name, string source)
        {
            m_Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            m_Expected = expected;
            m_Name = name;
            m_Source = source;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = m_Inner.Read(buffer, offset, count);
            if (count != 0) Observe(buffer, offset, read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await m_Inner.ReadAsync(buffer, offset, count, cancellationToken);
            if (count != 0) Observe(buffer, offset, read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_Hash.Dispose();
                m_Inner.Dispose();
            }
            base.Dispose(disposing);
        }

        private void Observe(byte[] buffer, int offset, int count)
        {
            if (count > 0)
            {
                m_Hash.TransformBlock(buffer, offset, count, buffer, offset);
                return;
            }
            if (m_Validated)
            {
                if (m_ValidationError != null) throw m_ValidationError;
                return;
            }

            m_Hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var builder = new StringBuilder(m_Hash.Hash!.Length * 2);
            foreach (var value in m_Hash.Hash) builder.Append(value.ToString("x2"));
            var actual = builder.ToString();
            m_Validated = true;
            if (!string.Equals(actual, m_Expected, StringComparison.Ordinal))
            {
                m_ValidationError = new InvalidDataException($"Hash validation failed for '{m_Name}' from '{m_Source}': expected {m_Expected}, actual {actual} ({HashValidatedDataSource.Algorithm} {HashValidatedDataSource.HashFormat}).");
                throw m_ValidationError;
            }
        }
    }
}
