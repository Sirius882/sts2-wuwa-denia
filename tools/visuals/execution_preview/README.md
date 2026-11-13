# Execution overlay preview

Run from the workspace root:

```powershell
python -B myModresources/denia/tools/visuals/prepare_execution_video.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py
python -B myModresources/denia/tools/visuals/execution_preview/run_preview.py --render
```

The first command regenerates the tracked character-only OGV from source frames 87–190.
The checks link the production overlay and timeline, and load the installed RitsuLib 0.6.11 / API 0.113 modules.
They cover the three-stack gate, dead/immune cases, contour triangulation, original-speed playback,
video completion, the RitsuLib-driven timeline and node cleanup when leaving the tree.
Entry regression checks require a flight from the left to the center before approaching the camera,
and verify the enlarged body covers the entire 16:9, 21:9 and 4:3 viewport geometrically.
The GPU run also requires every pixel to be white just before the video cut, including the cinema bars.

`--render` uses a hidden D3D12 window and captures actual GPU output at 30 FPS; this is a visual check,
not a performance benchmark. The backdrop is a schematic standalone scene, not an actual combat.
Generated projects, logs, captures and MP4 are under the workspace `.cache/denia-execution-preview/`.
Deleting that directory is safe. `RITSULIB_DIR` can override the installed runtime directory.
Standalone Godot may report that the Sentry extension is absent; it is not needed for this preview.

All game speed modes, including Instant, use the same 1x execution animation and video.
