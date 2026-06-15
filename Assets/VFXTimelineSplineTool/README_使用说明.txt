VFX Timeline Spline Tool v2.7.0 使用说明

核心模块：
1. VFX Simple Spline Path：编辑 3D Catmull-Rom 路径，支持进度刻度、方向箭头、形状预设、自定义路径预设、动态起终点绑定。
2. VFXSplineAnimator：让物体沿路径运动，可用 Animation Track 给 Progress 打关键帧，也可使用 Timeline 快速模式。
3. VFXSplineAnchor：路径上的特效挂点，粒子、面片、爆点可以作为子物体跟随路径位置。

v2.7.0 包含：Bake To AnimationClip
- 在 VFXSplineAnimator Inspector 中找到 Bake To AnimationClip / 烘焙。
- 设置 Frame Rate、Duration、是否烘焙 Position / Rotation、保存目录和 Clip 名称。
- 点击 Bake To AnimationClip 后，会生成普通 Unity AnimationClip。
- 第一版烘焙按 Duration 将 Progress 0→1 采样成 Transform 曲线。
- Bake Progress Source 可选择 Linear 0→1 / Bake Progress Curve / Current Animator Progress / Existing AnimationClip Progress Curve。
- 已经精细 K 了 Progress 时，建议选择 Existing AnimationClip Progress Curve 并拖入你的 Progress 动画片段；不要再叠加 Bake Progress Curve。

推荐烘焙流程：
1. 创建并调好 VFX Simple Spline Path。
2. 给运动物体添加 VFXSplineAnimator，并指定 Spline。
3. 确认 Follow Rotation / Use Distance Based Progress / Dynamic Start-End 等效果正确。
4. 在 Bake 区设置 Duration 和 Frame Rate。
5. 点击 Bake To AnimationClip。
6. 生成的 .anim 可放到 Animation Track 中作为普通 Transform 动画使用。

注意：
- 烘焙会输出 localPosition / localRotation 曲线，因此请尽量在最终层级关系确定后再烘焙。
- 如果使用 Dynamic Start / End Binding，烘焙会采样当前动态端点位置得到最终动画。
- 烘焙后动画可以脱离 Spline 脚本播放，但后续修改 Spline 不会自动更新已烘焙的 AnimationClip，需要重新烘焙。


========================
 v2.7.0 Bake Simplify / 烘焙关键帧简化
========================
在 VFXSplineAnimator 的 Bake To AnimationClip 面板中新增：

1. Keyframe Step
   1 = 每帧记录；2 = 每 2 帧记录一次；5 = 每 5 帧记录一次。
   数值越大，生成的关键帧越少，但路径细节越少。

2. Always Key Start And End
   开启后始终保留第一帧和最后一帧，避免动画头尾丢失。

3. Optimize Curves
   开启后会根据 Position Tolerance / Rotation Tolerance 自动删除误差很小的中间关键帧。

推荐参数：
- 普通飞行：Keyframe Step = 3 或 5
- 快速转弯：Keyframe Step = 2 或 3
- 简单直线/弧线：Keyframe Step = 5 或 10
- 需要最高精度：Keyframe Step = 1


【v2.6.3 新增：从 Timeline 自动读取 Progress 曲线烘焙】
1. 在物体的 Timeline Animation Track 里正常 K VFXSplineAnimator / Progress。
2. 选中该物体，在 VFXSplineAnimator 的 Bake To AnimationClip 区域中设置：
   Bake Progress Source = Timeline Bound Animation Track。
3. Playable Director 可以留空，工具会自动在场景中查找绑定当前物体的 Timeline；也可以手动拖入对应 PlayableDirector。
4. 勾选 Use Timeline Clip Duration 后，烘焙时长会自动使用 Timeline Clip 的长度。
5. 点击 Bake To AnimationClip，即可按 Timeline 中已经 K 好的 Progress 曲线烘焙 Transform 动画。

如果自动识别失败，可以改用 Manual AnimationClip / Existing AnimationClip Progress Curve 模式，手动指定 Progress 动画片段。
