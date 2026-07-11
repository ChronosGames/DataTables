using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DataTables
{
    internal sealed class OwnedReadStream : Stream
    {
        private readonly Stream m_Inner;
        private readonly IDisposable m_Owner;

        public OwnedReadStream(Stream inner, IDisposable owner)
        {
            m_Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            m_Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public override bool CanRead => m_Inner.CanRead;
        public override bool CanSeek => m_Inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => m_Inner.Length;
        public override long Position { get => m_Inner.Position; set => m_Inner.Position = value; }
        public override void Flush() => m_Inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => m_Inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => m_Inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => m_Inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { m_Inner.Dispose(); }
                finally { m_Owner.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
