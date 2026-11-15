# Execution video remaster (2026-11-15)

Paused at the user's request on 2026-11-15 at 11:10 +08:00. Completed and hash-verified:
34/137 frames (0–29, 49–51, 136). Frame 30 was interrupted; resume from frame 30.
ComfyUI and the batch client were stopped and GPU memory released. Do not resume
until the user explicitly asks. The runtime OGV is still the old quality-8 asset;
no new video has been encoded, deployed or pushed. The aligned WAV and reusable
workflow are prepared. Durable progress and pause notes are next to `frames_4k`.

The re-cut source is `artResources/Denia/人物形象与动画/环三层集谐处决动画原视频.mp4`.
Alignment uses the previous runtime OGV at 192×88 plus soundtrack cross-correlation.
Source frames 54–190 inclusive match the old 137-frame clip; the closest competing
frame offsets have substantially larger RGB error. Source first video PTS is
0.114 seconds, so the clip starts at 1.914 seconds. The 0.8-second entrance starts
audio at 1.114 seconds (48 kHz sample 53472). The extracted unfaded audio body
matches the previous WAV exactly, sample for sample.

Visual timing stays 137/30 seconds. Audio stays 401600 stereo PCM16 samples,
with the same 0.8-second half-sine fade-in, 3-second half-sine fade-out and
post-video-only decaying echo recipe. Neither damage release nor input blocking
is extended by the tail.

Run from the workspace root:

```powershell
python -B myModresources/denia/tools/visuals/remaster_execution.py extract
powershell -File myModresources/denia/tools/visuals/start_execution_comfy.ps1
# In another shell, after the server is ready:
python -B myModresources/denia/tools/visuals/remaster_execution.py upscale
python -B myModresources/denia/tools/visuals/prepare_execution_video.py
python -B myModresources/denia/tools/visuals/prepare_execution_audio.py
```

The workflow uses the user's `animagine-xl-4.0-opt.safetensors` checkpoint from
`F:/ComfyUI/ComfyUI/models/checkpoints`. Lanczos establishes the 3840×1772 canvas;
UltimateSDUpscaleNoUpscale performs actual per-frame diffusion detail enhancement
with the checkpoint. It uses fixed seed 118451, DPM++ 2M/Karras, 12 steps,
CFG 4.5, denoise 0.10, 768×768 tiles, 32-pixel padding/mask blur, one tile per
batch and tiled VAE decoding. A final resize restores exactly 1772 pixels after
the diffusion node's multiple-of-eight canvas rounding. The original frames,
poses, motion blur and colors guide the low-denoise refinement.

The earlier 1024-pixel/two-tile/high-VRAM experiment exceeded the user's memory
budget and was stopped. The final server reserves 3 GB VRAM, uses normal model
management and disables pinned memory/async offload. This controls peak memory;
it does not reduce final output dimensions or sampling steps. Review adjacent
frames and fast effects before deployment: a fixed seed alone does not prove
temporal consistency.

Runtime OGV uses Theora quality 10, yuv420p, 30 FPS, original duration and no
embedded audio. Sound remains the separately aligned WAV. PNG frames and progress
records live permanently under `artResources/Denia/人物形象与动画/处决视频4K_Animagine_20261115/`;
large assets are not tracked. Workflows, metadata and scripts are tracked. Logs,
extracted source frames and ComfyUI scratch outputs live in workspace `.cache`.
After cache cleanup, re-extract source frames and restart the server; completed
PNG frames resume by hash without repeating their generation.
