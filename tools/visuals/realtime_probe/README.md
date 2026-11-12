# Denia 实时动画机制预研工具

2026-11-12；对应 `fit_beta` / Denia v4.1.0。本工具不参与 Mod 的内容注册和发布。

目的：验证同一套连续关节/网格计算可以分别驱动无人物贴图的代码绘画、固定图集的实时网格。按用户2026-11-12的明确纠正，明暗表现直接绘制为颜色区域、阴影形状和高光，不进行实时光照求解。示意人物只用于辨认网格运动，不代表 Denia 新美术设计、画质上限或两条路线的质量比较。

## 运行

在工作区根目录运行，需要现有 Godot 4.5.1 Mono、Python 与已安装的 Pillow：

```powershell
python myModresources/denia/tools/visuals/realtime_probe/run_probe.py
python myModresources/denia/tools/visuals/realtime_probe/run_probe.py --render --renderer d3d12
python myModresources/denia/tools/visuals/realtime_probe/run_probe.py --render --renderer compatibility
```

默认输出到工作区根 `.cache/denia-realtime-animation-20261112/`。`--output` 可指定该工作区 `.cache` 下另一目录；`--frames` 为 1–480，默认 144。源码目录的 `.gdignore` 隔离主工程扫描；运行器把可复用源复制到 `.cache` 中的独立 Godot 工程，临时日志、导入结果、纹理、截图和 GIF 均留在那里。清空 `.cache` 后可从这些源完整重跑，无需保留旧输出。

`probe_atlas.png` 是 Python 在启动前生成的一张 512×256 机制示例图集；运行时只上传一次。A 路线不把该图集用作人物纹理，直接以代码指定亮面／暗面颜色；B 路线以固定 UV 使用该图集。两者都实时更新关节和顶点，额头投影直接画成随脸移动的暗色形状，没有人物帧序列，也没有人物光照shader或移动光源。

## 输出与证据边界

- `checks/checks.json`：IK 长度、不可达/零距离目标、网格三角形朝向、暂停/恢复、死亡阻挡攻击/复活、三倍速、30/60/144 FPS 次级运动一致性检查。
- `<renderer>/render-result.json`：实际适配器、渲染器和两具示例总 CPU 更新时间。该计时不包括 GPU、截图、完整游戏及最终模型，不能换算成游戏 FPS 或证明两条路线谁更快。
- `<renderer>/frame_*.png`、`mechanism-preview.gif`、`contact-sheet.png`：真实 GPU 输出。GIF 是离线预研证据，运行机制不播放 GIF。
- `--headless` 只用于数值和 API 检查。GPU 验证启动位于屏幕外且不抢焦点的窗口，并截取独立 SubViewport；不能把 Dummy 渲染器当成真实画面验证。

这是独立 GDScript 原型。生产接入规划使用现有 C# 工程与 RitsuLib 公共接口；当前工具没有安装 RitsuLib、没有测试真实游戏 C# 注册、联机、存档或最终美术性能。

完整研究见工作区 `docs/ui/denia-realtime-animation-research.md`。
