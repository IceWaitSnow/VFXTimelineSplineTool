# VFX Timeline Spline Tool 使用指南

版本：v2.7.0

这是一套给 Unity Timeline 和特效流程使用的路径运动工具。它的目标不是替代 Unity 的动画系统，而是把“沿路径运动、路径挂点、粒子/面片/爆点跟随、最终烘焙成 AnimationClip”这些经常重复的工作整理成一套更顺手的流程。

适合用在这些场景：

- 奖励、道具、飞弹、能量球沿曲线飞行
- Timeline 里快速控制物体沿路径运动
- 粒子、面片、爆点固定到路径上的某个 Progress 位置
- 循环路径运动，比如环绕、巡游、圆形轨迹
- 把 Spline 驱动结果烘焙成普通 Unity AnimationClip，方便交付和复用

【截图：一个物体沿 Spline 路径运动的 Scene 视图】

---

## 1. 核心概念

这个工具主要由三个组件组成。

### VFX Simple Spline

路径本体。它负责保存和绘制路径，也负责计算某个 Progress 对应的空间位置。

你可以把它理解成“轨道”。

它支持：

- Catmull-Rom 路径
- Bezier 路径
- Loop 闭合路径
- Scene 视图直接编辑控制点
- 形状预设
- 自定义路径预设
- 路径健康检查
- Anchor 快速创建

### VFX Spline Animator

路径运动组件。它把某个物体绑定到一条 Spline 上，并根据 Progress 驱动物体移动和旋转。

你可以把它理解成“沿轨道运动的控制器”。

常用字段：

- Spline：要跟随的路径
- Progress：路径进度，0 到 1
- Use Distance Based Progress：按实际距离等速运动
- Follow Rotation：是否跟随路径方向旋转
- Rotation Mode / Forward Axis：旋转方向设置

### VFX Spline Anchor

路径挂点。它可以把一个空物体固定在路径上的某个位置，粒子、面片、爆点等对象可以放到 Anchor 下面。

你可以把它理解成“路径上的挂载点”。

Anchor 支持两种常用模式：

- Fixed Progress：固定在路径某个 Progress
- Follow Animator Progress：跟随某个 Animator 当前进度，并可加 Progress Offset

【截图：Spline、Animator、Anchor 三个 Inspector 对比】

---

## 2. 最快上手流程

### 第一步：创建路径

在 Unity 菜单中选择：

`GameObject > 特效工具 > VFX Simple Spline Path`

场景里会创建一个 `VFX Simple Spline Path` 对象。

选中它后，Scene 视图里会显示路径和控制点。你可以直接拖动控制点调整路径形状。

【截图：刚创建出来的 VFX Simple Spline Path】

### 第二步：让物体沿路径运动

给需要运动的物体添加：

`VFX Spline Animator`

然后把刚才创建的 `VFX Simple Spline Path` 拖到 Animator 的 `Spline` 字段里。

调整 `Progress`，物体就会沿路径移动：

- `Progress = 0`：路径起点
- `Progress = 0.5`：路径中间
- `Progress = 1`：路径终点

如果希望运动速度按路径长度均匀，建议开启：

`Use Distance Based Progress`

### 第三步：放进 Timeline

你有两种方式控制 Progress。

第一种是常规方式：  
在 Unity 原生 Animation Track 里给 `VFXSplineAnimator.progress` 打关键帧。

第二种是快速方式：  
使用 `VFX Spline Animation Track / Clip`，直接在 Timeline Clip 里设置 Start Progress、End Progress、速度曲线等参数。

如果只是快速做一段沿路径运动，第二种方式更省事；如果要精细控制节奏，第一种方式更自由。

【截图：Timeline 里控制 Progress 的动画片段】

---

## 3. Scene 视图编辑路径

选中 Spline 后，可以直接在 Scene 视图里编辑控制点。

### 状态提示条

Scene 左上角会显示当前状态：

- 当前模式
- Catmull-Rom / Bezier
- Loop On / Off
- 当前选中点或多选点数量
- 路径健康摘要
- 常用快捷键

【截图：Scene 左上角状态提示条】

### 常用快捷键

- `A`：进入 / 退出追加点模式
- `M`：打开点菜单，或在线段中间插入点
- `Ctrl / Cmd + 点击`：多选控制点
- `Ctrl / Cmd + 拖拽空白处`：框选控制点
- `F`：聚焦当前控制点
- `Delete / Backspace`：删除当前控制点
- `Esc`：退出追加点模式

快捷键可以在 Unity 内置快捷键面板中修改：

`Edit > Shortcuts...`

搜索：

`VFX Timeline Spline`

### 追加点

按 `A` 进入追加点模式后，在 Scene 空白处左键点击即可不断追加控制点。

再次按 `A` 或 `Esc` 退出追加点模式。

### 插入点

把鼠标移动到路径线段附近，按 `M`，工具会在线段中间插入一个控制点。

把鼠标移动到某个控制点附近，按 `M`，会打开这个点的右键菜单。

### 多选控制点

多选后可以批量移动，也可以在菜单里执行批量操作，例如：

- 对齐 X / Y / Z
- 归零 X / Y / Z
- 批量删除
- 批量修改 Bezier 点类型
- 批量修改 Bezier 手柄模式

【截图：多选控制点后的菜单】

---

## 4. Catmull-Rom 和 Bezier 怎么选

### Catmull-Rom

Catmull-Rom 更适合快速拉路径。你只需要摆控制点，曲线会自动穿过这些点。

适合：

- 快速草拟路径
- 飞行轨迹
- 不需要精细手柄控制的曲线

缺点：

- 没有 Bezier 手柄
- 拐角不如 Bezier 精准

### Bezier

Bezier 更适合精细控制。每个点都有 in / out tangent，可以调整曲柄。

适合：

- 需要精确弧度的路径
- 需要硬拐角和软曲线混合的路径
- 需要美术细调的路径

Bezier 点支持几种类型：

- Corner：拐角
- Smooth：平滑
- Symmetric：对称
- Auto Smooth：自动平滑

### 路径模式切换

你可以在 Inspector 里切换路径模式，也可以在 Scene 右键菜单中切换。

从 Catmull-Rom 转到 Bezier 后，可以继续用 Bezier 手柄精调路径。

---

## 5. Loop 闭合路径

如果希望路径首尾相连，需要开启：

`Loop 闭合路径`

开启后，最后一个控制点会直接连接回第一个控制点，`Progress = 1` 会回到 `Progress = 0`。

这对循环运动很重要。

比如 Circle、Triangle、Square 这类看起来是闭合的形状，如果没有真正开启 Loop，运动到结尾时可能会出现卡顿或跳变。

工具里的闭合形状预设会尽量使用 Loop，而不是额外创建一个重复的终点。

【截图：Loop Off 和 Loop On 的对比】

---

## 6. 形状预设和自定义预设

### 形状预设

Inspector 里提供了一组常用 Shape Presets，例如：

- Line
- Arc
- S Curve
- Wave
- Circle
- Ellipse
- Square
- Rectangle
- Triangle
- Diamond
- Infinity

点击 `应用形状预设` 会覆盖当前路径点。

如果开启 `实时预览形状预设`，修改参数时会实时刷新路径。建议在正式路径上使用前先复制一份，避免误覆盖。

### 自定义路径预设

调好路径后，可以保存为 `.asset` 资源，方便下次复用，也方便提交到 SVN / Git 给团队共享。

适合保存：

- 常用飞行轨迹
- 奖励飞入路径
- 圆环/螺旋/波浪路径
- 项目内统一风格的运动曲线

---

## 7. 路径健康检查

Inspector 里有一个 `路径健康检查` 区块，用来提示路径里可能存在的问题。

它会检查：

- 相邻控制点重叠
- 相邻控制点距离过近
- Bezier 手柄过长
- 首尾控制点很近但没有开启 Loop
- Loop 首尾切线变化过大

常用按钮：

- `选中第一个问题点`：直接定位到第一个问题点
- `删除重叠控制点`
- `按距离均匀重排`
- `收紧过长 Bezier 手柄`
- `开启 Loop`

这个功能不会自动修改你的路径。它只会提醒你哪里可能有问题，你可以决定要不要修。

【截图：路径健康检查区】

---

## 8. 使用 Anchor 做路径挂点

Anchor 适合处理“这个特效要在路径上的某个点出现”的需求。

常见用法：

1. 选中 `VFX Simple Spline Path`
2. 在 `特效挂点工具 Anchor Tools` 中点击创建 0%、25%、50%、75%、100% Anchor
3. 把粒子、面片、爆点对象放到 Anchor 下面
4. 用 Timeline 原生 Control Track / Activation Track 控制这些子物体播放

这样路径负责位置，Timeline 负责播放，结构会比较清楚。

### Follow Animator Progress

如果 Anchor 选择 `Follow Animator Progress`，它可以跟随某个 `VFXSplineAnimator` 的当前进度。

再配合 `Progress Offset`，就可以做：

- 跟随运动物体前方一点的粒子
- 落后运动物体一点的拖尾挂点
- 沿路径偏移出现的爆点

【截图：Anchor Follow Animator Progress 设置】

---

## 9. 烘焙为 AnimationClip

当路径运动调好后，可以把结果烘焙成普通 Unity `AnimationClip`。

烘焙适合这些情况：

- 最终交付不希望依赖 Spline 运行逻辑
- 想把路径运动变成普通 Transform 动画
- 想给 Timeline / Animator Controller 复用
- 想减少运行时计算

### 在 VFXSplineAnimator 上烘焙

选中带有 `VFXSplineAnimator` 的物体，在 Inspector 中找到：

`Bake To AnimationClip / 烘焙`

常用设置：

- Frame Rate：帧率，特效一般建议 60
- Duration：烘焙时长
- Bake Position：是否烘焙位置
- Bake Rotation：是否烘焙旋转
- Bake Progress Source：Progress 来源
- Save Folder：保存目录
- Clip Name：输出文件名

点击：

`烘焙为 AnimationClip`

工具会生成一个普通 `.anim` 文件。

### Bake Progress Source 怎么选

`Linear 0→1`  
适合最简单的从起点到终点匀速运动。

`Bake Progress Curve`  
适合快速做缓入、缓出、停顿、回弹。

`Existing AnimationClip Progress Curve`  
适合你已经用 AnimationClip 精细 K 好 `VFXSplineAnimator.progress` 后，再把结果烘焙成 Transform 动画。

`Timeline Bound Animation Track`  
适合从 Timeline 上自动读取绑定当前物体的 Animation Track，并使用其中的 Progress 曲线。

### 自动使用 Timeline Clip 时长

如果你使用 Timeline Clip 快速模式，可以开启自动使用 Clip 时长。工具会识别当前 Timeline Clip 的长度，并作为 Bake Duration。

这样不需要手动输入 4.8 秒、3.5 秒之类的时长，减少出错。

### 关键帧简化

烘焙时可以减少关键帧数量，避免每一帧都写入曲线。

常用参数：

- Keyframe Step：每隔几帧记录一个关键帧
- Optimize Curves：根据误差阈值删除变化很小的中间帧
- Always Key Start And End：始终保留首尾帧

建议：

- 复杂曲线：Keyframe Step = 1 或 2
- 普通飞行：Keyframe Step = 3 或 5
- 简单直线/大弧线：Keyframe Step = 5 或 10

烘焙后请在 Timeline 或 Animation 窗口里预览一次，确认运动没有丢细节。

【截图：Bake To AnimationClip 面板】

---

## 10. Timeline Clip 快速模式

`VFX Spline Animation Clip` 是 Timeline 里的快速模式。

它适合快速做一段沿路径运动，不一定要单独 K Progress。

常用参数：

- Spline 路径
- Start Progress
- End Progress
- Speed Curve
- Reverse
- Loop Playback
- Use Distance Based Progress
- Position Offset
- Follow Rotation
- Rotation Mode
- Forward Axis

如果开启 `Loop Playback`，Clip 可以拉长为多轮循环，运动速度由 `Seconds Per Loop` 控制，而不是由 Clip 总长度控制。

这适合做循环飞行、环绕运动、巡游路径。

---

## 11. 常见问题

### 为什么 Circle 看起来是圆，但运动到首尾会卡一下？

通常是因为路径看起来闭合，但没有真正开启 Loop。

解决方法：

- 开启 `Loop 闭合路径`
- 或使用健康检查里的 `开启 Loop`

### 为什么有些三角形、方形会变成软边？

如果路径是 Bezier，并且点类型是 Smooth / Auto Smooth，拐角会被自动平滑。

解决方法：

- 把这些点设为 Corner
- 使用形状预设生成 Triangle / Square / Rectangle 时，确认点类型为拐角

### 为什么选不中第一个点？

第一个点可能和 Unity Transform Gizmo 重叠。

可以尝试：

- 调大 `拾取尺寸倍数`
- 开启 `放大第一个点`
- 用 Scene 左上角状态条确认当前选点
- 使用点列表窗口作为备用编辑入口

### Catmull-Rom 为什么没有 Bezier 手柄？

Catmull-Rom 本身就是通过控制点自动生成曲线，没有 Bezier 的 in / out tangent。

如果需要手柄精调，请转换为 Bezier。

### 烘焙后修改 Spline，AnimationClip 会自动更新吗？

不会。

烘焙后的 AnimationClip 是普通动画资源，已经和 Spline 解耦。修改 Spline 后需要重新烘焙。

### 已经用 Animation Track 给 Progress 打关键帧了，还要用 Bake Progress Curve 吗？

一般不需要。

如果 Progress 已经精细 K 好，建议使用：

`Existing AnimationClip Progress Curve`

或：

`Timeline Bound Animation Track`

不要再叠加 `Bake Progress Curve`，否则节奏可能被二次改动。

---

## 12. 推荐工作流

### 快速路径运动

1. 创建 `VFX Simple Spline Path`
2. 调整路径控制点
3. 给物体添加 `VFXSplineAnimator`
4. 指定 Spline
5. 在 Timeline 里给 Progress 打关键帧
6. 预览效果
7. 需要交付时烘焙成 AnimationClip

### Timeline 快速模式

1. 创建 Spline
2. 创建 `VFX Spline Animation Track`
3. 添加 `VFX Spline Animation Clip`
4. 设置 Start / End Progress
5. 调整 Speed Curve
6. 需要循环时开启 Loop Playback
7. 最终烘焙成 AnimationClip

### 粒子挂点

1. 创建 Spline
2. 用 Anchor Tools 创建 Anchor
3. 把粒子/面片/爆点放到 Anchor 下
4. 用 Timeline 原生 Track 控制子物体播放
5. 需要跟随运动时使用 Follow Animator Progress

---

## 13. 已知限制

- Catmull-Rom 没有 Bezier 手柄，想精调曲柄需要转换为 Bezier。
- 路径健康检查只是辅助判断，不会自动保证路径一定正确。
- 烘焙是采样结果，帧率、关键帧间隔、曲线简化都会影响最终精度。
- 烘焙后的 AnimationClip 不会随着 Spline 修改自动更新。
- 使用 Dynamic Start / End Binding 时，烘焙会采样当前动态端点状态。

---

## 14. 一句话总结

`VFX Timeline Spline Tool` 的定位是：让特效路径运动从“手动 K 一堆 Transform”变成“先画路径，再控制 Progress，最后按需要烘焙”。

它更适合特效制作中的快速迭代：先在 Scene 里把路径调顺，再交给 Timeline 控节奏，最后输出成普通 AnimationClip。

【截图：最终效果对比，Spline 编辑态 / Timeline 播放态 / 烘焙 AnimationClip】
