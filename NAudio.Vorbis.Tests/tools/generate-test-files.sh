#!/usr/bin/env bash
# Regenerates the Ogg Vorbis files in ../TestFiles.
#
# The files are synthesised sine tones, not excerpts of any recording, so the
# test corpus carries no third-party rights. Requires python3 and oggenc
# (Debian/Ubuntu: `apt-get install vorbis-tools`).
set -euo pipefail

out="$(cd "$(dirname "$0")/../TestFiles" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

python3 - "$tmp" <<'PY'
import math, struct, sys, wave

def write(path, rate, channels, seconds, freqs):
    w = wave.open(path, 'wb')
    w.setnchannels(channels); w.setsampwidth(2); w.setframerate(rate)
    frames = bytearray()
    for i in range(int(rate * seconds)):
        t = i / rate
        # short fade in/out so the first and last samples are well defined
        env = min(1.0, t * 20, (seconds - t) * 20)
        for c in range(channels):
            frames += struct.pack('<h', int(math.sin(2 * math.pi * freqs[c] * t) * 0.5 * env * 32767))
    w.writeframes(bytes(frames)); w.close()

tmp = sys.argv[1]
write(f'{tmp}/sine-stereo.wav', 44100, 2, 1.5,  [440.0, 660.0])
write(f'{tmp}/sine-mono.wav',   22050, 1, 0.75, [523.25])
PY

oggenc -Q -q 3 -o "$out/sine-stereo.ogg" \
  -a "NAudio.Vorbis" -t "Stereo Test Tone" \
  -c "DESCRIPTION=440/660Hz sine, generated for tests" "$tmp/sine-stereo.wav"

oggenc -Q -q 1 -o "$out/sine-mono.ogg" -t "Mono Test Tone" "$tmp/sine-mono.wav"

# short-granule.ogg is sine-stereo.ogg with the last page's granule position pulled
# back so the stated length understates the content - see ../TestFiles/README.md.
python3 "$(dirname "$0")/patch-last-granule.py" \
  "$out/sine-stereo.ogg" "$out/short-granule.ogg" -2000

echo "Regenerated:"
ls -l "$out"/*.ogg
