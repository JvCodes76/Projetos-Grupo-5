using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Feedback do movimento (SPEC §10.3): partículas (pool por ParticleSystem.Emit), áudio (AudioCueSet, pool de 4 +
/// 1 loop), rumble, afterimages do dash e passos. Reage aos eventos no MESMO frame do tick (RF-53) — o
/// <see cref="IPlayerFeedbackProbe"/> expõe o frame do último feedback para o smoke test. Nunca escreve no núcleo.
/// </summary>
[DefaultExecutionOrder(42)]
public sealed class PlayerFeedback : MonoBehaviour, IPlayerFeedbackProbe
{
    private const int OneShotSources = 4;

    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerView view;
    [SerializeField] private FeedbackTuning tuning;
    [SerializeField] private AudioCueSet audioCues;
    [Tooltip("Sistema de partículas de poeira nos pés (Emit manual; não precisa tocar sozinho).")]
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private AfterimagePool afterimages;
    [Tooltip("Distância corrida no chão entre dois passos (u).")]
    [SerializeField] private float footstepDistance = 1.4f;

    private AudioSource[] oneShots;
    private AudioSource loopSource;
    private int nextSource;
    private float lastDeniedTime = -10f;
    private float wallSlideParticleDebt;
    private sbyte wallSide;
    private float dashTime = -1f;
    private int afterimagesSpawned;
    private Color afterimageColor;
    private float dashParticleDebt;
    private float footstepAccumulator;

    public int LastFeedbackFrame { get; private set; } = -1;

    public FeedbackCue LastCue { get; private set; }

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (view == null) view = GetComponent<PlayerView>();
        if (tuning == null) tuning = ScriptableObject.CreateInstance<FeedbackTuning>();

        oneShots = new AudioSource[OneShotSources];
        for (int i = 0; i < OneShotSources; i++) oneShots[i] = CreateSource("SfxOneShot" + i, false);
        loopSource = CreateSource("SfxLoop", true);
    }

    private AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void OnEnable()
    {
        EventBus<PlayerJumped>.Subscribe(HandleJumped);
        EventBus<PlayerWallJumped>.Subscribe(HandleWallJumped);
        EventBus<PlayerLanded>.Subscribe(HandleLanded);
        EventBus<PlayerDashed>.Subscribe(HandleDashed);
        EventBus<PlayerWallSlideChanged>.Subscribe(HandleWallSlideChanged);
        EventBus<PlayerAbilityRefilled>.Subscribe(HandleRefilled);
        EventBus<PlayerActionDenied>.Subscribe(HandleDenied);
        EventBus<PlayerGrappleFired>.Subscribe(HandleGrappleFired);
        EventBus<PlayerGrappleAttached>.Subscribe(HandleGrappleAttached);
        EventBus<PlayerGrappleReleased>.Subscribe(HandleGrappleReleased);
        EventBus<PlayerFellOut>.Subscribe(HandleFellOut);
        EventBus<PlayerRespawned>.Subscribe(HandleRespawned);
        EventBus<PlayerDied>.Subscribe(HandleDied);
    }

    private void OnDisable()
    {
        EventBus<PlayerJumped>.Unsubscribe(HandleJumped);
        EventBus<PlayerWallJumped>.Unsubscribe(HandleWallJumped);
        EventBus<PlayerLanded>.Unsubscribe(HandleLanded);
        EventBus<PlayerDashed>.Unsubscribe(HandleDashed);
        EventBus<PlayerWallSlideChanged>.Unsubscribe(HandleWallSlideChanged);
        EventBus<PlayerAbilityRefilled>.Unsubscribe(HandleRefilled);
        EventBus<PlayerActionDenied>.Unsubscribe(HandleDenied);
        EventBus<PlayerGrappleFired>.Unsubscribe(HandleGrappleFired);
        EventBus<PlayerGrappleAttached>.Unsubscribe(HandleGrappleAttached);
        EventBus<PlayerGrappleReleased>.Unsubscribe(HandleGrappleReleased);
        EventBus<PlayerFellOut>.Unsubscribe(HandleFellOut);
        EventBus<PlayerRespawned>.Unsubscribe(HandleRespawned);
        EventBus<PlayerDied>.Unsubscribe(HandleDied);
        if (loopSource != null) loopSource.Stop();
    }

    // ───────────────────────────── Eventos ─────────────────────────────

    private void HandleJumped(PlayerJumped evt)
    {
        FeedbackCue cue = FeedbackCueMap.ForJump(evt.Kind);
        Play(cue);
        if (evt.Kind == JumpKind.Air) EmitRing(evt.Position, tuning.AirJumpParticles, 3f);
        else EmitCone(evt.Position, Vector2.up, tuning.JumpParticles, 3f, 50f);
    }

    private void HandleWallJumped(PlayerWallJumped evt)
    {
        Play(FeedbackCue.WallJump);
        Vector2 away = new Vector2(-(int)evt.Side, 1f).normalized;
        Vector2 contact = evt.Position + new Vector2((int)evt.Side * 0.25f, 0.6f);
        EmitCone(contact, away, tuning.WallJumpParticles, 3f, 35f);
    }

    private void HandleLanded(PlayerLanded evt)
    {
        FeedbackCue cue = FeedbackCueMap.ForLanding(evt.ImpactSpeed, tuning.HardLandingSpeed);
        Play(cue);
        if (cue == FeedbackCue.LandHard)
        {
            EmitCone(evt.Position, Vector2.up, tuning.LandParticles, 2.5f, 80f);
            RumbleService.Play(cue, tuning);
        }
    }

    private void HandleDashed(PlayerDashed evt)
    {
        Play(FeedbackCue.Dash);
        RumbleService.Play(FeedbackCue.Dash, tuning);
        dashTime = 0f;
        afterimagesSpawned = 0;
        dashParticleDebt = 0f;
        afterimageColor = ColorForCharges(evt.ChargesLeft);
        afterimageColor.a = 0.6f;
    }

    private void HandleWallSlideChanged(PlayerWallSlideChanged evt)
    {
        if (evt.Started)
        {
            wallSide = (sbyte)evt.Side;
            Play(FeedbackCue.WallSlideLoop, loop: true);
        }
        else
        {
            wallSide = 0;
            if (loopSource != null) loopSource.Stop();
        }
    }

    private void HandleRefilled(PlayerAbilityRefilled evt) => Play(FeedbackCue.Refill);

    private void HandleDenied(PlayerActionDenied evt)
    {
        FeedbackCue cue = FeedbackCueMap.ForDenied(evt.Reason);
        if (cue == FeedbackCue.None) return;
        if (Time.unscaledTime - lastDeniedTime < tuning.DeniedCooldown) return;
        lastDeniedTime = Time.unscaledTime;
        Play(cue);
    }

    private void HandleGrappleFired(PlayerGrappleFired evt) => Play(FeedbackCue.GrappleFire);

    private void HandleGrappleAttached(PlayerGrappleAttached evt)
    {
        Play(FeedbackCue.GrappleAttach);
        EmitRing(evt.Anchor, 6, 4f);
        RumbleService.Play(FeedbackCue.GrappleAttach, tuning);
    }

    private void HandleGrappleReleased(PlayerGrappleReleased evt) => Play(FeedbackCue.GrappleRelease);

    private void HandleFellOut(PlayerFellOut evt)
    {
        Play(FeedbackCue.FellOut);
        wallSide = 0;
        if (loopSource != null) loopSource.Stop();
    }

    private void HandleRespawned(PlayerRespawned evt)
    {
        Play(FeedbackCue.Respawn);
        EmitRing(evt.Position + new Vector2(0f, 0.6f), tuning.RespawnParticles, 3f);
    }

    private void HandleDied(PlayerDied evt)
    {
        Play(FeedbackCue.Death);
        if (loopSource != null) loopSource.Stop();
    }

    // ───────────────────────────── Render ─────────────────────────────

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null) return;
        PlayerSnapshot snap = controller.Snapshot;
        float dt = Time.deltaTime;

        // Deslize: partículas contínuas no lado da parede.
        if (wallSide != 0 && snap.IsWallSliding)
        {
            wallSlideParticleDebt += tuning.WallSlideParticlesPerSecond * dt;
            int n = (int)wallSlideParticleDebt;
            if (n > 0)
            {
                wallSlideParticleDebt -= n;
                Vector2 contact = controller.RenderPosition + new Vector2(wallSide * 0.26f, 0.9f);
                EmitCone(contact, new Vector2(-wallSide * 0.3f, 1f), n, 1.5f, 20f);
            }
        }

        // Dash: afterimages nos instantes do tuning e trilha de partículas.
        if (dashTime >= 0f)
        {
            dashTime += dt;
            float[] times = tuning.AfterimageTimes;
            while (afterimagesSpawned < times.Length && dashTime >= times[afterimagesSpawned])
            {
                if (afterimages != null && view != null) afterimages.Spawn(view.Sprite, afterimageColor, tuning.AfterimageFade);
                afterimagesSpawned++;
            }

            if (snap.State == MotorStateId.Dash)
            {
                dashParticleDebt += dt / Mathf.Max(0.005f, tuning.DashParticleInterval);
                int n = (int)dashParticleDebt;
                if (n > 0)
                {
                    dashParticleDebt -= n;
                    EmitCone(controller.RenderPosition + new Vector2(0f, 0.6f), -snap.Velocity.normalized, n, 1f, 30f);
                }
            }
            else if (afterimagesSpawned >= times.Length)
            {
                dashTime = -1f;
            }
        }

        // Passos por distância corrida (sem Animation Events: os clipes atuais não têm frames marcados).
        if (snap.Grounded && Mathf.Abs(snap.Velocity.x) > 0.5f && snap.State == MotorStateId.Normal)
        {
            footstepAccumulator += Mathf.Abs(snap.Velocity.x) * dt;
            if (footstepAccumulator >= footstepDistance)
            {
                footstepAccumulator = 0f;
                Play(FeedbackCue.Footstep);
            }
        }
        else
        {
            footstepAccumulator = footstepDistance * 0.5f;
        }
    }

    // ───────────────────────────── Utilitários ─────────────────────────────

    private void Play(FeedbackCue cue, bool loop = false)
    {
        LastFeedbackFrame = Time.frameCount;
        LastCue = cue;
        if (audioCues == null || !audioCues.TryGet(cue, out AudioCueSet.Entry entry)) return;

        AudioClip clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
        float pitch = 1f + Random.Range(-entry.PitchVariation, entry.PitchVariation);
        if (loop)
        {
            loopSource.clip = clip;
            loopSource.volume = entry.Volume;
            loopSource.pitch = pitch;
            if (!loopSource.isPlaying) loopSource.Play();
            return;
        }

        AudioSource source = oneShots[nextSource];
        nextSource = (nextSource + 1) % oneShots.Length;
        source.pitch = pitch;
        source.PlayOneShot(clip, entry.Volume);
    }

    private void EmitCone(Vector2 origin, Vector2 dir, int count, float speed, float spreadDegrees)
    {
        if (dust == null || count <= 0) return;
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = (baseAngle + Mathf.Lerp(-spreadDegrees, spreadDegrees, t)) * Mathf.Deg2Rad;
            p.position = origin;
            p.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed;
            dust.Emit(p, 1);
        }
    }

    private void EmitRing(Vector2 origin, int count, float speed)
    {
        if (dust == null || count <= 0) return;
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            p.position = origin;
            p.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed;
            dust.Emit(p, 1);
        }
    }

    private Color ColorForCharges(int charges)
    {
        if (charges <= 0) return tuning.DashEmptyColor;
        return charges == 1 ? tuning.DashOneColor : tuning.DashTwoColor;
    }
}
