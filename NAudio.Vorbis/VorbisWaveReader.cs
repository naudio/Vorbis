using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace NAudio.Vorbis
{
    /// <summary>
    /// A <see cref="Wave.WaveStream"/> that decodes an Ogg Vorbis stream to 32 bit IEEE float samples.
    /// </summary>
    public class VorbisWaveReader : Wave.WaveStream, Wave.ISampleProvider
    {
        VorbisSampleProvider _sampleProvider;

        /// <summary>
        /// Creates a new instance of <see cref="VorbisWaveReader"/> reading from the file specified.
        /// </summary>
        /// <param name="fileName">The path of the Ogg Vorbis file to read.  The file is closed when this instance is disposed.</param>
        public VorbisWaveReader(string fileName)
            : this(System.IO.File.OpenRead(fileName), true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="VorbisWaveReader"/> reading from the stream specified.
        /// </summary>
        /// <param name="sourceStream">The stream to read Ogg Vorbis data from.</param>
        /// <param name="closeOnDispose"><see langword="true"/> to close <paramref name="sourceStream"/> when this instance is disposed.</param>
        public VorbisWaveReader(System.IO.Stream sourceStream, bool closeOnDispose = false)
        {
            // To maintain consistent semantics with v1.1, we don't expose the events and auto-advance / stream removal features of VorbisSampleProvider.
            // If one wishes to use those features, they should really use VorbisSampleProvider directly...
            _sampleProvider = new VorbisSampleProvider(sourceStream, closeOnDispose);
        }

        /// <summary>
        /// Cleans up resources used by this instance.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="System.IO.Stream.Dispose()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sampleProvider?.Dispose();
                _sampleProvider = null;
            }
            
            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets the <see cref="Wave.WaveFormat"/> of the current stream, always 32 bit IEEE float.
        /// </summary>
        public override Wave.WaveFormat WaveFormat => _sampleProvider.WaveFormat;

        /// <summary>
        /// Gets the length of the current stream in bytes.
        /// </summary>
        /// <remarks>This is derived from the length the Vorbis stream reports, which for some files
        /// does not match the number of samples that actually decode.</remarks>
        public override long Length => _sampleProvider.Length * _sampleProvider.WaveFormat.BlockAlign;

        /// <summary>
        /// Gets or sets the position within the current stream, in bytes.
        /// </summary>
        /// <exception cref="InvalidOperationException">The stream does not support seeking.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or beyond <see cref="Length"/>.</exception>
        public override long Position
        {
            get => _sampleProvider.SamplePosition * _sampleProvider.WaveFormat.BlockAlign;
            set
            {
                if (!_sampleProvider.CanSeek) throw new InvalidOperationException("Cannot seek!");
                if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value));

                _sampleProvider.Seek(value / _sampleProvider.WaveFormat.BlockAlign);
            }
        }

        /// <summary>
        /// Reads decoded audio into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to fill with 32 bit IEEE float samples.</param>
        /// <returns>The number of bytes written to <paramref name="buffer"/>, or 0 at the end of the stream.</returns>
        public override int Read(Span<byte> buffer)
        {
            // reinterpret the caller's buffer as floats so the decoder can write straight into it,
            // then let Read(Span<float>) do the actual reading and adjust the count back to bytes
            return Read(MemoryMarshal.Cast<byte, float>(buffer)) * sizeof(float);
        }

        /// <summary>
        /// Reads decoded audio into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to fill with 32 bit IEEE float samples.</param>
        /// <param name="offset">The offset into <paramref name="buffer"/> to start writing at.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        /// <returns>The number of bytes written to <paramref name="buffer"/>, or 0 at the end of the stream.</returns>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <summary>
        /// Reads decoded audio into the sample buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to fill with samples.</param>
        /// <returns>The number of samples written to <paramref name="buffer"/>, or 0 at the end of the stream.</returns>
        public int Read(Span<float> buffer) => _sampleProvider.Read(buffer);

        /// <summary>
        /// Reads decoded audio into the sample buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to fill with samples.</param>
        /// <param name="offset">The offset into <paramref name="buffer"/> to start writing at.</param>
        /// <param name="count">The maximum number of samples to write.</param>
        /// <returns>The number of samples written to <paramref name="buffer"/>, or 0 at the end of the stream.</returns>
        public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <summary>
        /// Gets the number of Vorbis streams currently known in the container.
        /// </summary>
        public int StreamCount => _sampleProvider.StreamCount;

        /// <summary>
        /// Gets the index of the next stream to switch to, if one has been found by <see cref="GetNextStreamIndex"/>.
        /// </summary>
        public int? NextStreamIndex { get; set; }

        /// <summary>
        /// Looks for another Vorbis stream after the current one, storing its index in <see cref="NextStreamIndex"/>.
        /// </summary>
        /// <returns><see langword="true"/> if another stream was found.</returns>
        public bool GetNextStreamIndex()
        {
            if (!NextStreamIndex.HasValue)
            {
                NextStreamIndex = _sampleProvider.GetNextStreamIndex();
                return NextStreamIndex.HasValue;
            }
            return false;
        }

        /// <summary>
        /// Gets or sets the index of the Vorbis stream being decoded.
        /// </summary>
        public int CurrentStream
        {
            get => _sampleProvider.GetCurrentStreamIndex();
            set
            {
                _sampleProvider.SwitchStreams(value);

                NextStreamIndex = null;
            }
        }

        /// <summary>
        /// Gets the encoder's upper bitrate of the current selected Vorbis stream
        /// </summary>
        public int UpperBitrate => _sampleProvider.UpperBitrate;

        /// <summary>
        /// Gets the encoder's nominal bitrate of the current selected Vorbis stream
        /// </summary>
        public int NominalBitrate => _sampleProvider.NominalBitrate;

        /// <summary>
        /// Gets the encoder's lower bitrate of the current selected Vorbis stream
        /// </summary>
        public int LowerBitrate => _sampleProvider.LowerBitrate;

        /// <summary>
        /// Gets the encoder's vendor string for the current selected Vorbis stream
        /// </summary>
        public string Vendor => _sampleProvider.Tags.EncoderVendor;

        /// <summary>
        /// Gets the comments in the current selected Vorbis stream
        /// </summary>
        public string[] Comments => _sampleProvider.Tags.All.SelectMany(k => k.Value, (kvp, Item) => $"{kvp.Key}={Item}").ToArray();

        /// <summary>
        /// Gets stats from each decoder stream available
        /// </summary>
        public NVorbis.Contracts.IStreamStats[] Stats => new[] { _sampleProvider.Stats };
    }
}
