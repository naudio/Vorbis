# Test files

These Ogg Vorbis files are generated from synthesised sine tones rather than taken
from any existing recording, so they carry no third-party rights and can be
regenerated at will. `tools/generate-test-files.sh` produces them; see that script
for the exact parameters.

| File | Contents |
| --- | --- |
| `sine-stereo.ogg` | 1.5s, 44.1kHz stereo, 440Hz left / 660Hz right, quality 3, tagged |
| `sine-mono.ogg` | 0.75s, 22.05kHz mono, 523.25Hz, quality 1, tagged |
| `short-granule.ogg` | `sine-stereo.ogg` with the final page's granule position lowered by 2000 samples (and the page CRC recomputed), so the stream's stated length understates what it actually decodes to |

`short-granule.ogg` reproduces issue #16. A granule position that understates
the content makes the last read run past the stream's stated end, which is the
state that sends NVorbis's decode loop into a spin it never comes out of
(NVorbis#40, fixed in the NVorbis 1.0.0 line). Decoding it without the guard in
`VorbisSampleProvider.Read` hangs indefinitely, so `OverReadRegressionTests`
covers it under a watchdog. It is derived from our own generated file rather
than the .ogg attached to the issue, which has no clear licence.

The first two differ in channel count, sample rate and quality on purpose: several of
the things worth testing (frame alignment when a buffer is not a whole number of
sample frames, block align arithmetic, `Length` in bytes vs samples) behave
differently for mono and stereo.
