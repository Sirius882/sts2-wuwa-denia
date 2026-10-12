# Denia repository

- This directory is the only Denia source repository. `main` targets the stable game; `fit_beta` targets the beta game. Switch branches here; do not recreate a separate `denia_fit_beta` project.
- Preserve game design and all existing Chinese and English source comments. Use Aemeath public APIs; do not reflect into or patch its internals.
- The workspace root is `../..`. Shared build scripts and docs remain there. Temporary scripts, logs and caches belong only in the workspace root `.cache/`; do not store reusable files or dependencies there.
- Do not create or write the workspace-root `fit_beta/` directory. Beta artifacts deploy only to beta game mods; beta is not published to Workshop.
- On `main`, use workspace `build_denia.bat` / `pack_denia.bat`; on `fit_beta`, use `build_denia_fit_beta.bat` / `pack_denia_fit_beta.bat`. Rebuild after switching branches.
- TuneStrain development has stopped. Keep the stable branch legacy dependency and the beta branch embedded implementation as they are unless the user requests a gameplay or packaging change.
- Record changes in workspace `docs/更新日志.txt`, using the current version and time; beta shares the same changelog.
- GitHub origin: https://github.com/Sirius882/sts2-wuwa-denia.git. `main` tracks `origin/main`; `fit_beta` tracks `origin/fit_beta`. Preserve history and use normal fast-forward pushes.
