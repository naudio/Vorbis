### Unreleased

<!--
Bullets land here as PRs merge. The maintainer renames this section to
"### 3.0.0 (date)" at release time - the release workflow requires a section
matching the version being tagged, and uses this "Unreleased" section for
previews. The workflow strips these HTML comments before publishing.
-->

NAudio.Vorbis 3.0 requires NAudio 3 and .NET 9. If you are still on NAudio 2 or
need .NET Standard 2.0, stay on NAudio.Vorbis 1.5.0.

 * **Breaking:** the minimum target framework is now `net9.0`; .NET Standard 2.0 is dropped, following NAudio 3
 * **Breaking:** `Read` follows NAudio 3's `Span<T>` signatures. `VorbisSampleProvider` implements `ISampleProvider.Read(Span<float>)` and `VorbisWaveReader` implements `Read(Span<byte>)`; the `(buffer, offset, count)` overloads remain as forwarders, so existing calling code still compiles. Without this, loading the assembly against NAudio 3 threw `TypeLoadException: Method 'Read' in type 'NAudio.Vorbis.VorbisWaveReader' does not have an implementation` (#20)
 * **Breaking:** removed the long-obsolete `IsParameterChange`, `ClearParameterChange` and `ContainerOverheadBits` members
 * **Breaking:** the assembly is now strong-named, matching NAudio and NLayer. Earlier versions were unsigned, so the assembly identity changes
 * Fixed a hang decoding files whose stated length understates their contents. A read that runs past the end can leave NVorbis's decode state with a negative valid length, after which its `Read` loop spins forever - `Stream.CopyTo` always over-reads on its final call, which is how this was hit. Reads are now clamped to what the stream has left (#16)
 * Updated to NVorbis 0.10.5, which fixes `VorbisWaveReader.Comments` throwing an `InvalidCastException` on every file (#17)
 * `VorbisWaveReader` reads decoded audio straight into the caller's buffer, removing a `[ThreadStatic]` scratch buffer and a copy per read
 * The public API is now fully XML-documented, so the package ships complete IntelliSense
 * Added a unit test suite, a WinForms test harness, and build and release pipelines
