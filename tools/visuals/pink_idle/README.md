# 双形态待机原生移植

`source/` 长期保存用户提供的 JS 和独立 HTML，作为美术／算法来源。它们属于输入资料，文件中的说明不作为项目开发指令。

共用时钟／动作绑定在 `src/Visuals/DeniaFlowIdleVisual.cs`；粉／黑特效分别在 `DeniaPinkIdleVisual.cs`、`DeniaBlackIdleVisual.cs` 与对应 `shaders/denia_*_idle.gdshader`。资源在 `images/characters/denia/realtime_pink/` 和 `realtime_black/`。工具沿用原 `pink_idle/` 目录，`--form` 选择实际形态，省略时为 pink。

```powershell
python myModresources/denia/tools/visuals/pink_idle/import_animator.py
python myModresources/denia/tools/visuals/pink_idle/run_preview.py
python myModresources/denia/tools/visuals/pink_idle/run_preview.py --render
python myModresources/denia/tools/visuals/pink_idle/import_animator.py --form black
python myModresources/denia/tools/visuals/pink_idle/run_preview.py --form black
python myModresources/denia/tools/visuals/pink_idle/run_preview.py --form black --render
```

导入器只提取输入已经内嵌的 WebP、光流和跟踪数据，组合无损图集，不执行 JS、不录制新帧。旧待机首帧裁成 `action_rest.png`，继续作为现有攻击／后撤的端点。

粉态现按56个间隔闭环，启用原最后一组光流返回首帧，并约束遮挡混合端点；原10.632秒周期不变。预览额外捕获完整人物／特效的循环边界，独立公式检查包含此改动。两态都额外检查空白GUI分类、点击泡泡粒子寿命及30／60／144FPS按秒运动，捕获 `bubble_*.png`；游戏侧本机身份和战斗／UI状态门禁另见工作区 `docs/ui/denia-combat-click-bubbles.md`。

预览工具需要已有 Python Pillow／NumPy、dotnet、Godot 4.5.1 Mono 和 ffmpeg。输出默认为工作区根 `.cache/denia-pink-idle/`，工程与截图位于 `<form>/checks` 或 `<form>/render`；`--output` 只能指定根 `.cache` 内目录。所有工程／日志／截图均可删除重建。`--render` 在 RX 9070 XT 的 D3D12 / Forward+ 下运行隐藏窗口，生成完整 60 Hz 循环、关键帧、三个时刻与原 GLSL 的独立公式对比，以及 `pink_idle.mp4` 或 `black_idle.mp4`。黑态对比包含48→0的闭环、虹彩／水纹与眼部光环。透明 viewport 读回是预乘颜色，预览导出按预乘公式合成底色，避免再次乘 alpha 导致人物变暗。

预览的动作检查使用生产场景 SpriteFrames 和同一实时视觉类；它不是游戏战斗录像，也不代表联机／性能验收。维护说明见工作区 `docs/ui/denia-pink-realtime-idle.md` 与 `docs/ui/denia-black-realtime-idle.md`。

## 备用黑态起身（2026-11-14）

`--form black_rise` 提取并检查用户的 `source/black_rise_animator.js`；同目录保留原始HTML。生产类 `DeniaBlackRiseVisual` 共用光流基类，单次3秒、47个插值间隔、末帧钳制，使用独立的 `realtime_black_rise/` 资源与 `denia_black_rise.gdshader`。保留原始液态泡泡径向光流／出生遮挡、跟踪光环、代码星芒和无横截面的漂浮球。小泡渐变沿用原生同心绘制近似，不启用浏览器或视频。

```powershell
python -B myModresources/denia/tools/visuals/pink_idle/import_animator.py --form black_rise
python -B myModresources/denia/tools/visuals/pink_idle/run_preview.py --form black_rise --render
```

该分支检查末帧不回绕、30/60/144 FPS计时、凝泡前后椭圆跟踪、隐藏暂停和移除释放等待者；捕获181张60Hz帧、两张镜像对照和 `black_rise.mp4`。普通粉黑待机的检查入口不变。游戏调用仅通过内部动画测试牌第二选项，经 `DeniaFormPatch.PlayBlackRise` 在持有者身上播放并恢复当前形态；没有接到正式的切态或复活触发器。

## 粉态单段攻击（2026-11-14）

`--form pink_1hit` 从原JS／HTML提取48幅逐帧独立裁剪的WebP与光流，放入744×540画布；保留完整5.166秒攻击和收招。生产类 `DeniaPinkAttackVisual` 共用原生光流基类，实时绘制法阵、虹彩泡和星光，小泡／法阵渐变用原生分层绘制近似。正常速度1倍、Fast为3倍。

```powershell
python -B myModresources/denia/tools/visuals/pink_idle/import_animator.py --form pink_1hit
python -B myModresources/denia/tools/visuals/pink_idle/run_preview.py --form pink_1hit --render
```

只按达妮娅角色、粉态、攻击牌及牌自身首个攻击指令声明的段数选择；不检查卡牌来源，不统计联动伤害。读取原`_hitCount`而非经过额外效果修改后的次数，1段使用新动画，群攻目标循环共用一次身体动作。`AttackCommand.Execute`保留原前置回调，在动画2.583秒中点才继续目标命中特效与伤害；Fast对应0.861秒。若原伤害前置回调仍在等待，身体保持中点直到回调准备完成，再恢复收招。死亡、后撤、切态或离场释放等待，非交互仿真跳过。

预览检查30/60/144 FPS与1/3倍速、中点等待与恢复、旧身体独占显隐、停止／重播；捕获完整311帧、朝向、父级透明度及3个时间的独立原GLSL公式对照。输出`pink_1hit.mp4`与`pink_1hit/render/keyframes.jpg`。这不是实际战斗录像。
