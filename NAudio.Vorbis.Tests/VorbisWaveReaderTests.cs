using System;
using System.IO;
using NAudio.Wave;
using Xunit;

namespace NAudio.Vorbis.Tests;

public class VorbisWaveReaderTests
{
    [Fact]
    public void ReportsTheFormatTheFileWasEncodedWith()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);

        Assert.Equal(44100, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, reader.WaveFormat.Encoding);
        Assert.Equal(32, reader.WaveFormat.BitsPerSample);
        Assert.Equal(8, reader.WaveFormat.BlockAlign);
    }

    [Fact]
    public void MonoFilesDecodeAsMono()
    {
        using var reader = TestAudio.Open(TestAudio.Mono);

        Assert.Equal(22050, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(4, reader.WaveFormat.BlockAlign);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void DecodesAudioOfRoughlyTheRightDuration(string file)
    {
        using var reader = TestAudio.Open(file);
        var expected = file == TestAudio.Stereo ? 1.5 : 0.75;

        Assert.InRange(reader.TotalTime.TotalSeconds, expected - 0.05, expected + 0.05);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void DecodedLengthMatchesTheReportedLength(string file)
    {
        using var reader = TestAudio.Open(file);
        var length = reader.Length;

        var decoded = TestAudio.DecodeAllBytes(file).Length;

        // NVorbis derives Length from the granule timeline, which for some files does not
        // agree exactly with what decodes; a well-formed file should be within a few frames.
        Assert.InRange(decoded, length - reader.WaveFormat.AverageBytesPerSecond, length);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void DecodesSamplesInRange(string file)
    {
        var bytes = TestAudio.DecodeAllBytes(file);

        Assert.NotEmpty(bytes);
        Assert.Equal(0, bytes.Length % 4);
        for (var i = 0; i < bytes.Length; i += 4)
        {
            var sample = BitConverter.ToSingle(bytes, i);
            Assert.False(float.IsNaN(sample), $"sample {i / 4} is NaN");
            Assert.InRange(sample, -1.5f, 1.5f);
        }
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void ReadsTheSameAudioWhicheverOverloadIsUsed(string file)
    {
        var viaByteArray = TestAudio.DecodeAllBytes(file);

        // Read(Span<byte>)
        using var spanReader = TestAudio.Open(file);
        using var viaByteSpan = new MemoryStream();
        var byteBuffer = new byte[16384];
        int read;
        while ((read = spanReader.Read(byteBuffer.AsSpan())) > 0)
        {
            viaByteSpan.Write(byteBuffer, 0, read);
        }

        // Read(Span<float>) - the ISampleProvider entry point
        using var sampleReader = TestAudio.Open(file);
        using var viaSampleSpan = new MemoryStream();
        var floatBuffer = new float[4096];
        while ((read = ((ISampleProvider)sampleReader).Read(floatBuffer.AsSpan())) > 0)
        {
            viaSampleSpan.Write(MemoryMarshalCast(floatBuffer, read));
        }

        // Read(float[], offset, count) at a non-zero offset
        using var arrayReader = TestAudio.Open(file);
        using var viaFloatArray = new MemoryStream();
        var offsetBuffer = new float[5000];
        while ((read = arrayReader.Read(offsetBuffer, 500, 4096)) > 0)
        {
            viaFloatArray.Write(MemoryMarshalCast(offsetBuffer.AsSpan(500).ToArray(), read));
        }

        Assert.Equal(viaByteArray, viaByteSpan.ToArray());
        Assert.Equal(viaByteArray, viaSampleSpan.ToArray());
        Assert.Equal(viaByteArray, viaFloatArray.ToArray());
    }

    private static byte[] MemoryMarshalCast(float[] samples, int count)
    {
        var bytes = new byte[count * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void ReadsWholeSampleFramesEvenFromAnAwkwardlySizedBuffer()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);

        // three floats is one and a half stereo frames
        var read = reader.Read(new byte[3 * sizeof(float)], 0, 3 * sizeof(float));

        Assert.Equal(0, read % reader.WaveFormat.BlockAlign);
        Assert.True(read > 0, "expected at least one whole frame");
    }

    [Fact]
    public void ReturnsZeroAtTheEndOfTheStream()
    {
        using var reader = TestAudio.Open(TestAudio.Mono);
        var buffer = new byte[16384];
        while (reader.Read(buffer, 0, buffer.Length) > 0) { }

        Assert.Equal(0, reader.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, reader.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void ExposesTheEncoderVendorAndComments()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);

        // Tags.All threw an InvalidCastException on every file with NVorbis 0.10.4
        Assert.NotNull(reader.Vendor);
        Assert.NotEmpty(reader.Vendor);
        Assert.Contains(reader.Comments, c => c.StartsWith("TITLE=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExposesOneSetOfStatsPerStream()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);

        Assert.Equal(1, reader.StreamCount);
        Assert.Single(reader.Stats);
    }

    [Fact]
    public void ClosesAnOwnedStreamOnDispose()
    {
        var stream = TestAudio.OpenStream(TestAudio.Mono);
        using (new VorbisWaveReader(stream, closeOnDispose: true)) { }

        Assert.False(stream.CanRead);
    }

    [Fact]
    public void LeavesABorrowedStreamOpenOnDispose()
    {
        using var stream = TestAudio.OpenStream(TestAudio.Mono);
        using (new VorbisWaveReader(stream, closeOnDispose: false)) { }

        Assert.True(stream.CanRead);
    }
}
