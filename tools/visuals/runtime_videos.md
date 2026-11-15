# Local runtime videos

Windows character selection now uses the original MP4 through system Media
Foundation/D3D11 hardware decode; see `native_video/README.md`. Large `videos/*.mp4`
are also ignored by Git and verified/copied from `artResources` before pack.
The quality-10 OGV below remains as a compatibility fallback, not the primary
Windows playback path. The execution-video pipeline remains unchanged/paused.

Large `videos/*.ogv` files are ignored by Git. Their stable `.uid` files, preparation
scripts and small metadata remain tracked. Pack scripts run this command before
Godot export (Python 3.11+; FFmpeg/ffprobe required for regeneration):

```powershell
python -B myModresources/denia/tools/visuals/prepare_runtime_videos.py
```

The workspace-local masters live in `artResources/Denia/游戏视频/`, outside Git and
outside disposable `.cache`. Missing runtime files are restored from those masters.
A fresh checkout needs the local source/master assets before packing.

The character-select source is `artResources/Denia/选人界面背景视频.mp4`.
Its default Theora quality is now 10 (maximum), retaining 3840×2160, 60 FPS and
original duration. To regenerate with another quality (0–10):

```powershell
python -B myModresources/denia/tools/visuals/prepare_character_select_video.py --quality 10
```

`--ensure` verifies the source/output fingerprints and skips matching work.
Quality mode uses variable bitrate; adding `-b:v` in this mode does not set bitrate.
This is lossy transcoding, even at quality 10. Godot plays the prepared stream;
it does not encode it again during import, export or playback.

The preserved execution master keeps its existing quality and timing. Its original
MP4 is currently absent from the documented source path, so it must not be deleted
on the assumption that `prepare_execution_video.py` can regenerate it. This script
and its original trim recipe are retained for when that source is available again.
