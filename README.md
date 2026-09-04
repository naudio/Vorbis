# NAudio.Vorbis

[![build](https://github.com/naudio/Vorbis/actions/workflows/build.yml/badge.svg)](https://github.com/naudio/Vorbis/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/NAudio.Vorbis.svg)](https://www.nuget.org/packages/NAudio.Vorbis/)

NAudio.Vorbis is a convenience wrapper that lets you play and read Ogg Vorbis
files with [NAudio](https://github.com/naudio/NAudio), using
[NVorbis](https://github.com/NVorbis/NVorbis) to do the decoding.

## Requirements

| NAudio.Vorbis | Requires |
| --- | --- |
| 3.x | NAudio 3, .NET 9 |
| 1.5.0 | NAudio 2, .NET Standard 2.0 |

NAudio 3 dropped .NET Framework and .NET Standard 2.0 and changed the `Read`
signatures on `IWaveProvider` and `ISampleProvider` to take a `Span<T>`, so
NAudio.Vorbis 3.0 is a clean break. If you are still on NAudio 2, stay on
NAudio.Vorbis 1.5.0.

## Playing a file

`VorbisWaveReader` is a `WaveStream`, so it plugs into any NAudio output device:

```cs
using NAudio.Vorbis;
using NAudio.Wave;

using var reader = new VorbisWaveReader("path/to/file.ogg");
using var player = new WasapiPlayerBuilder().Build();

player.Init(reader);
player.Play();

// wait here until playback stops or should stop
```

`WasapiPlayer` comes from the `NAudio.Wasapi` package. Any other NAudio output
works the same way — `WaveOut` from `NAudio.WinMM`, or `AlsaOut` from
`NAudio.Alsa` on Linux.

## Reading samples

`VorbisWaveReader` also implements `ISampleProvider`, so it drops straight into
a sample pipeline:

```cs
using NAudio.Wave.SampleProviders;

using var reader = new VorbisWaveReader("path/to/file.ogg");
var mixer = new MixingSampleProvider(new[] { (ISampleProvider)reader });
```

Or convert to a WAV file:

```cs
using var reader = new VorbisWaveReader("path/to/file.ogg");
WaveFileWriter.CreateWaveFile("output.wav", reader);
```

`VorbisSampleProvider` is the layer underneath, and exposes the things
`VorbisWaveReader` deliberately hides — the end-of-stream event, and switching
between the logical streams of a chained file.

## Seeking

`VorbisWaveReader` supports seeking when the underlying stream does. Positions
are in bytes and should be a multiple of `WaveFormat.BlockAlign`:

```cs
reader.CurrentTime = TimeSpan.FromSeconds(30);
```

A source that cannot seek (a network or pipe stream) still decodes; only
`Position` and `Length` are unavailable.

## Known NVorbis limitations

Decoding is NVorbis's job, and the current stable release (0.10.5) has bugs we
cannot work around from here. All three are fixed in the NVorbis 1.0.0 line,
which is still a prerelease — a stable package should not depend on one, so
releases stay on 0.10.5 until NVorbis 1.0 ships.

 * **Seeking into the final page can throw** `InvalidDataException: GranulePos mismatch` when the last page's granule position does not line up with the packets on it — the ordinary shape of an encoder's trailing partial page (NVorbis#39)
 * **Seeking back to the start does not always land on the first sample.** The position reads back as 0, but decoding can resume elsewhere in the file
 * **A forward-only source can still hang** on a file whose granule positions understate its contents. For seekable sources this is handled — reads are clamped to what the stream has left — but with no length to clamp to there is nothing to be done here (NVorbis#40, [#16](https://github.com/naudio/Vorbis/issues/16))

To try a build against the NVorbis 1.0 prerelease:

```
dotnet build -p:NVorbisVersion=1.0.0-rc.2
```

The release workflow takes the same value, so preview packages on the NVorbis
1.0 prerelease can be published without changing the repo default.

## Strong naming

The assembly is strong-named from 3.0 onwards, as NAudio and NLayer are; 1.5.0
and earlier were not, so the assembly identity changes with this release. The
key is checked in at the repo root: for open-source strong naming the private
half is not a secret — it establishes assembly identity, it does not protect
anything — and a key nobody can build with would be worse than none.

NVorbis is not strong-named, in any version, so the build suppresses CS8002 for
that reference. .NET Framework refused to load an unsigned reference from a
signed assembly, but .NET Core and later dropped strong-name verification, so on
`net9.0` it has no effect.

## Building

```
dotnet build NAudio.Vorbis.slnx
dotnet test NAudio.Vorbis.slnx
```

The solution is in the XML `.slnx` format, so it needs the .NET 9.0.200 SDK or
later; `global.json` pins that floor and rolls forward to newer SDKs.

`TestApp` is a WinForms harness (Windows only) for playing a file by hand and
dragging a position bar around, which is the most practical way to exercise
seeking. It reports failed seeks in its status line rather than crashing, so
you can see which files misbehave.

The test corpus in `NAudio.Vorbis.Tests/TestFiles` is generated from synthesised
tones rather than taken from any recording; `NAudio.Vorbis.Tests/tools` has the
script that regenerates it.

## Releasing

`build.yml` builds and tests every push and pull request, and separately builds
against the NVorbis 1.0 prerelease. `release.yml` publishes to NuGet:

 * **Final release** — set `VersionPrefix` in `Directory.Build.props`, rename the `### Unreleased` section of `RELEASE_NOTES.md` to `### <version> (date)`, then push a matching `v*` tag. The workflow refuses to cut a final release against a prerelease NVorbis
 * **Preview** — run the workflow manually from `master`. It publishes `<VersionPrefix>-preview.<run number>`, or the label you pass in `milestone`. Pass `nvorbis_version` to build the preview against a different NVorbis

Publishing uses NuGet trusted publishing (OIDC), so there is no API key to
rotate; the `NUGET_USER` repository variable names the NuGet.org account.

## Licence

MIT.
