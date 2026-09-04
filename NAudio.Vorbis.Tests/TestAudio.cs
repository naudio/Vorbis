using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Vorbis;

namespace NAudio.Vorbis.Tests;

/// <summary>
/// Shared helpers for locating the test files and decoding them.
/// </summary>
public static class TestAudio
{
    public const string Stereo = "sine-stereo.ogg";
    public const string Mono = "sine-mono.ogg";

    /// <summary>Both test files, for [Theory] cases that should hold for either.</summary>
    public static IEnumerable<object[]> AllFiles => new[] { new object[] { Stereo }, new object[] { Mono } };

    public static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    public static VorbisWaveReader Open(string name) => new(Path(name));

    public static Stream OpenStream(string name) => File.OpenRead(Path(name));

    /// <summary>Decodes the whole file through <see cref="VorbisWaveReader.Read(byte[], int, int)"/>.</summary>
    public static byte[] DecodeAllBytes(string name, int bufferSize = 16384)
    {
        using var reader = Open(name);
        using var output = new MemoryStream();
        var buffer = new byte[bufferSize];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    /// <summary>A read-only, non-seekable view of a stream, standing in for network or pipe delivery.</summary>
    public sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream inner;

        public ForwardOnlyStream(Stream inner) => this.inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
