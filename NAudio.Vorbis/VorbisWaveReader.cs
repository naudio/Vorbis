using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace NAudio.Vorbis
{
    public class VorbisWaveReader : Wave.WaveStream, Wave.ISampleProvider
    {
        VorbisSampleProvider _sampleProvider;

        public VorbisWaveReader(string fileName)
            : this(System.IO.File.OpenRead(fileName), true)
        {
        }

        public VorbisWaveReader(System.IO.Stream sourceStream, bool closeOnDispose = false)
        {
            // To maintain consistent semantics with v1.1, we don't expose the events and auto-advance / stream removal features of VorbisSampleProvider.
            // If one wishes to use those features, they should really use VorbisSampleProvider directly...
            _sampleProvider = new VorbisSampleProvider(sourceStream, closeOnDispose);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sampleProvider?.Dispose();
                _sampleProvider = null;
            }
            
            base.Dispose(disposing);
        }

        public override Wave.WaveFormat WaveFormat => _sampleProvider.WaveFormat;

        public override long Length => _sampleProvider.Length * _sampleProvider.WaveFormat.BlockAlign;

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

        public override int Read(Span<byte> buffer)
        {
            // reinterpret the caller's buffer as floats so the decoder can write straight into it,
            // then let Read(Span<float>) do the actual reading and adjust the count back to bytes
            return Read(MemoryMarshal.Cast<byte, float>(buffer)) * sizeof(float);
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public int Read(Span<float> buffer) => _sampleProvider.Read(buffer);

        public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public int StreamCount => _sampleProvider.StreamCount;

        public int? NextStreamIndex { get; set; }

        public bool GetNextStreamIndex()
        {
            if (!NextStreamIndex.HasValue)
            {
                NextStreamIndex = _sampleProvider.GetNextStreamIndex();
                return NextStreamIndex.HasValue;
            }
            return false;
        }

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
