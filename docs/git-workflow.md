# Git repositories and branches

The workspace root is not a Git repository. This directory is the Denia repository. Baldurs Enemys has its own repository at `../baldurs_enemys`; TuneStrain is retained as a discontinued legacy dependency.

Both Denia branches use `origin` at https://github.com/Sirius882/sts2-wuwa-denia.git:

| Branch | Game | Build from the workspace root | Pack from the workspace root |
| --- | --- | --- | --- |
| `main` | Stable | `build_denia.bat` | `pack_denia.bat` |
| `fit_beta` | Beta | `build_denia_fit_beta.bat` | `pack_denia_fit_beta.bat` |

```powershell
# Run these commands from E:\STS2_mod.
git -C myModresources/denia switch main
git -C myModresources/denia switch fit_beta

# Commit on the branch being maintained, then push to its upstream.
git -C myModresources/denia status
git -C myModresources/denia add <files>
git -C myModresources/denia commit -m "Describe the change"
git -C myModresources/denia push
```

Commit or stash local changes before switching; do not force checkout. Build scripts check the current branch before compiling or deploying. DLL/PCK outputs and build caches are ignored and may remain from the previous branch, so rebuild after switching and repack when resources change. Source resource metadata (`*.import`, `*.uid`, `export_presets.cfg`) stays tracked.

`main` preserves the existing stable-game TuneStrain DLL reference. `fit_beta` preserves the embedded TuneStrain implementation and beta APIs. This migration does not merge their gameplay differences. Port common fixes between branches deliberately and resolve API differences when necessary.

Beta artifacts deploy only to beta game mods. `upload_denia.py` requires `main`; pushing beta source to GitHub does not publish a Workshop item or GitHub Release. Shared workspace docs and scripts remain at `../..` and are not part of either mod source repository.
