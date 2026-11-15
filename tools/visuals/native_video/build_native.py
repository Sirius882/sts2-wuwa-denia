"""Build the tiny Windows system Media Foundation bridge. Requires MSVC/Windows SDK."""
from pathlib import Path
import hashlib, json, os, subprocess, tempfile
HERE = Path(__file__).resolve().parent
MOD = HERE.parents[2]
ROOT = MOD.parents[1]
OUTPUT = MOD / 'native/denia_video.bin'

def main():
    OUTPUT.parent.mkdir(exist_ok=True)
    vswhere = Path(os.environ['ProgramFiles(x86)']) / 'Microsoft Visual Studio/Installer/vswhere.exe'
    install = subprocess.check_output([str(vswhere), '-latest', '-products', '*', '-requires',
        'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-property', 'installationPath'], text=True).strip()
    if not install: raise RuntimeError('MSVC C++ build tools are required')
    vcvars = Path(install) / 'VC/Auxiliary/Build/vcvars64.bat'
    (ROOT / '.cache').mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='denia-video-build-', dir=ROOT / '.cache') as work:
        work = Path(work)
        command = work / 'build.cmd'
        command.write_text(f'@echo off\ncall "{vcvars}" >nul\nif errorlevel 1 exit /b 1\ncl /nologo /std:c++17 /O2 /EHsc /MT /LD "{HERE / "denia_video.cpp"}" /link /OUT:"{OUTPUT}" mfplat.lib mfreadwrite.lib mfuuid.lib d3d11.lib ole32.lib propsys.lib\n', encoding='utf-8')
        subprocess.run(['cmd', '/c', str(command)], cwd=work, check=True)
    manifest = {'source_sha256': hashlib.sha256((HERE / 'denia_video.cpp').read_text(encoding='utf-8').encode('utf-8')).hexdigest(),
        'binary_sha256': hashlib.sha256(OUTPUT.read_bytes()).hexdigest(), 'binary_bytes': OUTPUT.stat().st_size,
        'platform': 'Windows x86_64', 'backend': 'System Media Foundation / D3D11 NV12',
        'runtime': 'MSVC static CRT; system DLLs only'}
    (HERE / 'native_video.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'{OUTPUT.name}: {OUTPUT.stat().st_size} bytes, SHA256 {hashlib.sha256(OUTPUT.read_bytes()).hexdigest()}')

if __name__ == '__main__': main()
