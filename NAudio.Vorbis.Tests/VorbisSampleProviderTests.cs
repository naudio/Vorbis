using System;
using System.Linq;
using NAudio.Wave;
using Xunit;

namespace NAudio.Vorbis.Tests;

public class VorbisSampleProviderTests
{
    [Fact]
    public void IsAnIeeeFloatSampleProvider()
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Stereo), true);

        Assert.IsAssignableFrom<ISampleProvider>(provider);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, provider.WaveFormat.Encoding);
        Assert.Equal(2, provider.WaveFormat.Channels);
    }

    [Fact]
    public void ReportsLengthInSamplesNotBytes()
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Stereo), true);
        using var reader = TestAudio.Open(TestAudio.Stereo);

        Assert.Equal(reader.Length, provider.Length * provider.WaveFormat.BlockAlign);
    }

    /// <summary>
    /// NVorbis requires a whole number of sample frames, but an ISampleProvider consumer is free
    /// to hand over a span of any length, so the provider has to trim rather than throw.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(101)]
    [InlineData(1023)]
    public void AcceptsABufferThatIsNotAWholeNumberOfFrames(int samples)
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Stereo), true);

        var read = provider.Read(new float[samples].AsSpan());

        Assert.Equal(0, read % provider.WaveFormat.Channels);
        Assert.True(read <= samples);
    }

    [Fact]
    public void ReadsToTheEndInSmallChunks()
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Mono), true);
        var buffer = new float[64];
        long total = 0;
        int read;

        while ((read = provider.Read(buffer.AsSpan())) > 0) total += read;

        Assert.True(total > 0);
        Assert.Equal(0, provider.Read(buffer.AsSpan()));
    }

    [Fact]
    public void SeekReturnsThePositionItReached()
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Stereo), true);

        var reached = provider.Seek(provider.Length / 2);

        Assert.Equal(reached, provider.SamplePosition);
    }

    [Fact]
    public void ExposesTagsAndStats()
    {
        using var provider = new VorbisSampleProvider(TestAudio.OpenStream(TestAudio.Stereo), true);

        Assert.NotNull(provider.Stats);
        Assert.NotEmpty(provider.Tags.EncoderVendor);
        Assert.Contains(provider.Tags.All, t => t.Key.Equals("TITLE", StringComparison.OrdinalIgnoreCase));
    }
}
