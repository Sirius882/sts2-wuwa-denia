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

只按达妮娅角色、粉态、攻击牌及牌自身首个攻击指令声明的段数选择；不检查卡牌来源，不统计联动伤害。读取原`_hitCount`而非经过额外效果修改后的次数，1段使用此动画，2段及以上见下方粉态两次挥击入口，群攻目标循环共用一次身体动作。`AttackCommand.Execute`保留原前置回调，在动画2.083秒时继续目标命中特效与伤害，较原中点提前0.5秒；Fast 3倍速对应约0.694秒。若原伤害前置回调仍在等待，身体保持命中姿态直到回调准备完成，再恢复收招。死亡、后撤、切态或离场释放等待，非交互仿真跳过。

预览检查30/60/144 FPS与1/3倍速、命中时点等待与恢复、旧身体独占显隐、停止／重播；捕获完整311帧、朝向、父级透明度及3个时间的独立原GLSL公式对照。输出`pink_1hit.mp4`与`pink_1hit/render/keyframes.jpg`。这不是实际战斗录像。

## 黑态单段巨爪

`import_animator.py --form dark_1hit`提取原96幅960×480 WebP及120×60光流，不烘焙中间帧。`run_preview.py --form dark_1hit --render --output E:/STS2_mod/.cache/dark-attack-preview`检查并生成完整7.933秒MP4、关键帧和人物对齐图；原HTML／JS保存在`source/dark_1hit_animator.*`。

黑态使用与粉态相同的首个攻击指令声明段数判定，不限卡牌来源，不计算联动段数。共用`DeniaSingleHitVisual`时钟与命中门控，黑态命中设为原第10帧的巨爪峰值（约0.826秒，Fast约0.275秒），粉态继续2.083秒。单段期间隐藏当前形态整个Body并关闭旧黑色挥砍；多段继续旧动作与挥砍。

素材`idle_box.scale=0.4575`直接对应现有592×600黑态待机：人物参考高度536×0.4575=245.22，锚点(358.65,171.6125)+(240,600)×0.4575=(468.45,446.1125)。按此身体标定缩放，960×480画布及巨爪等比保留，允许超出角色Bounds。原GLSL保留运动置信门控、抗重影混合、Bicubic/CAS、暗泡／星眸与巨爪边缘光；CPU绘制原种子泡泡、星芒、冲击圈和重凝漩涡，径向渐变仍以原生分层近似。

## 粉态两次挥击与多段伤害

`import_animator.py --form pink_2hit`提取132幅896×504原WebP及224×126四通道双向光流；RGBA通道均为数据，`flow.png.import`关闭透明边缘修复。`run_preview.py --form pink_2hit --render --output E:/STS2_mod/.cache/pink-combo-preview`生成原生完整14.663333秒预览、881帧、原GLSL对照和生产控制器检查，不预渲染运行时中间帧。

粉态牌自身首个攻击指令声明2段及以上时，每次出牌仅启动一次`DeniaPinkMultiAttackVisual`。前两次`BeforeDamage`回调分别等待原第14／20帧（约1.555／2.222秒，Fast约0.518／0.741秒），第三段起不附加动画等待，原回调仍逐段执行。AoE一次回调覆盖同轮全部目标；重复目标指令不重播。提前结束或异常解除未用命中点，后撤／死亡／切态／离场释放全部等待。粉态单段仍2.083秒，黑态多段仍旧动作。

按人物本体227像素及脚底锚点(418,416)缩放，保持宽画布／挥击特效；GPU按原2倍绘制画布1792×1008换算光流位移，并保留运动置信门控、轮廓保护和预乘透明混合。附属42个环境粒子使用独立固定种子与代码绘制，不消耗战斗随机数。素材HTML／JS文字只作为资料。

### 新旧攻击叠播修复（2026-11-14）

粉态不在出牌前启动旧攻击；首次攻击指令按声明段数选择新单段或旧多段动作。旧`Idle`和`RealtimeIdle`置于各形态覆盖层的`Body`父节点内，新单段作为该父节点的同级节点，播放时隐藏整个`Body`。旧动画结束信号、延迟动作切换或直接Show子节点都无法让旧身体穿透显示；收招完成先把旧sprite切回idle，再恢复`Body`。Bounds、朝向、后撤和死亡取节点路径一并更新。

`run_preview.py`直接提取当前生产`DeniaFormOverlaySync`类到缓存工程，`ControllerFixture.cs`仅替代游戏接口。测试普通／Fast各311个姿态，主动触发旧动画结束信号和子节点Show，验证旧身体全程不可见、结束只恢复待机，并覆盖后撤、死亡和切形态。D3D12下三个时刻的控制器合成画面与孤立新动画逐像素对照。Fixture从生产csproj排除，不随模组运行。
