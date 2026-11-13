# Execution overlay preview

Run from the workspace root:

```powershell
python -B myModresources/denia/tools/visuals/prepare_execution_video.py
python -B myModresources/denia/tools/visuals/prepare_execution_audio.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py --render
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py --audio-check
```

The first command regenerates the tracked OGV from source frames 54–190: the extreme-close-up
gingerbread transition and the character segment. The legacy resource filename remains
`denia_execution_middle.ogv`. It has 137 frame intervals at 30 FPS (4.566667 seconds).
Theora may represent repeated frames with empty packets, so the preparation check uses its timeline
length rather than requiring a decoded picture for every interval.

The overlay applies a growing, uneven pink-white filter for 0.8 seconds, then cuts directly to the clip.
It does not draw gingerbread geometry. It resumes the predecoded player one frame before the cut,
without the old 0.2-second pre-roll that would skip the extreme close-up. Damage release stays at
169/30 seconds, followed by the existing code-driven impact and return.

The checks link production code and use the installed RitsuLib 0.6.11 / API 0.113 modules.
They cover the three-stack/dead/immune gates, original-speed playback, video timing and completion,
the RitsuLib timeline and cleanup. GPU checks require an early video position and a pale first visible
video frame at the direct cut. Neutral-backdrop captures test uneven opacity, spatially different
growth rates and the final pink-white veil. These captures are `dream_*.png`.

`--render` uses a hidden D3D12 window and captures actual GPU output at 30 FPS; this is a visual check,
not a performance benchmark. The backdrop is a schematic standalone scene, not an actual combat.
Generated projects, logs, captures and MP4 are under the workspace `.cache/denia-execution-preview/`.
Deleting that directory is safe. `RITSULIB_DIR` can override the installed runtime directory.
Standalone Godot may report that the Sentry extension is absent; it is not needed for this preview.

All game speed modes, including Instant, use the same 1x execution animation and video.

The separate stereo WAV starts at filter time zero, independently of the hidden video pre-roll.
`execution_audio_alignment.json` records the preserved sound source, hashes, frame offset and exact
sample range. Slice frame 54 matches sound-source frame 204 at PTS 6.906s; the 0.8s entrance
therefore starts at source audio 6.106s (sample 293088 at 48 kHz). The WAV contains 401600 samples
(8.366667s): a smooth 0.8s fade-in, unchanged aligned middle, then a 3s fade-out from the end of the
video at presentation time 5.366667s. The source ends before this tail finishes, so decaying echoes
extend only its post-video audio; dialogue and the video segment are not looped or stretched.
The visual return still ends at 6.133333s. The hidden overlay then releases input and only retains
the fading audio until it finishes. Leaving the room or replacing the presentation stops both players.
The video remains silent to avoid doubled audio.

The rendered MP4 now includes this WAV from timestamp zero. Its sound is muxed from the verified
samples; offline fixed-FPS GPU capture is not a real-time audio recording. `--audio-check` separately
runs at real-time 60 FPS with WASAPI audio output and checks the native playback clock against the
presentation clock, accounting for mixer/output latency. It does not test physical speakers or game mixing.
