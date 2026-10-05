# Audio test fixtures

Small synthetic Ogg Vorbis files used by `OggDecoderTests`, `AudioClipLoaderTests` and `MusicPlayerTests`.
They are generated, not recorded: no third party content. Each one is 0.5 s long and under 7 KB.

Recreate them from this folder with the ffmpeg shipped in the NuGet cache
(`monogame.tool.ffmpeg`, any ffmpeg with libvorbis works):

```bash
FFMPEG=C:/Users/<user>/.nuget/packages/monogame.tool.ffmpeg/7.0.0.10/binaries/windows-x64/ffmpeg.exe

# 440 Hz, mono, 44100 Hz
"$FFMPEG" -y -f lavfi -i "sine=frequency=440:sample_rate=44100:duration=0.5" -ac 1 -c:a libvorbis -q:a 4 sine-440hz-mono-44100.ogg

# stereo, 440 Hz left / 880 Hz right, 44100 Hz
"$FFMPEG" -y -f lavfi -i "sine=frequency=440:sample_rate=44100:duration=0.5" -f lavfi -i "sine=frequency=880:sample_rate=44100:duration=0.5" -filter_complex "[0:a][1:a]amerge=inputs=2[a]" -map "[a]" -ac 2 -c:a libvorbis -q:a 4 sine-440hz-left-880hz-right-stereo-44100.ogg

# 440 Hz, mono, 22050 Hz
"$FFMPEG" -y -f lavfi -i "sine=frequency=440:sample_rate=22050:duration=0.5" -ac 1 -c:a libvorbis -q:a 4 sine-440hz-mono-22050.ogg
```

The encoder output is not bit-identical between ffmpeg builds; the tests only rely on rate, channel count,
approximate length and measured frequency.
