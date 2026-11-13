# Execution overlay preview

Run from the workspace root:

```powershell
python -B myModresources/denia/tools/visuals/prepare_execution_video.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py --render
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
