using System.Collections.Generic;
using System.Linq;
using Roguelike.Movement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Gera o Cyborg_Motor.controller (SPEC §10.2, DS-15): um parâmetro int "State" (valores de AnimState) com transições
/// Any State → estado de 0 s, sem exit time, sem triggers e sem "transition to self"; "RunSpeedMul" controla o ritmo da
/// corrida e "Speed" (|vx| real) fica disponível. Cria os clipes novos (Rise, Apex, Land) a partir das fatias da sheet
/// de pulo. Idempotente: reconstrói o controller no mesmo asset (mesmo GUID).
/// </summary>
public static class CyborgAnimatorBuilder
{
    public const string ControllerPath = "Assets/Sprites/PlayerAnimations/Cyborg/Cyborg_Motor.controller";
    private const string Root = "Assets/Sprites/PlayerAnimations/Cyborg/";
    private const string JumpSheet = Root + "CyborgJump/CyborgJump.png";
    private const float Fps = 60f;

    [MenuItem("Tools/Movement/Build Cyborg Animator")]
    public static void BuildMenu()
    {
        AnimatorController controller = Build();
        Selection.activeObject = controller;
    }

    public static AnimatorController Build()
    {
        Dictionary<string, Sprite> jump = AssetDatabase.LoadAllAssetsAtPath(JumpSheet).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite Jump(int i) => jump.TryGetValue("CyborgJump_" + i, out Sprite s) ? s : null;

        // Antecipação (2 ticks, cosmética) → impulso → subida; ápice (braço para cima); pouso (último frame desenhado).
        AnimationClip rise = SpriteClip(Root + "CyborgJump/PlayerRiseAnimation.anim", false,
            (Jump(0), 0f), (Jump(1), 2f / Fps), (Jump(2), 6f / Fps));
        AnimationClip apex = SpriteClip(Root + "CyborgJump/PlayerApexAnimation.anim", true, (Jump(3), 0f));
        AnimationClip land = SpriteClip(Root + "CyborgJump/PlayerLandAnimation.anim", false, (Jump(5) != null ? Jump(5) : Jump(0), 0f));

        AnimationClip idle = Load(Root + "CyborgIdle/PlayerIdleAnimation.anim");
        AnimationClip run = Load(Root + "CyborgRunSprite/PlayerRunAnimation.anim");
        AnimationClip fall = Load(Root + "CyborgFall/PlayerFallAnimation.anim");
        AnimationClip wallSlide = Load(Root + "CyborgWallSlide/PlayerWallSlideAnimation.anim");
        AnimationClip airJump = Load(Root + "CyborgDoubleJump/DoubleJumpAnimation.anim");
        SetLoop(airJump, false); // SPEC §10.2: cambalhota sem loop

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray()) controller.RemoveParameter(parameter);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("RunSpeedMul", AnimatorControllerParameterType.Float);
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "RunSpeedMul") parameters[i].defaultFloat = 1f;
        }

        controller.parameters = parameters;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in sm.states.ToArray()) sm.RemoveState(child.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);

        var motions = new Dictionary<AnimState, Motion>
        {
            { AnimState.Idle, idle },
            { AnimState.Run, run },
            { AnimState.Rise, rise },
            { AnimState.Apex, apex },
            { AnimState.Fall, fall },
            { AnimState.Land, land },
            { AnimState.WallSlide, wallSlide },
            { AnimState.AirJump, airJump },
            { AnimState.Dash, rise },        // placeholder: frame de Rise + squash (1,3; 0,8) no PlayerView
            { AnimState.GrapplePull, apex }, // reusa o ápice
            { AnimState.Dead, null },        // congela o frame atual (o driver zera a velocidade do Animator)
        };

        int column = 0;
        AnimatorState idleState = null;
        foreach (AnimState value in System.Enum.GetValues(typeof(AnimState)))
        {
            AnimatorState state = sm.AddState(value.ToString(), new Vector3(300f + (column % 4) * 220f, 60f + (column / 4) * 90f, 0f));
            column++;
            state.motion = motions[value];
            state.writeDefaultValues = false;
            if (value == AnimState.Run)
            {
                state.speedParameterActive = true;
                state.speedParameter = "RunSpeedMul";
            }

            AnimatorStateTransition transition = sm.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.Equals, (int)value, "State");
            transition.duration = 0f;
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.None;

            if (value == AnimState.Idle) idleState = state;
        }

        sm.defaultState = idleState;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Movement] - Cyborg_Motor.controller gerado ({sm.states.Length} estados)");
        return controller;
    }

    private static AnimationClip Load(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) Debug.LogWarning($"[Movement] - Clipe não encontrado: {path}");
        return clip;
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        if (clip == null) return;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loop) return;
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static AnimationClip SpriteClip(string path, bool loop, params (Sprite sprite, float time)[] frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool created = clip == null;
        if (created) clip = new AnimationClip { frameRate = Fps };

        var keys = new List<ObjectReferenceKeyframe>();
        foreach ((Sprite sprite, float time) in frames)
        {
            if (sprite != null) keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
        }

        var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (created) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
        return clip;
    }
}
