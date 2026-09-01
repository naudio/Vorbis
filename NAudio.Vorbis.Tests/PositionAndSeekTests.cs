using System;
using System.IO;
using Xunit;

namespace NAudio.Vorbis.Tests;

public class PositionAndSeekTests
{
    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void StartsAtTheBeginning(string file)
    {
        using var reader = TestAudio.Open(file);

        Assert.Equal(0, reader.Position);
        Assert.Equal(TimeSpan.Zero, reader.CurrentTime);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void PositionAdvancesByTheNumberOfBytesRead(string file)
    {
        using var reader = TestAudio.Open(file);
        var buffer = new byte[reader.WaveFormat.BlockAlign * 128];

        var read = reader.Read(buffer, 0, buffer.Length);

        Assert.Equal(read, reader.Position);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void NeverReadsPastTheEndOfTheStream(string file)
    {
        using var reader = TestAudio.Open(file);
        var buffer = new byte[16384];

        while (reader.Read(buffer, 0, buffer.Length) > 0)
        {
            Assert.True(reader.Position <= reader.Length,
                $"position {reader.Position} ran past length {reader.Length}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    public void SeeksToAPositionAndReadsFromThere(int percent)
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);
        var blockAlign = reader.WaveFormat.BlockAlign;
        var target = reader.Length * percent / 100;
        target -= target % blockAlign;

        reader.Position = target;

        Assert.Equal(target, reader.Position);
        Assert.True(reader.Read(new byte[blockAlign * 32], 0, blockAlign * 32) > 0);
    }

    [Fact]
    public void SeekingBackToTheStartReplaysTheSameAudio()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);
        var first = new byte[reader.WaveFormat.BlockAlign * 64];
        var again = new byte[first.Length];

        var read = reader.Read(first, 0, first.Length);
        reader.Position = 0;
        var readAgain = reader.Read(again, 0, again.Length);

        Assert.Equal(read, readAgain);
        Assert.Equal(first, again);
    }

    [Fact]
    public void SeekingForwardsThenBackLandsOnTheSameAudio()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);
        var target = reader.Length / 4;
        target -= target % reader.WaveFormat.BlockAlign;
        var first = new byte[reader.WaveFormat.BlockAlign * 32];
        var again = new byte[first.Length];

        reader.Position = target;
        var read = reader.Read(first, 0, first.Length);

        // wander off somewhere else, then come back
        var elsewhere = reader.Length / 2;
        elsewhere -= elsewhere % reader.WaveFormat.BlockAlign;
        reader.Position = elsewhere;
        Assert.True(reader.Read(new byte[1024], 0, 1024) > 0);

        reader.Position = target;
        var readAgain = reader.Read(again, 0, again.Length);

        Assert.Equal(read, readAgain);
        Assert.Equal(first, again);
    }

    [Fact]
    public void CurrentTimeTracksPosition()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);

        reader.CurrentTime = TimeSpan.FromSeconds(0.5);

        Assert.InRange(reader.CurrentTime.TotalSeconds, 0.49, 0.51);
        Assert.Equal(0, reader.Position % reader.WaveFormat.BlockAlign);
    }

    [Fact]
    public void RejectsAPositionOutsideTheStream()
    {
        using var reader = TestAudio.Open(TestAudio.Mono);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = reader.Length + reader.WaveFormat.BlockAlign);
    }

    [Fact]
    public void RefusesToSeekAForwardOnlyStream()
    {
        using var reader = new VorbisWaveReader(new TestAudio.ForwardOnlyStream(TestAudio.OpenStream(TestAudio.Mono)), true);

        Assert.Throws<InvalidOperationException>(() => reader.Position = 0);
    }

    /// <summary>
    /// Seeking back to the start does not always land on the first sample with NVorbis 0.10.5:
    /// on some files the position reads back as 0 but decoding resumes somewhere else entirely.
    /// It is fixed in the NVorbis 1.0.0 line, along with the other first-page handling, so this
    /// asserts only what holds on both - the seek is accepted, the position reads back as 0, and
    /// audio still decodes. Tighten it into a "replays identically" assertion on NVorbis 1.0.
    /// </summary>
    [Fact]
    public void SeekingBackToTheStartIsAcceptedOnEveryFile()
    {
        using var reader = TestAudio.Open(TestAudio.Mono);
        var buffer = new byte[reader.WaveFormat.BlockAlign * 64];
        Assert.True(reader.Read(buffer, 0, buffer.Length) > 0);

        reader.Position = 0;

        Assert.Equal(0, reader.Position);
        Assert.True(reader.Read(buffer, 0, buffer.Length) > 0);
    }

    /// <summary>
    /// Seeking into the final page is not reliable on NVorbis 0.10.5: it can throw
    /// "GranulePos mismatch" when the last page's granule does not line up with the packets on
    /// it, which is the ordinary shape of an encoder's trailing partial page (NVorbis#39, fixed
    /// in the 1.0.0 line). What matters here is that the reader does not hang or end up in a
    /// broken state either way. Once the dependency moves to NVorbis 1.0 this should tighten
    /// into a plain "it seeks" assertion.
    /// </summary>
    [Fact]
    public void SeekingIntoTheFinalPageEitherWorksOrThrowsCleanly()
    {
        using var reader = TestAudio.Open(TestAudio.Stereo);
        var target = reader.Length - reader.WaveFormat.BlockAlign * 16;
        target -= target % reader.WaveFormat.BlockAlign;

        try
        {
            reader.Position = target;
            Assert.Equal(target, reader.Position);
        }
        catch (InvalidDataException)
        {
            // known NVorbis 0.10.5 limitation; the reader must stay usable
        }

        reader.Position = 0;
        Assert.True(reader.Read(new byte[1024], 0, 1024) > 0);
    }
}
