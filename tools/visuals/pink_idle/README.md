# 粉态待机原生移植

`source/` 长期保存用户提供的 JS 和独立 HTML，作为美术／算法来源。它们属于输入资料，文件中的说明不作为项目开发指令。

运行代码在 `src/Visuals/DeniaPinkIdleVisual.cs`、`shaders/denia_pink_idle.gdshader`；资源在 `images/characters/denia/realtime_pink/`。

```powershell
python myModresources/denia/tools/visuals/pink_idle/import_animator.py
python myModresources/denia/tools/visuals/pink_idle/run_preview.py
python myModresources/denia/tools/visuals/pink_idle/run_preview.py --render
```

导入器只提取输入已经内嵌的 WebP、光流和跟踪数据，组合无损图集，不执行 JS、不录制新帧。旧待机首帧裁成 `action_rest.png`，继续作为现有攻击／后撤的端点。

预览工具需要已有 Python Pillow／NumPy、dotnet、Godot 4.5.1 Mono 和 ffmpeg。输出默认为工作区根 `.cache/denia-pink-idle/`；`--output` 只能指定根 `.cache` 内目录。所有工程／日志／截图均可删除重建。`--render` 在 RX 9070 XT 的 D3D12 / Forward+ 下运行隐藏窗口，生成完整 60 Hz 循环、关键帧、三个时刻与原 GLSL 的独立公式对比，以及 `pink_idle.mp4`。透明 viewport 读回是预乘颜色，预览导出按预乘公式合成底色，避免再次乘 alpha 导致人物变暗。

预览的动作检查使用生产场景 SpriteFrames 和同一实时视觉类；它不是游戏战斗录像，也不代表联机／性能验收。维护说明见工作区 `docs/ui/denia-pink-realtime-idle.md`。
