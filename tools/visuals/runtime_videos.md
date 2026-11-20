# Local runtime videos

Windows character selection now uses the original MP4 through system Media
Foundation/D3D11 hardware decode; see `native_video/README.md`. Large `videos/*.mp4`
are also ignored by Git and verified/copied from `artResources` before pack.
The quality-10 OGV below remains as a compatibility fallback, not the primary
Windows playback path. The execution clip uses the completed quality-10 Animagine remaster described below.

Large `videos/*.ogv` files are ignored by Git. Their stable `.uid` files, preparation
scripts and small metadata remain tracked. Pack scripts run this command before
Godot export (Python 3.11+; FFmpeg/ffprobe required for regeneration):

```powershell
python -B myModresources/denia/tools/visuals/prepare_runtime_videos.py
```

The workspace-local masters live in `artResources/Denia/人物形象与动画/`, outside Git and
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

Master relocation (2026-11-15): the earlier `artResources/Denia/游戏视频/` directory
was merged into `artResources/Denia/人物形象与动画/`. Video bytes are unchanged.
`denia_execution_middle.ogv` is the current quality-10 Animagine runtime master.
`denia_execution_alignment_reference_20261115.ogv` is the earlier quality-8 clip
kept solely as an alignment reference; it is not restored or packaged by these
scripts. `denia_character_select_bg.ogv` remains the compatibility fallback.

## Battle clips

`prepare_battle_videos.py` builds transparent character video from the sources in
`artResources/Denia/人物形象与动画`. The recipe records source frame counts and
zero-based impact frame indices. It retains the original video timebase, with a
30 fps sample grid for 60 fps sources plus every marked impact. The stand-up MP4
uses the matching alpha atlas to reverse its opening zoom and reconstruct the
checkerboard-composited edges. Each DBV2 asset includes the primary stereo stream;
stand-up also contains a reversed stream for the sit-down phase.

Battle videos and code animations share damage-wait and interruption rules.
Normal mode protects the attack until the required damage rounds settle. Fast
pink attacks use the original card timing. Fast black attacks wait at the video
impact times at 3× speed and release outstanding waits when replaced. The black
triple clip retains its third swing, with two damage waits as in the code attack.

The local `战斗视频运行时` directory contains the reusable output masters and their
fingerprints. Both pack scripts call the preparation step and export the DBV2
assets. A fresh conversion needs FFmpeg with libvorbis, NumPy, OpenCV and Pillow.
