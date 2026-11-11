using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>关卡进场动画烘焙：把 CameraInfoDto.intro 落到场景 Camera 子物体的
///  Animator 上（controller 整替，绝不改 common01 原版资产本体）。
///
///  模式：
///  - vanilla：恢复原版 Camera.controller（PullBackIntro→Camera_Idle），并清理
///    本场景烘过的 cameraintro_* 资产；
///  - none：禁用 Animator（开场即最终画面）；
///  - pullBack / rotateLeft / rotateRight / focus / tour：烘焙自定义 clip（Camera
///    子物体 localPosition + localRotation 四元数曲线，首帧=起始位姿、末帧=当前编辑
///    位姿即最终画面，easeInOut 采样）+ 两态 controller（Intro --exit time--> Idle
///    常持），整替 Animator.runtimeAnimatorController —— 原版同款纪律：动画只作用于
///    Camera 子物体局部变换，与 rig 根 60° 俯角、MultiplayerCamera 跟随零争用（原版
///    PullBackIntro / Space roll / FX shake 全是同一套路）；tour = 巡回 A→B→归位
///    （两段折线、段时长按距离比例分配）；focus/tour 均只沿世界 X 平移——与主相机
///    同高度/朝向/Z（画面= 最终画面水平平移，杜绝贴地黑屏；目标 Z 恒取主相机 Z，
///    startDistance 不参与）；
///  - raft / space：切换 common01 官方变体 controller（进场+待机一体：木筏摇曳 /
///    太空 roll 循环，同时改变待机行为）。
///
///  往返：intro 参数 JSON 以 IntroSource TextAsset 子资产嵌入 controller（仿
///  AnimGroupBakery.PersistAuthoringSource）；SceneLayoutExporter.CollectCameraInfo
///  经 Collect() 按 Animator 现状（enabled + controller 名）反推完整配置。资产放
///  GetAnimationsFolder（Assets/LevelSets/&lt;set&gt;/animations/）——与动画组同目录，
///  导出/打包链已验证。分屏模式（SplitScreen|）运行时屏蔽原版镜头，进场动画
///  不可见（前端在选择分屏时禁用配置）。</summary>
public static class CameraIntroBakery
{
    public const string ModeVanilla = "vanilla";
    public const string ModeNone = "none";
    public const string ModePullBack = "pullBack";
    public const string ModeRotateLeft = "rotateLeft";
    public const string ModeRotateRight = "rotateRight";
    public const string ModeFocus = "focus";
    public const string ModeTour = "tour";
    public const string ModeDollyIn = "dollyIn";
    public const string ModeDive = "dive";
    public const string ModeOrbit = "orbit";
    public const string ModeRaft = "raft";
    public const string ModeSpace = "space";

    /** 烘焙资产名前缀（clip/hold/controller 共用；Collect 按 controller 名识别）。 */
    public const string ClipPrefix = "cameraintro_";
    private const string SourceAssetName = "IntroSource";

    private const string VanillaControllerPath = "Assets/common01/animations/Camera.controller";
    private const string RaftControllerPath = "Assets/common01/animations/Raft_Camera.controller";
    private const string SpaceControllerPath = "Assets/common01/animations/Space_Camera.controller";

    /// <summary>写回入口（SceneLayoutApplier.ApplyCameraInfo 调用）。
    ///  返回警告文案（写入失败/资产缺失），null = 成功或无需写入。</summary>
    public static string Apply(Camera cam, CameraIntroDto intro)
    {
        if (cam == null || intro == null || string.IsNullOrEmpty(intro.mode))
            return null;
        var animator = cam.GetComponent<Animator>();
        if (animator == null)
            return "相机上无 Animator，进场动画未写入";
        var scene = SceneManager.GetActiveScene();

        if (intro.mode == ModeNone)
        {
            if (animator.enabled)
            {
                Undo.RecordObject(animator, "Layout Editor Camera Intro");
                animator.enabled = false;
                EditorUtility.SetDirty(animator);
            }
            return null;
        }

        // 其余模式都需要 Animator 工作（none → 其它模式时重新启用）。
        if (!animator.enabled)
        {
            Undo.RecordObject(animator, "Layout Editor Camera Intro");
            animator.enabled = true;
            EditorUtility.SetDirty(animator);
        }

        if (intro.mode == ModeVanilla)
        {
            var vanilla = LoadControllerAsset("Camera", VanillaControllerPath);
            if (vanilla == null)
                return "未找到原版 Camera.controller，进场动画未恢复";
            if (animator.runtimeAnimatorController != vanilla)
            {
                Undo.RecordObject(animator, "Layout Editor Camera Intro");
                animator.runtimeAnimatorController = vanilla;
                EditorUtility.SetDirty(animator);
            }
            CleanBakedAssets(scene);
            return null;
        }

        if (intro.mode == ModeRaft || intro.mode == ModeSpace)
        {
            bool raft = intro.mode == ModeRaft;
            var variant = LoadControllerAsset(raft ? "Raft_Camera" : "Space_Camera",
                raft ? RaftControllerPath : SpaceControllerPath);
            if (variant == null)
                return "未找到官方进场变体资产（" + intro.mode + "），进场动画未写入";
            if (animator.runtimeAnimatorController != variant)
            {
                Undo.RecordObject(animator, "Layout Editor Camera Intro");
                animator.runtimeAnimatorController = variant;
                EditorUtility.SetDirty(animator);
            }
            CleanBakedAssets(scene);
            return null;
        }

        if (intro.mode == ModePullBack || intro.mode == ModeRotateLeft
            || intro.mode == ModeRotateRight || intro.mode == ModeFocus
            || intro.mode == ModeTour || intro.mode == ModeDollyIn
            || intro.mode == ModeDive || intro.mode == ModeOrbit)
            return BakeCustom(animator, intro, scene);

        return "未知进场动画模式「" + intro.mode + "」，未写入";
    }

    /// <summary>导出读回（SceneLayoutExporter.CollectCameraInfo 调用）：按 Animator
    ///  现状反推 intro 配置；自定义模式从 controller 内嵌 IntroSource 还原完整参数。</summary>
    public static CameraIntroDto Collect(Camera cam)
    {
        var result = new CameraIntroDto();
        result.mode = ModeVanilla;
        if (cam == null)
            return result;
        var animator = cam.GetComponent<Animator>();
        if (animator == null)
            return result;
        if (!animator.enabled)
        {
            result.mode = ModeNone;
            return result;
        }
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
            return result;
        string assetName = controller.name;
        if (assetName == "Raft_Camera")
        {
            result.mode = ModeRaft;
            return result;
        }
        if (assetName == "Space_Camera")
        {
            result.mode = ModeSpace;
            return result;
        }
        if (!string.IsNullOrEmpty(assetName) && assetName.StartsWith(ClipPrefix, StringComparison.Ordinal))
        {
            result.mode = ModePullBack;
            try
            {
                var path = AssetDatabase.GetAssetPath(controller);
                if (!string.IsNullOrEmpty(path))
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int i = 0; i < assets.Length; i++)
                    {
                        var ta = assets[i] as TextAsset;
                        if (ta == null || ta.name != SourceAssetName)
                            continue;
                        var parsed = JsonUtility.FromJson<CameraIntroDto>(ta.text);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.mode))
                            return parsed;
                    }
                }
            }
            catch (Exception e)
            {
                LayoutEditorLog.LogWarning("camera intro: IntroSource 解析失败（退化为默认）：" + e.Message);
            }
        }
        return result;
    }

    // ------------------------------------------------------------------ custom bake

    /// <summary>自定义模式烘焙：clip（首帧=起始位姿 → 末帧=最终位姿，easeInOut）+
    ///  Idle 常持 clip + 两态 controller（复刻原版 PullBackIntro→Camera_Idle 拓扑）
    ///  + IntroSource 参数嵌入，整替 Animator.controller。</summary>
    private static string BakeCustom(Animator animator, CameraIntroDto intro, Scene scene)
    {
        if (!scene.IsValid())
            return "活动场景无效，进场动画未烘焙";

        var t = animator.transform;
        // 最终位姿 = 当前编辑态局部位姿（ApplyCameraInfo 写回路径已把子物体
        // localPosition 清零；仍以实际值烘焙，末帧精确钉回最终画面）。
        var finalPos = t.localPosition;
        var finalRot = t.localRotation;

        float duration = Mathf.Clamp(intro.duration > 0f ? intro.duration : 2f, 0.3f, 8f);
        float distance = Mathf.Clamp(intro.startDistance >= 0f ? intro.startDistance : 5f, 0f, 20f);
        float angle = Mathf.Clamp(intro.rotateAngle > 0f ? intro.rotateAngle : 30f, 1f, 179f);
        float diveH = Mathf.Clamp(intro.diveHeight > 0f ? intro.diveHeight : 10f, 2f, 40f);
        float orbitAng = Mathf.Clamp(intro.orbitAngle >= 30f ? intro.orbitAngle : 180f, 30f, 360f);
        bool orbitLeft = intro.orbitDirection == "left";

        // 路点序列（首=起始位姿、末=最终位姿；BuildPathClip 按相邻距离比例分配
        // 段时长、每段独立 easeInOut）；旋转 startRot→finalRot 全程 slerp。
        // 局部 +Z = 相机视轴（原版 PullBackIntro 同方向：z 5→0 拉近后滑回）。
        var path = new List<Vector3>();
        var startRot = finalRot;
        var rig = t.parent != null ? t.parent : t;
        var finalWorld = rig.TransformPoint(finalPos);

        if (intro.mode == ModeRotateLeft || intro.mode == ModeRotateRight)
        {
            // rig 根无 yaw（Euler 60,0,0），局部 up 即世界 up：绕竖直轴的起始偏航，
            // 进场过程旋回正位（rotateLeft = 起始朝右偏 +angle、向左旋回）。
            float yaw = intro.mode == ModeRotateLeft ? angle : -angle;
            startRot = Quaternion.AngleAxis(yaw, Vector3.up) * finalRot;
            path.Add(finalPos + Vector3.forward * distance);
        }
        else if (intro.mode == ModeDollyIn)
        {
            // 推近：起始在最终位的远端（沿视轴后退），滑入到位（pullBack 镜像）。
            path.Add(finalPos - Vector3.forward * distance);
        }
        else if (intro.mode == ModeDive)
        {
            // 俯冲：起始更高（世界 +Y）且沿视轴更远 → 俯冲滑入（航拍感）。
            var fwd = t.forward.normalized;
            var startWorld = finalWorld - fwd * Mathf.Max(0.5f, distance) + new Vector3(0f, diveH, 0f);
            path.Add(rig.InverseTransformPoint(startWorld));
        }
        else if (intro.mode == ModeOrbit)
        {
            // 环绕：绕最终位竖直轴的圆弧滑入。offset(θ) = R·(sinθ, 0, cosθ−1)，
            // θ 从 ±orbitAngle 归零 —— 末点精确落最终位；R = startDistance（环绕
            // 半径）；朝向恒为最终俯角（构图连续）。方向：left = 逆时针（−θ）。
            float r = Mathf.Max(1.5f, distance < 2f ? 8f : distance);
            float th0 = orbitLeft ? -orbitAng : orbitAng;
            const int arcSteps = 32;
            for (int i = 0; i < arcSteps; i++)
            {
                float th = Mathf.Deg2Rad * (th0 * (1f - (float)i / arcSteps));
                var off = new Vector3(Mathf.Sin(th) * r, 0f, (Mathf.Cos(th) - 1f) * r);
                path.Add(rig.InverseTransformPoint(finalWorld + off));
            }
        }
        else if (intro.mode == ModeFocus || intro.mode == ModeTour)
        {
            // focus / tour：与主相机同高度、同朝向，水平平移（画面 = 最终画面的
            // 平移，构图恒与主相机一致——贴地/低空特写会露出背景黑屏，此方案
            // 杜绝）。startDistance 参数不参与；X/Z 均不钳制（大地图可巡到远处，
            // 前端超画面范围仅提示不阻止）。focus 沿 XZ 全向平移（选点 X/Z 均生效）；
            // tour 仅沿 X 平移（Z 恒取主相机 Z，选点 Z 忽略）。
            if (intro.mode == ModeFocus)
            {
                float dx = (intro.focusTarget != null ? intro.focusTarget.x : 0f) - finalWorld.x;
                float dz = (intro.focusTarget != null ? intro.focusTarget.z : 0f) - finalWorld.z;
                path.Add(rig.InverseTransformPoint(finalWorld + new Vector3(dx, 0f, dz)));
            }
            else
            {
                float dxA = (intro.tourA != null ? intro.tourA.x : 0f) - finalWorld.x;
                float dxB = (intro.tourB != null ? intro.tourB.x : 0f) - finalWorld.x;
                path.Add(rig.InverseTransformPoint(finalWorld + new Vector3(dxA, 0f, 0f)));
                path.Add(rig.InverseTransformPoint(finalWorld + new Vector3(dxB, 0f, 0f)));
            }
        }
        else
        {
            // pullBack（及默认）：起始沿视轴贴近厨房，滑回全景。
            path.Add(finalPos + Vector3.forward * distance);
        }
        path.Add(finalPos);

        var animDir = AnimGroupBakery.GetAnimationsFolder(scene.path);
        AnimGroupBakery.EnsureFolder(animDir);
        var baseName = ClipPrefix + scene.name;
        var clipPath = animDir + "/" + baseName + ".anim";
        var holdPath = animDir + "/" + baseName + "_idle.anim";
        var controllerPath = animDir + "/" + baseName + ".controller";
        AnimGroupBakery.DeleteAssetIfExists(controllerPath);
        AnimGroupBakery.DeleteAssetIfExists(clipPath);
        AnimGroupBakery.DeleteAssetIfExists(holdPath);

        AnimationClip clip = BuildPathClip(baseName, path, startRot, finalRot, duration,
            intro.mode == ModeOrbit);
        AssetDatabase.CreateAsset(clip, clipPath);
        var hold = BuildHoldClip(baseName + "_idle", finalPos, finalRot);
        AssetDatabase.CreateAsset(hold, holdPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller == null)
            return "进场动画 controller 创建失败（" + controllerPath + "）";
        controller.name = baseName;
        var sm = controller.layers[0].stateMachine;
        var introState = sm.AddState("PullBackIntro");
        introState.motion = clip;
        introState.writeDefaultValues = false;
        var idleState = sm.AddState("Camera_Idle");
        idleState.motion = hold;
        idleState.writeDefaultValues = false;
        sm.defaultState = introState;
        var exit = introState.AddTransition(idleState);
        exit.hasExitTime = true;
        exit.exitTime = 1f;
        exit.duration = 0f;
        exit.hasFixedDuration = true;

        EmbedAuthoringSource(controllerPath, intro);

        Undo.RecordObject(animator, "Layout Editor Camera Intro");
        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animator);
        AssetDatabase.SaveAssets();
        LayoutEditorLog.Log("camera intro: 烘焙 " + baseName + "（" + intro.mode + "，"
            + duration.ToString("0.##") + "s）");
        return null;
    }

    /// <summary>进场 clip（路点版）：localPosition + localRotation 四元数曲线
    ///  （平切线 —— 与 AnimGroupBakery.BuildShakeClip 同款写法）。路点序列首=起始
    ///  位姿、末=最终位姿；段时长按相邻路点距离比例分配（全程速度感一致，单段
    ///  = 全程）；旋转 startRot→finalRot 按全程 easeInOut slerp（rotate 模式有起始
    ///  偏航，其余模式恒为最终朝向）。smoothPath=false（折线路点 ≤3 个，如 tour
    ///  A→B→归位）：每段独立 easeInOut（段间有停顿感）；smoothPath=true（密集采样
    ///  的弧线路点，如 orbit 圆弧）：段内 linear + 全程时间经 easeInOut 重映射 ——
    ///  密集段逐段缓动会呈脉冲式蠕动，弧线必须整体缓动。</summary>
    private static AnimationClip BuildPathClip(string clipName, List<Vector3> path,
        Quaternion startRot, Quaternion finalRot, float duration, bool smoothPath)
    {
        var clip = new AnimationClip();
        clip.name = clipName;
        clip.legacy = false;
        clip.frameRate = 60f;

        int segs = path.Count - 1;
        // cum[i] = 第 i 段结束时间（0..duration；退化零段均分兜底）。
        var cum = new float[segs + 1];
        float total = 0f;
        for (int i = 0; i < segs; i++)
            total += Vector3.Distance(path[i], path[i + 1]);
        float acc = 0f;
        for (int i = 0; i < segs; i++)
        {
            acc += Vector3.Distance(path[i], path[i + 1]);
            cum[i + 1] = total > 0.0001f ? duration * acc / total : duration * (i + 1) / segs;
        }
        cum[0] = 0f;
        cum[segs] = duration;

        var px = new AnimationCurve();
        var py = new AnimationCurve();
        var pz = new AnimationCurve();
        var rx = new AnimationCurve();
        var ry = new AnimationCurve();
        var rz = new AnimationCurve();
        var rw = new AnimationCurve();
        const int steps = 44;
        for (int i = 0; i <= steps; i++)
        {
            float time = (float)i / steps * duration;
            // 弧线路点：全程时间先经 easeInOut 重映射（整体缓动），段内 linear。
            float pathTime = smoothPath
                ? AnimGroupBakery.EaseValue(Mathf.Clamp01(time / duration), "easeInOut") * duration
                : time;
            int seg = 0;
            while (seg < segs - 1 && pathTime > cum[seg + 1])
                seg++;
            float t0 = cum[seg];
            float t1 = cum[seg + 1];
            float e = smoothPath
                ? Mathf.Clamp01(t1 > t0 ? (pathTime - t0) / (t1 - t0) : 1f)
                : AnimGroupBakery.EaseValue(
                    t1 > t0 ? Mathf.Clamp01((pathTime - t0) / (t1 - t0)) : 1f, "easeInOut");
            var p = Vector3.Lerp(path[seg], path[seg + 1], e);
            float kG = AnimGroupBakery.EaseValue(Mathf.Clamp01(time / duration), "easeInOut");
            var q = Quaternion.Slerp(startRot, finalRot, kG);
            px.AddKey(new Keyframe(time, p.x));
            py.AddKey(new Keyframe(time, p.y));
            pz.AddKey(new Keyframe(time, p.z));
            rx.AddKey(new Keyframe(time, q.x));
            ry.AddKey(new Keyframe(time, q.y));
            rz.AddKey(new Keyframe(time, q.z));
            rw.AddKey(new Keyframe(time, q.w));
        }
        SetFlat(px); SetFlat(py); SetFlat(pz);
        SetFlat(rx); SetFlat(ry); SetFlat(rz); SetFlat(rw);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), px);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), py);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), pz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.x"), rx);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.y"), ry);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.z"), rz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.w"), rw);
        return clip;
    }

    /// <summary>Idle 常持 clip：最终位姿常值（首末两帧同值）。WD=off 状态下每帧
    ///  重写精确位姿，任何采样/混合残差当帧矫正（AnimGroupBakery Idle 定持
    ///  同款教训）。</summary>
    private static AnimationClip BuildHoldClip(string clipName, Vector3 finalPos, Quaternion finalRot)
    {
        var clip = new AnimationClip();
        clip.name = clipName;
        clip.legacy = false;
        clip.frameRate = 60f;
        var px = new AnimationCurve(new Keyframe(0f, finalPos.x), new Keyframe(0.1f, finalPos.x));
        var py = new AnimationCurve(new Keyframe(0f, finalPos.y), new Keyframe(0.1f, finalPos.y));
        var pz = new AnimationCurve(new Keyframe(0f, finalPos.z), new Keyframe(0.1f, finalPos.z));
        var rx = new AnimationCurve(new Keyframe(0f, finalRot.x), new Keyframe(0.1f, finalRot.x));
        var ry = new AnimationCurve(new Keyframe(0f, finalRot.y), new Keyframe(0.1f, finalRot.y));
        var rz = new AnimationCurve(new Keyframe(0f, finalRot.z), new Keyframe(0.1f, finalRot.z));
        var rw = new AnimationCurve(new Keyframe(0f, finalRot.w), new Keyframe(0.1f, finalRot.w));
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), px);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), py);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), pz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.x"), rx);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.y"), ry);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.z"), rz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.w"), rw);
        return clip;
    }

    /// <summary>intro 参数 JSON 嵌为 controller 的 IntroSource TextAsset 子资产
    ///  （随资产保存/清理，游戏运行时无视；仿 PersistAuthoringSource —— Unity 2017
    ///  TextAsset 无带参构造，经 SerializedObject 写 m_Script）。嵌入失败只降级
    ///  往返（Collect 退化为默认参数），不影响烘焙产物。</summary>
    private static void EmbedAuthoringSource(string controllerPath, CameraIntroDto intro)
    {
        try
        {
            string json = JsonUtility.ToJson(intro, true);
            var ta = new TextAsset();
            ta.name = SourceAssetName;
            var so = new SerializedObject(ta);
            var prop = so.FindProperty("m_Script");
            if (prop == null)
            {
                LayoutEditorLog.LogWarning("camera intro: 参数嵌入失败（m_Script 缺失）");
                return;
            }
            prop.stringValue = json;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(ta, controllerPath);
            EditorUtility.SetDirty(ta);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("camera intro: 参数嵌入失败：" + e.Message);
        }
    }

    /// <summary>清理本场景烘过的进场动画资产（vanilla/官方变体恢复时调用；
    ///  调用前 Animator controller 已换走，删除不悬空引用）。</summary>
    private static void CleanBakedAssets(Scene scene)
    {
        if (!scene.IsValid())
            return;
        var animDir = AnimGroupBakery.GetAnimationsFolder(scene.path);
        var baseName = ClipPrefix + scene.name;
        AnimGroupBakery.DeleteAssetIfExists(animDir + "/" + baseName + ".controller");
        AnimGroupBakery.DeleteAssetIfExists(animDir + "/" + baseName + ".anim");
        AnimGroupBakery.DeleteAssetIfExists(animDir + "/" + baseName + "_idle.anim");
    }

    /// <summary>按名加载 AnimatorController：优先精确路径，兜底全局按文件名匹配。</summary>
    private static AnimatorController LoadControllerAsset(string assetName, string preferredPath)
    {
        var direct = AssetDatabase.LoadAssetAtPath<AnimatorController>(preferredPath);
        if (direct != null)
            return direct;
        var guids = AssetDatabase.FindAssets(assetName + " t:AnimatorController");
        for (int i = 0; i < guids.Length; i++)
        {
            var p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(p)
                && System.IO.Path.GetFileNameWithoutExtension(p) == assetName)
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
        }
        return null;
    }

    /// <summary>平切线（与既有 bakery 的 SetLinear 同款：密集采样下近似平滑曲线）。</summary>
    private static void SetFlat(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            var key = curve[i];
            key.inTangent = 0f;
            key.outTangent = 0f;
            curve.MoveKey(i, key);
        }
    }
}
