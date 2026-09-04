using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace NAudio.Vorbis.Tests;

/// <summary>
/// Regression tests for issue #16: reading with a buffer bigger than the samples left could
/// leave NVorbis's decode state with a negative valid length, after which its Read loop spun
/// forever without consuming input or producing output. The reader avoids the over-read, so
/// these must all complete. Each is bounded by a watchdog: a regression fails the test rather
/// than hanging the test run.
/// </summary>
public class OverReadRegressionTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    private static T RunBounded<T>(Func<T> work, string what)
    {
        var task = Task.Run(work);
        if (!task.Wait(Watchdog))
        {
            throw new Xunit.Sdk.XunitException($"{what} did not complete within {Watchdog.TotalSeconds}s - the decoder is stuck");
        }
        return task.Result;
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void CopyToCompletes(string file)
    {
        // Stream.CopyTo sizes its buffer from Length, so its final read always asks for more
        // than remains - the exact shape that hung in the original report.
        var copied = RunBounded(() =>
        {
            using var reader = TestAudio.Open(file);
            using var destination = new MemoryStream();
            reader.CopyTo(destination);
            return destination.Length;
        }, nameof(CopyToCompletes));

        Assert.True(copied > 0);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void ReadingWithABufferLargerThanTheWholeStreamCompletes(string file)
    {
        var read = RunBounded(() =>
        {
            using var reader = TestAudio.Open(file);
            var oversized = new byte[reader.Length * 2];
            var total = 0;
            int n;
            while ((n = reader.Read(oversized, 0, oversized.Length)) > 0) total += n;
            return total;
        }, nameof(ReadingWithABufferLargerThanTheWholeStreamCompletes));

        Assert.True(read > 0);
    }

    [Theory]
    [MemberData(nameof(TestAudio.AllFiles), MemberType = typeof(TestAudio))]
    public void TheFinalReadReturnsExactlyWhatIsLeft(string file)
    {
        RunBounded<object?>(() =>
        {
            using var reader = TestAudio.Open(file);
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                Assert.True(total <= reader.Length, $"read {total} bytes from a {reader.Length} byte stream");
            }
            return null;
        }, nameof(TheFinalReadReturnsExactlyWhatIsLeft));
    }

    [Fact]
    public void ReadingOnAfterSeekingNearTheEndCompletes()
    {
        RunBounded<object?>(() =>
        {
            using var reader = TestAudio.Open(TestAudio.Stereo);
            var buffer = new byte[262144];
            for (var i = 1; i <= 4; i++)
            {
                var target = reader.Length - reader.WaveFormat.BlockAlign * 64 * i;
                target -= target % reader.WaveFormat.BlockAlign;
                if (target < 0) break;

                try { reader.Position = target; }
                catch (InvalidDataException) { continue; }   // NVorbis 0.10.5, see PositionAndSeekTests
                while (reader.Read(buffer, 0, buffer.Length) > 0) { }
            }
            return null;
        }, nameof(ReadingOnAfterSeekingNearTheEndCompletes));
    }

    /// <summary>
    /// short-granule.ogg has a final granule position that understates its contents, so a read
    /// that reaches the end asks for more than the stream says is left - the exact condition
    /// behind issue #16. Without the guard in VorbisSampleProvider.Read this call never returns.
    /// </summary>
    [Fact]
    public void DecodesAFileWhoseStatedLengthUnderstatesItsContents()
    {
        var (decoded, length) = RunBounded(() =>
        {
            using var reader = TestAudio.Open("short-granule.ogg");
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) total += read;
            return (total, reader.Length);
        }, nameof(DecodesAFileWhoseStatedLengthUnderstatesItsContents));

        Assert.True(decoded > 0);
        Assert.Equal(length, decoded);
    }

    /// <summary>The same file through Stream.CopyTo, which is how the issue was reported.</summary>
    [Fact]
    public void CopiesAFileWhoseStatedLengthUnderstatesItsContents()
    {
        var copied = RunBounded(() =>
        {
            using var reader = TestAudio.Open("short-granule.ogg");
            using var destination = new MemoryStream();
            reader.CopyTo(destination);
            return destination.Length;
        }, nameof(CopiesAFileWhoseStatedLengthUnderstatesItsContents));

        Assert.True(copied > 0);
    }
}
