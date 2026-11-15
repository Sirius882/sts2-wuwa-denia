# Execution video remaster (2026-11-15)

Completed on 2026-11-15 at 17:55 +08:00 after the user resumed the task at
16:37. All 137 frame hashes and 3840x1772 dimensions were verified. The durable
frame directory, progress record and prepared soundtrack remain available for
reproduction; no ComfyUI process for this task remains running.

Runtime video is now Theora quality 10: 35,048,069 bytes, 137 decoded pictures and
137/30 seconds. SSIM against the enhanced PNG sequence is 0.995590. The source
frames, timing and soundtrack alignment below are unchanged. Full-frame QA found
maximum downsampled source MSE 0.00024632 and motion-compensated enhancement
residual MSE 0.000263765; contact sheets and native preview are next to `frames_4k`.
82 D3D12 rendering checks passed. Real-time audio initially exceeded the existing
80ms limit (84.3ms); the recheck passed all 36 assertions with 74.0ms maximum drift,
while shutdown still reported the existing single-resource cleanup diagnostic.
These checks do not establish real combat performance or speaker output.

The earlier pause at 11:10 saved 34 frames (0-29, 49-51, 136). Resume skipped those
verified images. An exclusive Windows file lock on the monitor report interrupted
the client once; its already-completed server output was recovered without
regeneration, and report reads now retry brief sharing violations. This retry was
verified using an actual exclusive Windows file handle. The same memory profile
stayed at 9.17 GiB dedicated / 161 MiB shared throughout the resumed run.

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
# In another shell, monitor only this ComfyUI server PID:
powershell -File myModresources/denia/tools/visuals/watch_execution_gpu.ps1 -ComfyProcessId <PID>
# After the server is ready and the monitor is running:
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

The GPU monitor is reusable source, not a cache dependency. It samples the specified
server process only and interrupts its pending/running queue after two consecutive
samples above 1 GiB shared GPU memory. Stop this task's server after generation to
release its memory; do not stop unrelated Python or ComfyUI processes.
