using System.IO.Compression;

namespace NothicWorlds.Core.Storage;

/// <summary>An asset stored inside a saved world file (a zip entry).</summary>
/// <param name="PackagePath">Full path of the <c>.nworld</c> file.</param>
/// <param name="EntryName">The asset's name inside it, e.g. <c>assets/1a2b….png</c>.</param>
public sealed record PackageAssetSource(string PackagePath, string EntryName) : IAssetSource
{
    /// <inheritdoc/>
    public Stream OpenRead()
    {
        ZipArchive archive = ZipFile.OpenRead(PackagePath);
        try
        {
            ZipArchiveEntry entry = archive.GetEntry(EntryName)
                ?? throw new WorldFileException(
                    $"The world file is missing its asset '{EntryName}'.");
            return new ArchiveOwningStream(entry.Open(), archive);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    // Keeps the zip archive open while its entry is being read, and closes both together.
    private sealed class ArchiveOwningStream(Stream inner, ZipArchive archive) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                archive.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
