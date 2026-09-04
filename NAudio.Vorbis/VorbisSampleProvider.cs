using NAudio.Wave;
using NVorbis.Contracts;
using System;
using System.Collections.Generic;

namespace NAudio.Vorbis
{
    /// <summary>
    /// Implements <see cref="ISampleProvider"/> for NVorbis.
    /// </summary>
    public sealed class VorbisSampleProvider : ISampleProvider, IDisposable
    {
        private IContainerReader _containerReader;
        private IStreamDecoder _streamDecoder;
        private readonly LinkedList<IStreamDecoder> _streamDecoders = new LinkedList<IStreamDecoder>();
        private bool _hasEnded;
        private bool _totalSamplesUnavailable;

        /// <summary>
        /// Gets the number of streams currently known by this instance.
        /// </summary>
        public int StreamCount => _streamDecoders.Count;

        /// <summary>
        /// Gets the <see cref="WaveFormat"/> of the current stream.
        /// </summary>
        public WaveFormat WaveFormat { get; private set; }

        /// <summary>
        /// Gets the position of the current stream, in samples.
        /// </summary>
        public long SamplePosition => _streamDecoder.SamplePosition;

        /// <summary>
        /// Gets whether the current stream can seek.
        /// </summary>
        public bool CanSeek { get; }

        /// <summary>
        /// Gets the length of the current stream, in samples.
        /// </summary>
        public long Length => _streamDecoder.TotalSamples;

        /// <summary>
        /// Gets the <see cref="IStreamStats"/> instance for the current stream.
        /// </summary>
        public IStreamStats Stats => _streamDecoder.Stats;

        /// <summary>
        /// Gets the <see cref="ITagData"/> instance for the current stream.
        /// </summary>
        public ITagData Tags => _streamDecoder.Tags;

        /// <summary>
        /// Gets the encoder's upper bitrate of the current selected Vorbis stream
        /// </summary>
        public int UpperBitrate => _streamDecoder.UpperBitrate;

        /// <summary>
        /// Gets the encoder's nominal bitrate of the current selected Vorbis stream
        /// </summary>
        public int NominalBitrate => _streamDecoder.NominalBitrate;

        /// <summary>
        /// Gets the encoder's lower bitrate of the current selected Vorbis stream
        /// </summary>
        public int LowerBitrate => _streamDecoder.LowerBitrate;

        /// <summary>
        /// Raised when the current stream has been fully read.
        /// </summary>
        public event EventHandler<EndOfStreamEventArgs> EndOfStream;

        /// <summary>
        /// Raised when a new stream is selected that has a different <see cref="WaveFormat"/> than the previous stream.
        /// </summary>
        public event EventHandler WaveFormatChange;

        /// <summary>
        /// Raised when a new stream is selected.
        /// </summary>
        public event EventHandler StreamChange;

        private bool ProcessNewStream(IPacketProvider packetProvider)
        {
            IStreamDecoder decoder;
            try
            {
                decoder = new NVorbis.StreamDecoder(packetProvider);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                // an exception here probably means the packet provider returned non-Vorbis data, so warn and reject the stream
                System.Diagnostics.Trace.TraceWarning($"Could not load stream {packetProvider.StreamSerial} due to error: {ex.Message}");
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
            _streamDecoders.AddLast(decoder);
            return true;
        }

        private bool? GetNextDecoder(bool keepOldDecoder)
        {
            // look for the next unplayed decoder after our current decoder
            LinkedListNode<IStreamDecoder> node;
            if (_streamDecoder == null)
            {
                // first stream...
                node = _streamDecoders.First;
            }
            else
            {
                node = _streamDecoders.Find(_streamDecoder);
                while (node != null && node.Value.IsEndOfStream)
                {
                    node = node.Next;
                }
            }

            // clean up and remove the old decoder if we're not keeping it
            if (!keepOldDecoder)
            {
                _streamDecoders.Remove(_streamDecoder);
                _streamDecoder.Dispose();
            }

            // finally, if we still don't have a valid decoder, try to find a new stream in the container
            if (node == null && FindNextStream())
            {
                node = _streamDecoders.Last;
            }

            // switch to the new decoder, if one was found
            if (node != null)
            {
                return SwitchToDecoder(node.Value);
            }
            return null;
        }

        private bool SwitchToDecoder(IStreamDecoder nextDecoder)
        {
            _streamDecoder = nextDecoder;
            _hasEnded = false;

            var channels = WaveFormat?.Channels;
            var sampleRate = WaveFormat?.SampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_streamDecoder.SampleRate, _streamDecoder.Channels);

            if ((channels ?? WaveFormat.Channels) != WaveFormat.Channels || (sampleRate ?? WaveFormat.SampleRate) != WaveFormat.SampleRate)
            {
                WaveFormatChange?.Invoke(this, EventArgs.Empty);
                return true;
            }

            StreamChange?.Invoke(this, EventArgs.Empty);
            return false;
        }

        delegate T NodeFoundAction<T>(LinkedListNode<IStreamDecoder> node);

        private T FindStreamNode<T>(int index, NodeFoundAction<T> action)
        {
            if (_containerReader == null) throw new InvalidOperationException("Cannot operate on more than the current stream if not loaded from stream!");
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            var node = _streamDecoders.First;
            var count = -1;
            while (++count < index)
            {
                if (node.Next == null && !FindNextStream())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                node = node.Next;
            }
            return action(node);
        }

        private bool ForgetStreamAction(LinkedListNode<IStreamDecoder> node)
        {
            node.Value.Dispose();
            _streamDecoders.Remove(node);
            return false;
        }

        private bool SwitchStreamsAction(LinkedListNode<IStreamDecoder> node)
        {
            return SwitchToDecoder(node.Value);
        }

        internal int GetCurrentStreamIndex()
        {
            var cnt = -1;
            var node = _streamDecoders.First;
            while (node != null)
            {
                ++cnt;
                if (node.Value == _streamDecoder)
                {
                    break;
                }
                node = node.Next;
            }
            return cnt;
        }

        internal int? GetNextStreamIndex()
        {
            if (_containerReader == null) return null;

            var cnt = -1;
            var node = _streamDecoders.First;
            while (node != null)
            {
                ++cnt;
                var sd = node.Value;
                node = node.Next;
                if (sd == _streamDecoder)
                {
                    break;
                }
            }
            if (node != null)
            {
                // if we have a node, that means we have at least one known stream after the current one
                return cnt + 1;
            }

            // if we get here, we're out of known streams...

            // if we don't need the current stream or the container can seek, we can try for more...
            if (_containerReader.CanSeek || _streamDecoder.IsEndOfStream)
            {
                ++cnt;
                while (_containerReader.FindNextStream())
                {
                    if (_streamDecoders.Count > cnt)
                    {
                        return cnt;
                    }
                }
            }

            // no more streams
            return null;
        }

        /// <summary>
        /// Creates a new instance of <see cref="VorbisSampleProvider"/>.
        /// </summary>
        /// <param name="sourceStream">The stream to read for data.</param>
        /// <param name="closeOnDispose"><see langword="true"/> to close <paramref name="sourceStream"/> when this instance is disposed.</param>
        public VorbisSampleProvider(System.IO.Stream sourceStream, bool closeOnDispose = false)
        {
            _containerReader = new NVorbis.Ogg.ContainerReader(sourceStream, closeOnDispose)
            {
                NewStreamCallback = ProcessNewStream
            };
            CanSeek = _containerReader.CanSeek;
            if (!_containerReader.TryInit()) throw new ArgumentException("Could not initialize container!");

            if (!GetNextDecoder(true).HasValue)
            {
                throw new InvalidOperationException("Container initialized, but no stream found?");
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="VorbisSampleProvider"/> that will attempt to use the specified <see cref="IPacketProvider"/> to decode audio.
        /// </summary>
        /// <param name="packetProvider"></param>
        public VorbisSampleProvider(IPacketProvider packetProvider)
            : this(new NVorbis.StreamDecoder(packetProvider), packetProvider.CanSeek)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="VorbisSampleProvider"/> that will read from a single specified decoder stream.
        /// </summary>
        /// <param name="streamDecoder">The decoder to read from.</param>
        /// <param name="allowSeek">Sets whether to allow seek operations to be attempted on this stream.  Note that setting this to <see langword="true"/>
        /// when the underlying stream doesn't support it will still generate an exception from the decoder when attempting seek operations.</param>
        public VorbisSampleProvider(IStreamDecoder streamDecoder, bool allowSeek)
        {
            _streamDecoders.AddLast(streamDecoder);
            SwitchToDecoder(streamDecoder);
            CanSeek = allowSeek;
        }

        /// <summary>
        /// Reads decoded audio data from the current stream.
        /// </summary>
        /// <param name="buffer">The buffer to write the data to.  If its length is not a multiple of
        /// <see cref="WaveFormat.Channels"/>, the trailing partial sample frame is not filled.</param>
        /// <returns>The number of values written to <paramref name="buffer"/>.</returns>
        public int Read(Span<float> buffer)
        {
            if (_streamDecoder.IsEndOfStream)
            {
                if (_hasEnded)
                {
                    // we've ended and don't have any data, so just bail
                    return 0;
                }

                var eosea = new EndOfStreamEventArgs();
                EndOfStream?.Invoke(this, eosea);
                _hasEnded = true;
                if (eosea.AdvanceToNextStream)
                {
                    var formatChanged = GetNextDecoder(eosea.KeepStream);
                    if (formatChanged ?? true)
                    {
                        return 0;
                    }
                }
            }

            // the decoder requires whole sample frames, but callers are free to pass any length
            var count = buffer.Length - buffer.Length % WaveFormat.Channels;

            // Never ask the decoder for more than the stream has left to give.  Over-reading past the
            // end can leave NVorbis's decode state with a negative valid length, and its Read loop then
            // spins forever without making progress (naudio/Vorbis#16, NVorbis#40).  TotalSamples is
            // only knowable when the container can seek, which is also the condition NVorbis's own fix
            // for this uses, so forward-only streams are left alone.
            if (CanSeek && !_totalSamplesUnavailable)
            {
                try
                {
                    var remaining = (_streamDecoder.TotalSamples - _streamDecoder.SamplePosition) * WaveFormat.Channels;
                    if (remaining < count) count = (int)Math.Max(remaining, 0);
                }
                catch (NotSupportedException)
                {
                    // constructed with allowSeek: true over a source that can't actually seek, so the
                    // decoder can't report a total; stop asking and read unclamped as before
                    _totalSamplesUnavailable = true;
                }
            }

            // NVorbis 0.10.x only offers the (buffer, offset, count) overload; the 1.0 line adds
            // Read(Span<float>) and marks this one obsolete.  Keep calling it so one source tree
            // builds against both, and suppress the obsoletion warning a 1.0 preview build raises.
#pragma warning disable CS0618 // Type or member is obsolete
            return _streamDecoder.Read(buffer, 0, count);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Reads decoded audio data from the current stream.
        /// </summary>
        /// <param name="buffer">The buffer to write the data to.</param>
        /// <param name="offset">The offset into <paramref name="buffer"/> to start writing data.</param>
        /// <param name="count">The number of values to write.  This must be a multiple of <see cref="WaveFormat.Channels"/>.</param>
        /// <returns>The number of values written to <paramref name="buffer"/>.</returns>
        public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <summary>
        /// Seeks the current stream to the sample position specified.
        /// </summary>
        /// <param name="samplePosition">The sample position to seek to.</param>
        /// <returns>The sample position seeked to.</returns>
        public long Seek(long samplePosition)
        {
            if (!CanSeek) throw new InvalidOperationException("Cannot seek underlying stream!");
            if (samplePosition < 0 || samplePosition > _streamDecoder.TotalSamples) throw new ArgumentOutOfRangeException(nameof(samplePosition));

            _streamDecoder.SeekTo(samplePosition);

            return _streamDecoder.SamplePosition;
        }

        /// <summary>
        /// Removes the stream at the index specified from the internal list and cleans up its resources.
        /// </summary>
        /// <param name="index">The index to remove</param>
        public void RemoveStream(int index) => FindStreamNode(index, ForgetStreamAction);

        /// <summary>
        /// Switches to the stream index specified.
        /// </summary>
        /// <param name="index">The index to switch to.</param>
        /// <returns><see langword="true"/> if the newly-selected decoder has a different sample rate or number of channels than the previous one.  Otherwise, <see langword="false"/>.</returns>
        public bool SwitchStreams(int index) => FindStreamNode(index, SwitchStreamsAction);

        /// <summary>
        /// Finds all available streams in the container.
        /// </summary>
        public void FindAllStreams()
        {
            if (!CanSeek)
            {
                if (_containerReader == null) throw new InvalidOperationException("No container loaded!");
                throw new InvalidOperationException("Cannot seek container!  Will discover streams as they are encountered.");
            }

            while (_containerReader.FindNextStream())
            {
            }
        }

        /// <summary>
        /// Finds the next available stream in the container.
        /// </summary>
        /// <returns><see langword="true"/> if another Vorbis stream was found, otherwise <see langword="false"/>.</returns>
        public bool FindNextStream()
        {
            if (_containerReader?.CanSeek ?? false)
            {
                var lastStream = _streamDecoders.Last?.Value;
                while (_containerReader.FindNextStream() && lastStream == _streamDecoders.Last.Value)
                {
                }
                return _streamDecoders.Last != null && lastStream != _streamDecoders.Last.Value;
            }
            return false;
        }

        /// <summary>
        /// Cleans up resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            var foundCurrent = false;
            foreach (var decoder in _streamDecoders)
            {
                foundCurrent |= decoder == _streamDecoder;
                decoder.Dispose();
            }
            _streamDecoders.Clear();
            if (!foundCurrent)
            {
                _streamDecoder.Dispose();
            }

            _containerReader?.Dispose();
            _containerReader = null;
        }
    }
}
