# Local runtime videos

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

The execution master is rebuilt from the re-cut source
`artResources/Denia/人物形象与动画/环三层集谐处决动画原视频.mp4`.
`execution_video_remaster.json` records the aligned source frames and source PTS;
`execution_upscale_workflow.json` and `remaster_execution.py` run the specified
Animagine XL checkpoint through local ComfyUI, using 768-pixel single-tile batches.
`prepare_execution_video.py` encodes the preserved 3840×1772 PNG frames at quality 10.
`execution_video.json` records the resulting master and per-frame hashes. Large
PNG frames and OGV masters stay in `artResources`, outside Git and `.cache`.
