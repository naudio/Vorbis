using System;
using NAudio.Wave;

namespace NAudio.Vorbis.TestApp;

/// <summary>
/// Wraps a <see cref="VorbisWaveReader"/> so the UI thread can reposition it while the
/// playback thread is reading from it.
/// </summary>
/// <remarks>
/// Without this, repositioning during playback is a race between two threads inside the
/// decoder, and the resulting corruption looks like a decoder bug. Everything that touches
/// the reader goes through the same lock.
/// </remarks>
internal sealed class PlaybackSource : IWaveProvider, IDisposable
{
    private readonly VorbisWaveReader reader;
    private readonly object sync = new();

    public PlaybackSource(VorbisWaveReader reader) => this.reader = reader;

    public WaveFormat WaveFormat => reader.WaveFormat;

    public long Length => reader.Length;

    public long Position
    {
        get { lock (sync) return reader.Position; }
    }

    public TimeSpan TotalTime => reader.TotalTime;

    public TimeSpan CurrentTime
    {
        get { lock (sync) return reader.CurrentTime; }
    }

    public int Read(Span<byte> buffer)
    {
        lock (sync) return reader.Read(buffer);
    }

    /// <summary>
    /// Repositions the reader, rounding down to a sample frame boundary.
    /// </summary>
    public void Seek(long bytePosition)
    {
        lock (sync)
        {
            bytePosition -= bytePosition % reader.WaveFormat.BlockAlign;
            reader.Position = Math.Clamp(bytePosition, 0, reader.Length);
        }
    }

    public string DescribeStream()
    {
        var format = reader.WaveFormat;
        var lines = new System.Collections.Generic.List<string>
        {
            $"Format:      {format.SampleRate} Hz, {format.Channels} channel(s), {format.BitsPerSample} bit {format.Encoding}",
            $"Duration:    {reader.TotalTime:mm\\:ss\\.fff}  ({reader.Length:N0} bytes)",
            $"Block align: {format.BlockAlign} bytes",
            $"Streams:     {reader.StreamCount}",
            $"Vendor:      {reader.Vendor}",
            $"Bitrate:     nominal {reader.NominalBitrate:N0}, lower {reader.LowerBitrate:N0}, upper {reader.UpperBitrate:N0}",
        };

        var comments = reader.Comments;
        lines.Add(comments.Length == 0 ? "Comments:    (none)" : "Comments:    " + string.Join(Environment.NewLine + "             ", comments));
        return string.Join(Environment.NewLine, lines);
    }

    public void Dispose() => reader.Dispose();
}
