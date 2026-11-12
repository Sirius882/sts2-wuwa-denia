"""Repeatable Godot 4.5.1 mechanism checks; all generated files stay in workspace .cache."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import subprocess

from PIL import Image, ImageDraw


SOURCE = Path(__file__).resolve().parent
WORKSPACE = SOURCE.parents[4]


def make_atlas(path: Path) -> None:
    """Build a fixed specimen texture, not a Denia production asset or frame animation."""
    image = Image.new("RGBA", (512, 256))
    bases = [(201, 178, 218), (243, 199, 220), (152, 104, 151), (255, 223, 207)]
    for tile, base in enumerate(bases):
        for y in range(256):
            for x in range(128):
                shade = 0.72 + 0.28 * (1 - abs(x / 127 - 0.38)) - 0.12 * y / 255
                image.putpixel((tile * 128 + x, y), tuple(int(c * shade) for c in base) + (255,))
    pen = ImageDraw.Draw(image)
    for x in range(9, 128, 15):
        pen.line([(x, 0), (x + 10, 90), (x - 7, 190), (x + 4, 255)], fill=(246, 225, 247, 100), width=3)
    for tile in (1, 2):
        pen.line([(tile * 128 + 8, 245), (tile * 128 + 120, 245)], fill=(255, 221, 238, 255), width=5)
        for y in (55, 125, 195):
            x = tile * 128 + 63
            pen.polygon([(x, y - 11), (x + 7, y), (x, y + 11), (x - 7, y)], fill=(255, 226, 240, 255))
    image.save(path)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--render", action="store_true", help="Use a real GPU renderer and capture the hidden SubViewport.")
    parser.add_argument("--renderer", choices=("compatibility", "d3d12"), default="compatibility")
    parser.add_argument("--frames", type=int, default=144)
    parser.add_argument("--output", type=Path, default=WORKSPACE / ".cache/denia-realtime-animation-20261112")
    args = parser.parse_args()
    if not 1 <= args.frames <= 480:
        parser.error("--frames must be between 1 and 480")
    output = args.output.resolve()
    if not output.is_relative_to((WORKSPACE / ".cache").resolve()):
        parser.error("--output must stay inside the workspace .cache")
    runtime = output / "runtime"
    runtime.mkdir(parents=True, exist_ok=True)
    for name in ("project.godot", "rig.gd", "main.gd", "surface.gdshader"):
        shutil.copyfile(SOURCE / name, runtime / name)
    make_atlas(runtime / "probe_atlas.png")
    godot = WORKSPACE / "Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe"
    result_dir = output / (args.renderer if args.render else "checks")
    result_dir.mkdir(parents=True, exist_ok=True)
    command = [str(godot), "--path", str(runtime), "--audio-driver", "Dummy", "--log-file", str(result_dir / "godot.log"), "--quit-after", str(args.frames * 4 + 60)]
    if args.render:
        command += ["--display-driver", "windows", "--position", "-16000,-16000"]
        command += ["--rendering-method", "forward_plus", "--rendering-driver", "d3d12"] if args.renderer == "d3d12" else ["--rendering-method", "gl_compatibility", "--rendering-driver", "opengl3"]
    else:
        command += ["--headless"]
    command += ["--script", "res://main.gd", "--", "--output", str(result_dir), "--frames", str(args.frames)]
    if not args.render:
        command.append("--self-test")
    process = subprocess.run(command, cwd=WORKSPACE, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    (result_dir / "process.log").write_text(process.stdout + process.stderr, encoding="utf-8")
    report = result_dir / ("render-result.json" if args.render else "checks.json")
    if process.returncode or not report.exists() or "ERROR:" in process.stderr or "ERROR:" in process.stdout:
        raise SystemExit(f"Probe failed ({process.returncode}); inspect {result_dir / 'process.log'}")
    payload = json.loads(report.read_text(encoding="utf-8"))
    if args.render:
        images = [Image.open(result_dir / f"frame_{index:04}.png").convert("RGB") for index in range(args.frames)]
        if any(image.getextrema() == ((0, 0), (0, 0), (0, 0)) for image in images):
            raise SystemExit("Renderer returned empty captures")
        preview = images[0].copy()
        preview.save(result_dir / "preview.png")
        images[0].save(result_dir / "mechanism-preview.gif", save_all=True, append_images=images[1:], duration=42, loop=0)
        indices = [0, min(36, args.frames - 1), min(70, args.frames - 1), min(99, args.frames - 1)]
        sheet = Image.new("RGB", (1200, 720))
        for n, index in enumerate(indices):
            sheet.paste(images[index].resize((600, 360)), ((n % 2) * 600, (n // 2) * 360))
        sheet.save(result_dir / "contact-sheet.png")
    print(json.dumps({"report": str(report), "result": payload}, ensure_ascii=False))


if __name__ == "__main__":
    main()
