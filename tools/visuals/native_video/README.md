# Windows hardware menu video

The selected-character background uses the original H.264 MP4, unchanged at
3840x2160 / 60 FPS / 397 frames. It no longer runs the Theora decoder on Windows.
`DeniaHardwareVideo` calls the tiny `native/denia_video.bin` bridge to system Media
Foundation and D3D11. No external FFmpeg, codec pack, shell process or new managed
package is required at runtime. An IMF DXGI sample surface confirms hardware decode.

Decode/CPU readback occurs on a worker with four reusable NV12 buffers (~47.5 MiB).
The Godot main thread only uploads luma R8 + chroma RG8 textures, with BT.709 range
conversion in `denia_video_nv12.gdshader`. This is a GPU decode + readback/upload
path, not zero-copy rendering. Slow display frames discard stale video frames;
the original clock/duration is preserved. Hidden backgrounds freeze the clock and
the bounded producer stops after filling its four buffers. Leaving cancels the
worker without joining it on the main thread; COM/device/source are released on
the MTA thread that created them. Startup keeps the existing poster visible.

`native/denia_video.bin` is a small tracked Windows x64 PE DLL (renamed for PCK
packaging), built with static CRT and system DLL imports only. Rebuild with MSVC
C++ Build Tools + Windows SDK:

```powershell
python -B myModresources/denia/tools/visuals/native_video/build_native.py
```

`native_video.json` records source/binary hashes. Build scratch remains under the
workspace `.cache`; the bridge source/binary are reusable and stay in the repo.
The large MP4 is ignored by Git. `prepare_runtime_videos.py` copies/verifies it
from `artResources/Denia/选人界面背景视频.mp4` before packing. `export_presets.cfg`
explicitly includes `videos/*.mp4,native/*.bin` in PCK. At runtime the worker
materializes hash-named MP4/DLL files under `user://denia/video` so Windows APIs can
open them. These are reusable local assets, independent of development `.cache`.

Unsupported systems/decoder failures log a warning and retain the prior Theora
fallback; the Windows hardware path does not start the Theora stream. The quality
10 OGV master is retained. No selection input, game audio or execution movie changes.

Native GPU playback/layout/loop tests:

```powershell
python -B myModresources/denia/tools/visuals/character_select_preview/run_preview.py --output E:/STS2_mod/.cache/select-video-check
python -B myModresources/denia/tools/visuals/character_select_preview/run_preview.py --game --game-gpu --output E:/STS2_mod/.cache/select-video-game-check
```

The second command boots the installed game and current deployed DLL/PCK. It does
not start a run or change selections. A standalone preview alone cannot verify
mod C# script registration, PCK inclusion or extraction into the game user path.
