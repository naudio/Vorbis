using System;
using System.IO;
using Xunit;

namespace NAudio.Vorbis.Tests;

/// <summary>
/// A forward-only source (a network or pipe stream) cannot report a total length, so the
/// reader has to keep working without one.
/// </summary>
public class ForwardOnlyStreamTests
{
    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void DecodesTheSameAudioAsASeekableSource(string file)
    {
        var fromSeekable = TestAudio.DecodeAllBytes(file);

        using var reader = new VorbisWaveReader(new TestAudio.ForwardOnlyStream(TestAudio.OpenStream(file)), true);
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }

        Assert.Equal(fromSeekable, output.ToArray());
    }

    [Fact]
    public void ReportsTheFormatWithoutSeeking()
    {
        using var reader = new VorbisWaveReader(new TestAudio.ForwardOnlyStream(TestAudio.OpenStream(TestAudio.Stereo)), true);

        Assert.Equal(44100, reader.WaveFormat.SampleRate);
        Assert.Equal(2, reader.WaveFormat.Channels);
    }
}
