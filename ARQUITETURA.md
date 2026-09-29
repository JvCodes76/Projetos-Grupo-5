# Arquitetura — refactor roguelike

Documento de contratos da branch `refactor/roguelike` (tarefa 1.1 do [PLANO_REFACTOR_ROGUELIKE.md](PLANO_REFACTOR_ROGUELIKE.md)).
O plano continua sendo a fonte da verdade do **quê** e do **quando**; este arquivo registra o **como**: tipos, invariantes e decisões (ADRs, §10).
As decisões D1–D7 do plano não foram votadas pelo time; tudo aqui segue as recomendações da tabela da §2 do plano.

Código dos contratos: `Assets/_Roguelike/Core/` (asmdef `Roguelike.Core`). Corpos com `NotImplementedException("Tarefa X.Y …")` indicam a tarefa que os implementa (§9).

---

## 1. Visão geral

```
            Assembly-CSharp (MonoBehaviours)                      Roguelike.Core (C# puro + SOs)
 ┌───────────────────────────────────────────────┐      ┌──────────────────────────────────────────┐
 │ Views (HUD, LevelResultView, UpgradeSelection, │      │ Events: IEvent, EventBus<T>, Registry,    │
 │ RunEndView, MainMenu)                          │─────▶│         GameEvents (structs)              │
 │ Gameplay (characterMovement, GrapplingHook,    │◀─────│                                          │
 │ EndGoal, EnemyBullet, CameraFollow, LevelTimer)│ bus  │ Run: IRunFlow/RunFlow, RunState, ...      │
 │ RunManager (DDOL) ── chamadas diretas ────────▶│─────▶│ Upgrades, Stats, Levels (lógica + dados)  │
 └───────────────────────────────────────────────┘      └──────────────────────────────────────────┘
```

- **Fatos** (algo aconteceu) trafegam pelo `EventBus<T>`; **comandos** dentro do mesmo domínio são chamadas diretas (§3.1 do plano).
- A **lógica pura** (máquina de estados da run, sorteio de raridade, ofertas, stats, desempenho) vive em `Roguelike.Core`, não conhece cena nem o bus e é testada em EditMode.
- O **RunManager** (tarefa 3.1) é o adaptador: ouve eventos, chama o `IRunFlow`, carrega cenas e emite os eventos da run.
- Os **dados** (upgrades, raridades, fases, run) são ScriptableObjects; criar conteúdo novo não exige código.
- `Roguelike.Core` não referencia o Assembly-CSharp (é o contrário: o Assembly-CSharp enxerga o Core automaticamente).

## 2. Pastas, namespaces e dependências

| Pasta (`Assets/_Roguelike/Core/`) | Namespace | Conteúdo |
|---|---|---|
| `EventBus/` | `Roguelike.Events` | `IEvent`, `EventBus<T>`, `EventBusRegistry` |
| `Events/` | `Roguelike.Events` | `GameEvents.cs` (todos os structs de evento) |
| `Run/` | `Roguelike.Run` | `RunPhase`, `IRunFlow`, `RunFlow`, `RunState`, `RunSummary`, `LevelResult`, `PerformanceEvaluator`, `PerformanceGrade`, `GradeThresholds`, `RunEndReason`, `DeathCause` |
| `Upgrades/` | `Roguelike.Upgrades` | `StatType`/`StatTypes`, `StatModifier`/`ModifierOperation`, `AbilityFlags`, `UpgradeDefinition`, `UpgradeEffect`, `RarityDefinition`, `RarityTable`/`RarityWeight`, `UpgradeOffer`, `UpgradeOfferRequest`, `IUpgradeInventory`, `IUpgradeOfferGenerator`, `UpgradeOfferGenerator`, `RarityRoller` |
| `Stats/` | `Roguelike.Stats` | `PlayerStats`, `PlayerStatsSnapshot`, `PlayerBaseStats` |
| `Levels/` | `Roguelike.Levels` | `LevelDefinition`, `RunConfig` |

Outros assemblies: `Roguelike.Tests.EditMode` (namespace `Roguelike.Tests`, enxerga os membros `internal` do Core), `Roguelike.Editor` (namespace `Roguelike.EditorTools`, só Editor).

**Regra de dependência:** `Roguelike.Events` depende de todos os domínios (é o catálogo); **nenhum domínio depende de `Roguelike.Events`**. Os domínios se referenciam entre si livremente (é um assembly só; `UpgradeEffect` recebe `RunState`, `RunConfig` guarda `GradeThresholds`).

## 3. Event Bus

```csharp
public interface IEvent { }

public static class EventBus<T> where T : struct, IEvent
{
    public static void Subscribe(Action<T> handler);
    public static void Unsubscribe(Action<T> handler);
    public static void Raise(in T evt);
    internal static void Clear();
}

public static class EventBusRegistry
{
    public static bool LogRaises { get; set; }                 // [DEBUG] "[EventBus] - Raise X"
    internal static IReadOnlyCollection<Type> RegisteredEventTypes { get; }
    internal static void Register(Type eventType, Action clear); // chamado pelo cctor de EventBus<T>
    internal static void ClearAll();
    [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] private static void ClearOnSubsystemRegistration();
}
```

**Regras de uso**
- Eventos são `readonly struct : IEvent`, com nome no passado e payload imutável.
- MonoBehaviours assinam em `OnEnable` e desassinam em `OnDisable`. Nunca em `Awake`/`Start`/`OnDestroy`.
- Handler é método (não lambda), para o `Unsubscribe` funcionar.
- Só na main thread.

**Semântica do `Raise`** (implementação e testes: tarefa 1.2; ADR-04)

| Situação | Comportamento |
|---|---|
| Ordem | Ordem de inscrição |
| Mesmo delegate inscrito duas vezes | Fica uma vez só (idempotente) |
| Handler `null` em Subscribe/Unsubscribe | `ArgumentNullException` |
| Unsubscribe de handler não inscrito | No-op |
| Inscrever durante o Raise | Não é chamado neste Raise |
| Desinscrever durante o Raise (ainda não chamado) | Não é chamado |
| Raise dentro de um handler (reentrância, mesmo T ou outro) | Permitido |
| Exceção num handler | `Debug.LogException`; os seguintes são chamados |
| Sem inscritos | No-op, sem alocação |
| `EventBusRegistry.LogRaises == true` | `Debug.Log("[EventBus] - Raise NomeDoEvento")` |

**Limpeza** (ADR-03): o construtor estático de `EventBus<T>` registra o próprio `Clear` no `EventBusRegistry`. Em `SubsystemRegistration` (antes de qualquer `Awake` da primeira cena) o Registry chama `Clear` de todos. A lista de barramentos **nunca** é esvaziada, porque sem domain reload o construtor estático não roda de novo.

## 4. Catálogo de eventos

Todos em `Roguelike.Events` (`Events/GameEvents.cs`). Tipos exatos dos payloads:

### 4.1 MVP (§3.4 do plano)

| Evento | Payload | Emissor | Ouvintes | Invariantes |
|---|---|---|---|---|
| `RunStarted` | `int Seed`, `RunConfig Config` | RunManager | RunUI (PlayerStats é recriado junto com o RunState) | Seguido de `PlayerStatsChanged` com o kit base |
| `LevelStarted` | `int LevelIndex` (base 0), `LevelDefinition Level`, `float EffectiveTimeLimit` | RunManager | LevelTimer, HUD | Vem depois de `PlayerSpawned` e `PlayerStatsChanged` da mesma fase |
| `PlayerSpawned` | `GameObject Player` | RunManager (o spawner desde a 3.1; antes, SceneController) | CameraFollow, LevelTimer, Minimap, RunManager (o EnemyAI detecta o jogador por overlap e não precisa dele) | Jogador não nulo, já posicionado, `Awake`/`OnEnable` executados |
| `LevelTimeChanged` | `float ElapsedSeconds`, `float EffectiveTimeLimit`, (`RemainingSeconds` calculado) | LevelTimer | HUD, RunManager | `ElapsedSeconds = floor(t×10)/10`; emitido com 0 no início e depois só quando muda; nada depois do fim da fase |
| `LevelTimeExpired` | — | LevelTimer | RunManager, characterMovement | No máximo um por fase; não emitido se a fase já acabou |
| `PlayerDied` | `DeathCause Cause` | characterMovement | RunManager, LevelTimer (para) | Um por vida; **nunca** por tempo esgotado (ADR-10) |
| `LevelGoalReached` | — | EndGoal | LevelTimer (para), RunManager | No máximo um por fase |
| `LevelCompleted` | `LevelResult Result` (índice, tempo, limite efetivo, alvo, desempenho 0–1, nota) | RunManager | LevelResultView, telemetria | `Result` = `IRunFlow.LastLevelResult` |
| `UpgradeOffersGenerated` | `int LevelIndex`, `IReadOnlyList<UpgradeOffer> Offers` (cada um: `UpgradeDefinition Upgrade`, `RarityDefinition Rarity`, `RarityDefinition RolledRarity`, `bool IsEmpty`) | RunManager | UpgradeSelectionView | `Offers.Count == RunConfig.OfferCount`; ≥ 1 não vazio; lista copiada |
| `UpgradeSelected` | `UpgradeDefinition Upgrade` | UpgradeSelectionView | RunManager | Precisa ser uma oferta não vazia; o RunFlow ignora se não for |
| `PlayerStatsChanged` | `PlayerStatsSnapshot Stats` | RunManager (em nome do PlayerStats, ADR-02) | characterMovement (repassa ao GrapplingHook por chamada direta, 2.4), HUD | Após `RunStarted`, `UpgradeSelected` e cada `PlayerSpawned` (ADR-16); `Stats.IsValid` |
| `RunEnded` | `RunSummary Summary`, (`bool IsVictory` calculado) | RunManager | RunEndView, telemetria | `Summary` não nulo |

### 4.2 Decisões do jogador na UI (extensão, ADR-14)

| Evento | Payload | Emissor | Ouvintes | Efeito no RunFlow |
|---|---|---|---|---|
| `NewRunRequested` | — | MainMenu ("Nova Run") | RunManager | `StartRun` (ignorado fora de Menu) |
| `LevelResultDismissed` | — | LevelResultView | RunManager | `ContinueFromResult` |
| `RunEndDismissed` | — | RunEndView | RunManager | `ReturnToMenu` + carregar o menu |

> Ação para o orquestrador: pela §7 do plano, estes três eventos e as extensões de payload (ADR-24) precisam entrar também na tabela da §3.4 do plano.

## 5. Contratos de dados (ScriptableObjects)

Todos em arquivo próprio com o nome da classe, `sealed` (exceto `UpgradeEffect`), com `[CreateAssetMenu]` em `Roguelike/…` e `internal Configure(...)`/`Set(...)` para montar instâncias em testes (ADR-17). Os assets são criados na tarefa 3.3.

| SO (menu) | Campos | Invariantes (validadas pelo `DataValidationTests`, 3.3) |
|---|---|---|
| `UpgradeDefinition` (`Roguelike/Upgrade`) | `Id`, `DisplayName`, `Description`, `Icon` (Sprite), `Rarity` (RarityDefinition), `MaxStacks` (≥1), `Prerequisites` (`IReadOnlyList<UpgradeDefinition>`), `Modifiers` (`IReadOnlyList<StatModifier>`), `Unlocks` (AbilityFlags), `Effects` (`IReadOnlyList<UpgradeEffect>`) | Id único e não vazio; Rarity não nula; pré-requisitos no pool e sem ciclo; Multiply > 0 |
| `UpgradeEffect` (abstrato, sem menu) | `abstract void OnAcquired(RunState run)` | Sem estado da run no asset (ADR-22) |
| `RarityDefinition` (`Roguelike/Rarity`) | `Id`, `DisplayName`, `Color`, `Tier` (0 = Comum) | Id e Tier únicos |
| `RarityTable` (`Roguelike/Rarity Table`) | `Entries`: lista de `RarityWeight` { `Rarity`, `AnimationCurve WeightByPerformance` (X = p, Y = peso) } | Cada raridade uma vez; cobre as raridades do pool. Valores iniciais: Comum 70→25, Raro 25→40, Épico 5→25, Lendário 0→10 |
| `LevelDefinition` (`Roguelike/Level Definition`) | `DisplayName`, `SceneName`, `TimeLimit` (base), `TargetTime` | Cena no Build Settings; 0 < alvo < limite. D6: 20/12, 30/18, 25/15 s |
| `RunConfig` (`Roguelike/Run Config`) | `Levels` (ordem da run), `RarityTable`, `UpgradePool`, `BaseStats` (PlayerBaseStats), `OfferCount` (3), `GradeThresholds` (0,9/0,66/0,33), `UseFixedSeed`, `FixedSeed` | Levels não vazio; nada nulo; pool sem repetidos |
| `PlayerBaseStats` (`Roguelike/Player Base Stats`) | Um campo por `StatType` + `BaseAbilities`; leitura `Get(StatType)` | Valores ≥ 0; tabela abaixo |

**`PlayerBaseStats`: origem de cada valor** (Cyborg = `Assets/Prefabs/Cyborg.prefab`; nenhuma cena sobrescreve; ADR-18)

| StatType | Campo legado | Cyborg hoje | Padrão do SO |
|---|---|---|---|
| `MaxSpeed` | `characterMovement.maxSpeed` | 10 + 0,5 × agility(1) = **10,5** | 10,5 |
| `Acceleration` | `characterMovement.acceleration` | 40 + agility(1) = **41** | 41 |
| `AirAcceleration` | `characterMovement.airAcceleration` | 20 | 20 |
| `JumpHeight` | `characterMovement.jumpHeight` | 2,5 + 0,1 × strength(1) = **2,6** | 2,6 |
| `CoyoteTime` | `characterMovement.coyoteTime` | 0,1 | 0,1 |
| `WallSlideSpeed` | `characterMovement.wallSlideSpeed` | 2 | 2 |
| `MaxAirJumps` | `PlayerData.maxAirJumps` | 1 | **0** (kit base) |
| `GrappleRadius` | `GrapplingHook.grappleRadius` | 9 | 9 |
| `GrappleCooldown` | `GrapplingHook.grappleCooldown` | 0,5 | 0,5 |
| `GrappleLaunchForce` | `GrapplingHook.launchBoostForce` | 25 | 25 |
| `TimeLimitBonus` | — (novo) | — | 0 |
| `BaseAbilities` | `PlayerData.canWallJump` / `canGrapplingHook` | ambos true | **None** (kit base) |

Parâmetros de movimento que não são upgradáveis (`deceleration`, `turnSpeed`, `timeToApex`, `hangTime`, `wallJumpForce`, `airJumpHeightMultiplier`…) continuam como `[SerializeField]` nos MonoBehaviours.

## 6. Contratos de lógica pura

### 6.1 Stats
- **`StatModifier`** (`[Serializable] struct`): `Stat`, `Operation` (`Add` | `Multiply`), `Value`. Por stat: **`final = max(0, (base + ΣAdd) × ΠMultiply)`**. `Multiply` é fator (1,08 = +8 %; 0,7 = −30 %) e se compõe entre stacks (ADR-09).
- **`PlayerStats`** (classe, tarefa 2.1): `new PlayerStats(PlayerBaseStats)`; leitura `Get(StatType)`, `GetInt(StatType)` (`Mathf.RoundToInt`), `HasAbility(AbilityFlags)`, `Abilities`, `BaseStats`; escrita `ApplyUpgrade(UpgradeDefinition)` (um stack: modificadores + flags), `AddModifier`, `UnlockAbilities`, `Reset()`; `CreateSnapshot()`. Não emite eventos, não valida stacks, não chama `UpgradeEffect`.
- **`PlayerStatsSnapshot`** (`readonly struct`, pronto): cópia imutável; `Get`, `GetInt`, `HasAbility`, `Abilities`, `IsValid` (`default` é inválido).
- **`AbilityFlags`** (`[Flags]`): `None`, `WallGrab`, `GrapplingHook`. Pulo duplo = `MaxAirJumps ≥ 1`. Planejadas para a Etapa 5, ainda não existem: `Bazooka` e `Teleport` (Raridade 4), como bits novos no fim (ADR-23).
- **`JumpPhysics`** (estático, adicionado na 2.1): `JumpSpeed(altura, tempoAteApice)` = `altura / tempo`, que é a fórmula legada e não a cinemática `2h/t`, preservada de propósito. `Gravity(altura, tempo)` = `2h / t²`. `GravityMultiplier(altura, tempo, |gravidadeDoMundo|, gravityScalePadrão)`. As fórmulas foram extraídas de `characterMovement.CalculateJumpVariables`. É a base do teste de valores de referência (`jumpSpeed` 7,027, `gravMultiplier` 3,872 com o Cyborg) e é o que o `characterMovement` passa a usar na 2.4.

### 6.2 Desempenho (`PerformanceEvaluator`, tarefa 2.2)
- `float Evaluate(float elapsedSeconds, float timeLimit, float targetTime)` = `clamp01((limite − tempo) / (limite − alvo))`, chamado com o **limite base** (ADR-12). Limite ≤ alvo: 1 se tempo ≤ limite, senão 0. Tempo negativo: exceção.
- `PerformanceGrade GetGrade(float p, GradeThresholds t)`: `p ≥ S → S`, `≥ A → A`, `≥ B → B`, senão `C` (inclusivo). Overload `GetGrade(p)` usa `GradeThresholds.Default`.
- `PerformanceGrade`: `C < B < A < S` (comparável).

### 6.3 Raridade e ofertas (tarefa 2.2)
- **`RarityRoller`** (estático): `RarityDefinition Roll(RarityTable, float p, System.Random)` (exatamente um `NextDouble()` por chamada) e `IReadOnlyList<float> GetProbabilities(RarityTable, float p)` (alinhada com `Entries`). p limitado a [0, 1], peso negativo = 0, soma 0 → menor tier.
- **`IUpgradeOfferGenerator`**: `IReadOnlyList<UpgradeOffer> Generate(in UpgradeOfferRequest)`. Determinístico, retorna exatamente `OfferCount` itens, sem repetição, sem inelegível, não altera nada.
- **`UpgradeOfferRequest`** (`readonly struct`): `RunSeed`, `LevelIndex` (fase recém-concluída), `Performance`, `OfferCount`, `Pool`, `RarityTable`, `Inventory` (`IUpgradeInventory`: `int GetStacks(UpgradeDefinition)`, implementado pelo `RunState`).
- **`UpgradeOfferGenerator`** (padrão): `rng = new System.Random(CombineSeed(runSeed, levelIndex))`; por slot sorteia a raridade, filtra candidatos elegíveis **na ordem do pool** e escolhe `rng.Next(n)`; fallback por tier (ADR-07); sem candidato → `UpgradeOffer.Empty(rolled)` (ADR-06). `CombineSeed` não pode usar `HashCode.Combine` (ADR-08).

### 6.4 Run (tarefa 2.3)
- **`RunState`** (classe): `Config`, `Seed`, `CurrentLevelIndex`, `LevelCount`, `CurrentLevel`, `IsLastLevel`, `EffectiveTimeLimit` (= `CurrentLevel.TimeLimit + Stats.Get(TimeLimitBonus)`), `Stats`, `LevelResults`, `AcquiredUpgrades` (um item por stack), `GetStacks`. Escrita só via `internal` pelo RunFlow: `AcquireUpgrade` (stack++ → `Stats.ApplyUpgrade` → `Effects[i].OnAcquired(this)`), `RecordLevelResult`, `AdvanceToNextLevel`, `CreateSummary`.
- **`LevelResult`** (`readonly struct`, pronto): `LevelIndex`, `Level`, `ElapsedSeconds`, `EffectiveTimeLimit`, `TargetTime`, `Performance`, `Grade`.
- **`RunSummary`** (classe imutável, pronta): `Seed`, `EndReason` (`RunEndReason`: Victory, TimeExpired, PlayerDied), `DeathCause`, `LevelCount`, `LevelResults`, `LevelsCompleted`, `FailedLevelIndex` (−1 na vitória), `TotalTimeSeconds` (só fases concluídas), `AcquiredUpgrades`, `IsVictory`.
- **`IRunFlow` / `RunFlow`** (`new RunFlow(IUpgradeOfferGenerator)`): propriedades `Phase`, `State` (null em Menu), `CurrentOffers`, `LastLevelResult`, `Summary`. Comandos retornam `bool`; fora de fase retornam `false` sem mudar nada; argumento inválido lança (ADR-15).

| Fase atual (`RunPhase`) | Comando | Nova fase | Efeito |
|---|---|---|---|
| `Menu` | `StartRun(config, seed)` | `LoadingLevel` | `RunState` novo, fase 0, stats do kit base |
| `LoadingLevel` | `StartLevel()` | `Playing` | — |
| `Playing` | `CompleteLevel(elapsed)` | `LevelResult` | Calcula p (limite base) e nota; `RecordLevelResult` |
| `Playing` | `FailByTimeout()` | `Defeat` | `Summary` (TimeExpired) |
| `Playing` | `FailByDeath(cause)` | `Defeat` | `Summary` (PlayerDied, cause) |
| `LevelResult` | `ContinueFromResult()` | `Victory` se era a última fase | `Summary` (Victory) |
| | | senão `UpgradeSelection` se há oferta não vazia | `CurrentOffers` preenchido |
| | | senão `LoadingLevel` | `AdvanceToNextLevel` (pula a escolha) |
| `UpgradeSelection` | `SelectUpgrade(upgrade)` | `LoadingLevel` | `AcquireUpgrade`, `AdvanceToNextLevel`, limpa ofertas |
| `Victory` / `Defeat` | `ReturnToMenu()` | `Menu` | Descarta `State` e `Summary` |

Mapeamento para o diagrama da §1: Menu, CarregandoFase = `LoadingLevel`, Jogando = `Playing`, Resultado = `LevelResult`, EscolhaUpgrade = `UpgradeSelection`, Vitoria = `Victory`, Derrota = `Defeat`.

## 7. Sequência de runtime (referência para a 3.1)

```
MainMenu "Nova Run"        → NewRunRequested
RunManager                 → flow.StartRun(config, seed) → RunStarted → PlayerStatsChanged(kit base) → carrega cena da fase 0
Cena carregada             → spawner posiciona o jogador → PlayerSpawned → PlayerStatsChanged(snapshot atual)
RunManager                 → flow.StartLevel() → LevelStarted(i, level, limiteEfetivo)
LevelTimer                 → LevelTimeChanged(0.0) … a cada 0,1 s (RunManager guarda o último valor)
EndGoal                    → LevelGoalReached → LevelTimer para; RunManager: flow.CompleteLevel(últimoTempo) → LevelCompleted
LevelResultView "Continuar"→ LevelResultDismissed → flow.ContinueFromResult():
                               Victory          → RunEnded
                               UpgradeSelection → UpgradeOffersGenerated
                               LoadingLevel     → carrega a próxima fase (sem ofertas elegíveis)
UpgradeSelectionView       → UpgradeSelected → flow.SelectUpgrade → PlayerStatsChanged → carrega a próxima fase
Derrota                    → LevelTimeExpired → flow.FailByTimeout() → RunEnded
                             PlayerDied(cause) → flow.FailByDeath(cause) → RunEnded
RunEndView "Menu"          → RunEndDismissed → flow.ReturnToMenu() → carrega MainMenu
```

Se o comando do RunFlow retornar `false` (evento fora de fase), o RunManager só loga `[RunManager] - …` e não faz nada.

**Como ficou na Etapa 3:**
- **Componentes:** o `RunManager`, o `SceneLoader`, o `LevelTimer` e o `RunTelemetry` ficam na raiz do prefab `RunSystems` (DDOL), em `Assets/_Roguelike/Prefabs/`. O canvas `RunUI` com as 4 views é filho dele.
- **Onde vive:** o `RunSystems` está no `MainMenu`. Ao voltar ao menu, a cópia da cena se desliga e se destrói no `Awake` (ADR-25).
- **Jogador:** cada fase instancia o próprio `Cyborg` no objeto com a tag `SpawnPoint`.
- **Pausa:** ver a ADR-26.
- **Primeiro tick da fase:** ver a ADR-27.

## 8. Guia de migração da tarefa 1.3

Os structs acima são usados sem alteração. O fluxo antigo (SceneController/Timer) continua funcionando até a 3.1.

| Hoje | Depois da 1.3 | Quem emite | Quem ouve na transição |
|---|---|---|---|
| `EndGoal` chama `SceneController.instance.NextLevel()` (com fallback `FindFirstObjectByType`) | `EndGoal` emite `LevelGoalReached` | EndGoal | SceneController (agenda `NextLevel` com `nextLevelDelay` = 0,5 s), Timer (para e soma o tempo) |
| `Timer` zera e chama `player.Die()` | `Timer` mostra o game over e emite `LevelTimeExpired` | Timer | characterMovement (trava, **sem** `PlayerDied`) |
| `EnemyBullet` acha o jogador por `FindFirstObjectByType` e chama `Die()` | `EnemyBullet` pega o `characterMovement` do collider atingido e chama `Die(DeathCause.EnemyProjectile)`; o `characterMovement` emite `PlayerDied` | characterMovement | Timer (para, mostra o game over e salva) |
| `SceneController.OnPlayerSpawned` (evento estático) | `PlayerSpawned` | SceneController | CameraFollow, Minimap (o Timer não precisa mais do jogador) |

**Como ficou (1.3):** a tela de game over, que antes era ligada em três lugares (characterMovement, Timer, EnemyBullet), agora tem um dono só: o Timer do `Canvas.prefab`, que a mostra no próprio tempo esgotado e ao ouvir `PlayerDied`.

**Depois da 4.1:** esta seção é histórica. O `SceneController`, o `Timer`, o `Canvas.prefab` (game over), o `PlayerData`, o save, a loja e as moedas foram removidos. Os papéis deles passaram para o `RunManager`, o `LevelTimer` e a `RunEndView`.

## 9. Quem implementa cada arquivo

| Arquivo(s) | Estado depois da 1.1 | Tarefa |
|---|---|---|
| `EventBus/EventBus.cs`, `EventBus/EventBusRegistry.cs` | Assinaturas + mecanismo de registro; corpos `NotImplementedException` | 1.2 (`dev`) |
| `Stats/PlayerStats.cs` | Assinaturas; corpos `NotImplementedException` | 2.1 (`dev`) |
| `Stats/PlayerBaseStats.cs` | Pronto (valores padrão = Cyborg); a 2.1 só ajusta valores se o teste de referência pedir | 2.1 |
| `Run/PerformanceEvaluator.cs`, `Upgrades/RarityRoller.cs`, `Upgrades/UpgradeOfferGenerator.cs` | Assinaturas; corpos `NotImplementedException` | 2.2 (`dev`) |
| `Run/RunFlow.cs`, `Run/RunState.cs` | Assinaturas; corpos `NotImplementedException` | 2.3 (`dev-core`) |
| Todo o resto (`IEvent`, `GameEvents`, enums, SOs de dados, `StatModifier`, `PlayerStatsSnapshot`, `UpgradeOffer`, `UpgradeOfferRequest`, `LevelResult`, `RunSummary`, `GradeThresholds`, interfaces) | Pronto | 1.1 (mudança de contrato passa pelo arquiteto/João) |
| `Run/LevelClock.cs` (puro) + `Assets/scripts/Run/RunManager.cs`, `SceneLoader.cs`, `LevelTimer.cs` | Pronto | 3.1 (`dev-core`) |
| `Run/TimeFormat.cs` (puro) + `Assets/scripts/UI/Run/*View.cs` + `MainMenu.cs` (`NewRunRequested`) | Pronto | 3.2 (`dev`) |
| Assets de `Assets/_Roguelike/Data/` + `Tests/EditMode/DataValidationTests.cs` | Pronto | 3.3 (`mecanico`) |
| `Assets/_Roguelike/Prefabs/RunSystems.prefab`, `UpgradeCard.prefab` e as cenas da run | Pronto | 3.4 (`integrador-unity`) |
| `Run/RunTelemetryRecorder.cs` (puro) + `Assets/scripts/Run/RunTelemetry.cs` | Pronto | 4.3 (`dev`) |
| `Assets/scripts/Debug/RunSmokeTestDriver.cs` + `Assets/scripts/Editor/RunSmokeTestRunner.cs` | Pronto | 4.2 (`dev`) |

Os conjuntos de 2.1, 2.2 e 2.3 são disjuntos. Mas **em runtime** o RunState/RunFlow (2.3) chama `PlayerStats` (2.1) e `PerformanceEvaluator` (2.2): os testes da 2.3 que passam por `StartRun`, `CompleteLevel` ou `SelectUpgrade` só ficam verdes depois de 2.1 e 2.2. Use um `IUpgradeOfferGenerator` falso nos testes da 2.3.

## 10. ADRs

**ADR-01 — Namespace `Roguelike.Events` para `EventBus/` e `Events/`.** Um namespace `Roguelike.EventBus` colidiria com o tipo `EventBus<T>` e obrigaria dois `using` em todo assinante. Os outros namespaces seguem a pasta (`Roguelike.Run`, `.Upgrades`, `.Stats`, `.Levels`).

**ADR-02 — Domínios não usam o bus; o adaptador emite.** `RunFlow`, `RunState` e `PlayerStats` são puros e não chamam `EventBus`: os testes não precisam limpar barramentos e a ordem dos eventos fica num lugar só (o RunManager). Por isso "PlayerStats ouve RunStarted/UpgradeSelected" da §3.4 vira: o RunState cria/atualiza o PlayerStats via RunFlow, e o RunManager emite `PlayerStatsChanged`.

**ADR-03 — Descoberta dos barramentos pelo construtor estático.** Cada `EventBus<T>` se registra no `EventBusRegistry` no primeiro uso. Não precisa de reflexão nem de lista manual de tipos, e funciona com IL2CPP. O Registry nunca esquece um barramento (sem domain reload o construtor estático não roda de novo).

**ADR-04 — Semântica do Raise** (tabela da §3). Handler desinscrito durante o Raise não é chamado: evita `MissingReferenceException` quando um handler destrói outro objeto que assina o mesmo evento. Exceção isolada por handler para uma view quebrada não travar a run. Inscrição idempotente contra `OnEnable` duplicado.

**ADR-05 — `LevelDefinition` referencia a cena pelo nome.** Índice de build quebra ao reordenar o Build Settings (D4 prevê adicionar fases). `SceneAsset` é só de Editor e um campo `#if UNITY_EDITOR` muda o layout de serialização no build. Custo: renomear a cena exige atualizar o asset, e o `DataValidationTests` (3.3) confere isso.

**ADR-06 — Ofertas em lista de tamanho fixo, com slot vazio explícito.** `Offers.Count == OfferCount` sempre; slot sem candidato = `UpgradeOffer.Empty(raridadeSorteada)`. A view esconde os vazios. Se todos forem vazios (pool esgotado), o RunFlow pula a escolha e vai direto para a próxima fase, senão a run travaria em EscolhaUpgrade. D7 (reroll/pular) cabe depois como comandos novos no `IRunFlow` e um contador no `UpgradeOfferRequest`.

**ADR-07 — Fallback de raridade por tier.** "Tenta a raridade abaixo, depois a acima" (§3.5) vira: todas as raridades de tier menor, da mais próxima para a mais distante; depois todas as de tier maior, da mais próxima para a mais distante. Só com a vizinha imediata, um Lendário sorteado sem candidato e sem Épico elegível daria slot vazio mesmo havendo Comuns. A ordem vem de `RarityDefinition.Tier`, não da ordem da tabela.

**ADR-08 — Determinismo das ofertas.** Seed da rodada = `UpgradeOfferGenerator.CombineSeed(runSeed, levelIndex)`, uma mistura própria (`System.HashCode` e `string.GetHashCode` são aleatorizados por processo). Candidatos na ordem do `RunConfig.UpgradePool`, um `NextDouble()` por sorteio de raridade e um `Next(n)` por escolha de upgrade. Mudar a ordem do pool muda as ofertas de uma seed (aceito).

**ADR-09 — Matemática dos modificadores.** `(base + ΣAdd) × ΠMultiply`, com clamp em 0. `Multiply` é fator composto: 2× "−30 % cooldown" = ×0,49, nunca negativo (com percentuais somados daria −60 % e, com mais stacks, valor negativo). Stats inteiros usam só `Add` e são lidos com `Mathf.RoundToInt`.

**ADR-10 — `DeathCause` e "tempo esgotado não é morte".** Enum `Unknown, EnemyProjectile, EnemyContact, Hazard, OutOfBounds` (valores explícitos). Tempo esgotado já é o fato `LevelTimeExpired`: se o characterMovement também emitisse `PlayerDied`, a derrota teria dois fatos para a mesma coisa. Ao ouvir `LevelTimeExpired`, o characterMovement trava e anima a morte sem emitir `PlayerDied`. O `RunSummary` distingue os dois por `EndReason`.

**ADR-11 — Limiares da nota no `RunConfig`.** `GradeThresholds` (0,9/0,66/0,33 por padrão) é valor de balanceamento, e a §7 do plano proíbe fixá-lo no código.

**ADR-12 — Desempenho usa o limite base.** `p` usa `LevelDefinition.TimeLimit`, não o limite com `TimeLimitBonus`. O Relógio de Bolso (+5 s) só adia a derrota. Se o bônus entrasse no `p`, o upgrade também subiria as raridades de todas as fases seguintes, um efeito em bola de neve. Terminar entre o limite base e o efetivo dá `p = 0`. **A confirmar com o João.**

**ADR-13 — Tempo oficial da fase = último `LevelTimeChanged`.** `LevelGoalReached` não tem payload e o EndGoal não conhece o timer. O RunManager guarda o último `ElapsedSeconds` (múltiplo de 0,1 s) e passa para `CompleteLevel`. Assim o tempo do resultado é exatamente o que o jogador viu no HUD, sem referência direta RunManager → LevelTimer. Erro máximo de 0,1 s a favor do jogador.

**ADR-14 — Eventos de decisão do jogador na UI.** `NewRunRequested`, `LevelResultDismissed` e `RunEndDismissed` completam o diagrama da §1 (Menu → Nova Run, Resultado → próximo passo, Vitória/Derrota → Menu) sem que as views referenciem o RunManager (exigência da 3.2). São fatos de input, como `UpgradeSelected`.

**ADR-15 — Comandos do RunFlow retornam `bool`.** Fora de fase: `false`, sem efeito e sem exceção. Isso resolve as corridas reais (meta e timer no mesmo frame, `PlayerDied` depois de `LevelTimeExpired`, clique duplo). Argumento inválido é bug e lança. O `RunState` só é escrito pelo RunFlow (membros `internal`), então nenhum adaptador pula a máquina de estados.

**ADR-16 — Sem eventos "sticky": estado atual é reemitido.** O bus não guarda o último valor. Um jogador recém-instanciado recebe os stats porque o RunManager emite `PlayerStatsChanged` logo depois de cada `PlayerSpawned` (o `OnEnable` do jogador já rodou nesse ponto). Os ouvintes aplicam o snapshot inteiro, então receber o mesmo valor duas vezes é inofensivo.

**ADR-17 — SOs: um arquivo por classe, `sealed`, `Configure` internal.** A Unity só serializa um SO cujo arquivo tem o nome da classe (verificado via `MonoScript.GetClass()`). `internal Configure`/`Set` (visível para `Roguelike.Tests.EditMode`) monta instâncias em teste sem reflexão e sem abrir setters públicos. Não há `OnValidate` alterando dados: a validação fica no `DataValidationTests` (3.3).

**ADR-18 — Padrões do `PlayerBaseStats` = sensação atual do Cyborg.** `MaxSpeed`, `Acceleration` e `JumpHeight` recebem hoje um bônus do `PlayerData` (agility = strength = 1 num save novo). O padrão é o valor efetivo (10,5 / 41 / 2,6), não o do prefab (10 / 40 / 2,5), para cumprir "sensação do movimento base idêntica" (2.4). O kit de habilidades segue o design novo: `MaxAirJumps = 0`, `BaseAbilities = None`. **A confirmar com o João.**

**ADR-19 — `WallGrab` controla o deslize E o pulo na parede.** Hoje o deslize acontece sempre e só o wall jump depende de `canWallJump`. Com o upgrade "Wall Grab: desbloqueia deslizar e pular na parede" (§3.5), sem a flag o jogador não desliza. A mudança de comportamento é intencional e vale para a 2.4.

**ADR-20 — Payloads com coleção são imutáveis.** `RunSummary` é classe imutável (listas copiadas; não existe instância "default" vazia). `UpgradeOffersGenerated` copia as ofertas no construtor, porque o RunFlow limpa a lista dele depois da escolha.

**ADR-21 — Seed da run.** O RunManager sorteia a seed (ex.: `Environment.TickCount`), a não ser que `RunConfig.UseFixedSeed` esteja ligado, e então usa `FixedSeed` (reproduzir ofertas em playtest e QA). A seed vai no `RunStarted` e no `RunSummary` (telemetria, 4.3).

**ADR-22 — `UpgradeEffect.OnAcquired(RunState)`.** O contexto mais geral para efeitos futuros (vida extra, bônus de tempo…). O asset é compartilhado entre runs, então todo estado vai para o `RunState`. É chamado depois de modificadores e flags, uma vez por stack.

**ADR-23 — Enums serializados com valores explícitos, só acrescentar.** `StatType` (contíguo a partir de 0, indexa o snapshot), `ModifierOperation`, `AbilityFlags` (bits), `DeathCause`, `RunEndReason`, `PerformanceGrade`: nunca reordenar nem renumerar, porque os assets e a telemetria guardam o número.

**ADR-24 — Extensões de payload em relação à §3.4.** `LevelTimeChanged` leva também `EffectiveTimeLimit` (o HUD mostra "tempo / limite" sem depender de ter recebido `LevelStarted`). `LevelCompleted` leva um `LevelResult`, que inclui o tempo-alvo, e a mesma struct é reaproveitada no `RunSummary`. `UpgradeOffersGenerated` leva o `LevelIndex` e, por oferta, a raridade sorteada (telemetria, 4.3). `UpgradeOffer` guarda a raridade sorteada e expõe a real (`Upgrade.Rarity`).

**ADR-25 — Sistemas da run num prefab DDOL, e não nas cenas das fases (3.1/3.4).** O plano previa um `LevelTimer` em cada fase, com referência à `LevelDefinition`. Mas o `LevelStarted` já traz a fase e o limite efetivo, então o timer vive no `RunSystems`, junto do `RunManager`. Uma fase nova precisa só de `SpawnPoint`, `EndGoal` e `EventSystem`, e não há ligação por cena para esquecer. A cópia do `RunSystems` que vem com o `MainMenu` se desliga com `SetActive(false)` antes do `Destroy`, para que as views filhas não fiquem inscritas nem por um frame.

**ADR-26 — Política de `timeScale`.**
- **Pausa (`timeScale = 0`):** do `LevelCompleted` até a cena seguinte carregar, ou seja, durante o resultado, a escolha de upgrade e a vitória.
- **Volta a 1:** só no callback do carregamento. Voltar antes despausaria a fase antiga durante o carregamento assíncrono.
- **Derrota:** não pausa. A animação de morte continua, e a `RunEndView` aparece com atraso em tempo real.
- **Views:** usam tempo não escalado.
- **Quem mexe:** só o `RunManager`.

**ADR-27 — O primeiro tick da fase é descartado.** O `LevelStarted` sai no frame em que a cena é ativada. O `deltaTime` do frame seguinte inclui os `Awake` da cena, até `Time.maximumDeltaTime` (0,33 s). O `LevelTimer` ignora os ticks desses dois frames: no máximo cerca de 2 frames a favor do jogador, dentro do erro da ADR-13.

**ADR-28 — Telemetria sem depender do bus no Core (4.3).** O `RunTelemetryRecorder` (Core/Run) recebe tipos de domínio e devolve linhas de CSV. Só o adaptador `RunTelemetry` ouve eventos, o que mantém a regra da §2. O CSV segue a RFC 4180, com vírgula e decimais com ponto (cultura invariante). É uma linha por fase jogada, e a última da run leva `run_result`. `death_cause` vai como número (ADR-23). Erro de IO vira warning e nunca derruba o jogo.

**ADR-29 — Abandono da run.** Se o menu for carregado por fora do fluxo, o `RunManager` descarta a run sem emitir eventos. Isso é defensivo e hoje é inalcançável: não há menu de pausa e os botões legados saíram na 4.1. Um futuro "Sair para o menu" deve entrar como evento no catálogo (ex.: `RunAbandoned`), para que as views e a telemetria fiquem sabendo.
