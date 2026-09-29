# Auditoria do movimento atual — Cyborg

Estado auditado: commit `8df28c9` (branch com os bugfixes do [RELATORIO_BUGS.md](../../RELATORIO_BUGS.md)) · Unity 6000.1.7f1 · URP 2D · Input System 1.14.2 · LDtk-Unity 6.11.2
Escrito em 29/09/2026 como insumo para o refactor de movimento (estilo Celeste / Hollow Knight / Super Meat Boy) e para a Etapa 2 do [PLANO_REFACTOR_ROGUELIKE.md](../../PLANO_REFACTOR_ROGUELIKE.md).

> **Método.** Não há Unity neste container. Tudo aqui vem da leitura dos scripts e dos YAML de prefabs, cenas e assets (`.prefab`, `.unity`, `.controller`, `.anim`, `.meta`, `ProjectSettings/*.asset`). As métricas da §7 vêm de uma simulação em Python que reproduz passo a passo o `FixedUpdate` de `characterMovement` (script primeiro, depois a integração do Box2D: `v += g·dt; x += v·dt`) com os valores **reais** do prefab. A geometria das fases (§5) foi reconstruída a partir dos `m_CompositePaths` serializados nas cenas. Os números marcados como *estimado* dependem de detalhes do solver do Box2D e devem ser confirmados com um log em Play Mode.

Severidade para o *feel*: 🔴 estraga a sensação ou gera bug perceptível a toda hora · 🟠 deixa o controle pesado, impreciso ou inconsistente · 🟡 menor, risco futuro ou polimento.

---

## Resumo executivo

1. **O pulo do chão não é uma parábola.** Enquanto o botão está pressionado, o personagem sobe em **velocidade constante (7,03 u/s) com gravidade 0** e, ao atingir `jumpHeight`, a velocidade vai **de 7,03 para 0 em um único passo**. A sensação é de elevador que bate num teto invisível. Depois cai com gravidade de 3,9 g e um arrasto multiplicativo por passo (`characterMovement.cs:402-427`).
2. **A altura do pulo não cresce de forma monotônica com o tempo de botão.** Segurando até o fim, o pulo chega a **2,67 u**. Soltando exatamente entre 0,36 e 0,38 s, chega a **3,25 u** (+22 %) (§7.2).
3. **Há um bug que "come" o pulo.** Se o pulo sai com o botão já solto (tap curto dentro do jump buffer antes de pousar, que é exatamente o caso que o buffer deveria atender), o `FixedUpdate` zera `vy` a cada passo e o jogador fica **flutuando**, inclusive ao sair de plataformas, até apertar pulo de novo (`:416-420`). No kit base do roguelike (`MaxAirJumps = 0`), isso acontece em todo pulo apertado **e solto** um pouco antes de pousar (um tap rápido).
4. **O teto não é detectado.** No prefab, `ceilingLayer` aponta para a layer *Default*, e os tiles estão na layer *Ground* (6). Quem segura o pulo embaixo de um teto **gruda nele** até soltar o botão. Moedas e o EndGoal (triggers na layer *Default*) funcionam como "teto" e cortam o pulo.
5. **A câmera esconde o que vem pela frente.** Com ortho size 5, a tela mostra 17,8 × 10 u. O SmoothDamp de 0,2 s atrasa a câmera 2,1 u na horizontal e cerca de 3 u na queda. Em queda rápida, **os pés do personagem ficam fora da tela**.
6. **O atrito padrão (0,4) briga com o controlador.** Não há `PhysicsMaterial2D`. A aceleração efetiva no chão é cerca de **26 u/s²** (o Inspector diz 41), e a velocidade máxima real é cerca de **10,2 u/s** (o Inspector diz 10,5).
7. **A integração com o roguelike tem armadilhas.** O wall slide não depende de `canWallJump`. O `speedYLimit = 20` anula o ganho vertical de "+40 % força do gancho". Mudar `JumpHeight` também muda a gravidade. E o plano pede "sensação idêntica" na tarefa 2.4 ao mesmo tempo em que o movimento vai ser refeito (§8.4).
8. **Com o kit base do plano, Quarta e Quinta não são completáveis** (modelo de alcançabilidade, §5.3). A Quinta exige o pulo duplo. A Quarta exige pulo duplo e wall jump, usando o fato de o wall jump recarregar o pulo aéreo.

---

## 1. Scripts de movimento e relacionados

### 1.1 Componentes do player (`Assets/Prefabs/Cyborg.prefab`)

| Ordem | Componente | Script / GUID | Papel |
|---|---|---|---|
| 1 | Transform | — | `localScale = (1.7, 1.7, 1.3)`: **tudo no player é escalado por 1,7** |
| 2 | Animator | controller `Player_Controller.controller` (`10470c9760779fd4e83cfaab906ca936`) | animações |
| 3 | SpriteRenderer | sprite `CyborgIdle_3`, sorting layer 5, order 1 | visual |
| 4 | Rigidbody2D | — | corpo dinâmico |
| 5 | `characterMovement` | `07cf2a0327132794e97b608e52064c88` | movimento, pulo, parede, morte, moedas |
| 6 | PlayerInput | `62899f850307741f2a39c98a8b639597` (pacote) | liga o `PlayerControls.inputactions` |
| 7 | BoxCollider2D | — | colisão |
| 8 | `CharacterAnimator` | `32ef9bcc0b2b4e14dbdb8ab738b83d7a` | parâmetros do Animator e flip |
| 9 | `GrapplingHook` | `ca30605b75f293240beaee20d178c107` | gancho |
| filhos | `HookTip` (prefab), `GroundCheck`, `RopeVisual` (LineRenderer), `FirePoint` | — | visuais do gancho e ponto do ground check |

O `PlayerData` **não** está mais no prefab: virou singleton DDOL em `8df28c9`. O `CeilingCheck` não existe no prefab; é criado em runtime (`characterMovement.cs:182-188`).

### 1.2 `Assets/scripts/characterMovement.cs` (605 linhas)

**Fluxo por frame**

| Onde | O que faz | Linhas |
|---|---|---|
| `Awake` | guarda os valores base do Inspector (`jumpHeight`, `maxSpeed`, `acceleration`); pega `Player/Movement` e `Player/Jump` do `PlayerInput`; procura `Canvas/GameOverBackground` **só se** `gameOverScreen` for nulo | 108-154 |
| `Start` | `FindPlayerData()` → `LoadPlayerStats()`; lê `defaultGravityScale = rb.gravityScale`; `CalculateJumpVariables()`; cria `GroundCheck`/`CeilingCheck` se faltarem; fallback de `ceilingLayer`/`wallLayer` **só se o valor for 0** | 156-195 |
| `LoadPlayerStats` | `jumpHeight = base + 0,1·strength`; `maxSpeed = base + 0,5·agility`; `acceleration = base + agility`; `enableWallJump = canWallJump`; `maxAirJumps` | 200-226 |
| `Update` | morto/`!canMove` → zera input e sai; lê `Movement` (float); `Jump` pressionado → `desiredJump`, buffer, `pressingJump`; **soltou → `pressingJump = false` e `currentlyJumping = false`**; `onGround = OverlapCircle && vy ≤ 0,1`; `CheckWalls()`; coyote; wall slide; **executa `Jump()`/`WallJump()` aqui mesmo (no Update)** | 261-344 |
| `FixedUpdate` | morto → `v = 0`; `IsGrappling` → `gravityScale = 0` e sai; aceleração horizontal por taxa (ground/air × accel/decel/turn); clamp do wall slide; `OverlapCircle` de teto; **três ramos verticais** (abaixo); hang; clamp simétrico `±speedYLimit`; escreve `rb.linearVelocity` | 346-443 |

**Os três ramos verticais do `FixedUpdate`** são a raiz da maior parte dos problemas:

| Ramo | Condição | Efeito | Linhas |
|---|---|---|---|
| 1 | `currentlyJumping && pressingJump && isGroundJump` | se `transform.position.y ≥ initialJumpY + jumpHeight` **ou** teto → `vy = 0`, liga a gravidade; senão **`vy = jumpSpeed` e `gravityScale = 0`** (subida linear) | 402-415 |
| 2 | `currentlyJumping && !pressingJump && isGroundJump` | **`vy = 0`**; arma `hangTimer` se ele for exatamente 0 | 416-420 |
| 3 | demais casos | `vy *= upwardMovementMultiplier` (subindo) ou `*= downwardMovementMultiplier` (caindo), **por passo**; `gravityScale = default·gravMultiplier` | 421-427 |
| hang | `hangTimer > 0` | `vy = 0`, `gravityScale = 0` | 434-439 |

Outros pontos do arquivo:
- `CalculateJumpVariables` (`:504-510`): `jumpSpeed = h/t` (velocidade **média** de uma subida linear) e `g = 2h/t²` (gravidade de **parábola**). Os dois modelos não batem. O pulo aéreo usa `jumpSpeed × airJumpHeightMultiplier (2)` = `2h/t`, que por coincidência é a velocidade inicial correta de uma parábola.
- `CheckWalls` (`:445-457`): **um** raio de 0,15 u para cada lado, saindo da borda do collider na altura do centro.
- `WallJump` (`:459-471`): `v = (±8, 14)`, `isWallJumping` por 0,3 s, zera `airJumpsUsed`.
- `Jump` (`:473-502`): consome o coyote (`:488`, correção do bug 6), escreve `rb.linearVelocity` **no Update** e dispara `JumpTrigger` ou `DoubleJumpTrigger` direto no Animator (`:495-499`).
- `OnTriggerEnter2D` (`:531-548`): coleta moeda (tag `Coin`) e salva o `PlayerData` na hora.
- `ResetToSpawnPoint` (`:550-566`), `Die` (`:567-594`), `DisableMovement`/`EnableMovement` (`:597-604`): contratos públicos (§8).

### 1.3 `Assets/scripts/GrapplingHook.cs` (293 linhas)

- Máquina de estados `Ready → Shooting → Grappling → Cooldown` (`:6-7`). `IsGrappling` só é verdadeiro em `Grappling` (`:46`).
- **Habilitação**: `playerData.canGrapplingHook`, lida a cada `Update`/`FixedUpdate` (`:77`, `:155`). O `PlayerData` vem de `FindFirstObjectByType` (`:56`).
- **Input**: `Player/Grapple` (tecla G), no `Update` (`:97`).
- **Mira**: automática. `OverlapCircleAll(raio, whatIsGrappleable)` e depois o alvo **mais próximo** com linha de visão (raycast contra `whatIsObstacle`), mirando o `bounds.center` do alvo (`:187-210`). Não usa direção de input.
- **Viagem da ponta**: `MoveTowards` com `Time.deltaTime` no `Update` (`:105`).
- **Puxão**: `rb.linearVelocity = dir · grapplePullSpeed` no `FixedUpdate`, calculado a partir de `transform.position` (`:168-169`). Durante o puxão o `characterMovement` desliga a gravidade e ignora o input (`characterMovement.cs:358-362`).
- **Lançamento**: ao chegar a ≤ `minDistanceToFinish`, zera `v` e aplica `AddForce(launchDirectionVector · launchBoostForce, Impulse)` (`:230-240`). O vetor é o calculado **no disparo**, não o atual.
- **Segurança** (correção do bug 7): timeout de 3 s e detecção de travamento (`:127-148`).
- **Não verifica morte nem `canMove`**: depois de `Die()` ou `DisableMovement()`, apertar G ainda dispara e puxa o jogador.

### 1.4 `Assets/scripts/CharacterAnimator.cs` (77 linhas)

Lê **campos públicos** de `characterMovement` a cada `Update` (`:36-54`):

| Parâmetro | Fonte | Observação |
|---|---|---|
| `Speed` | `abs(currentHorizontalVelocity)` | é o **alvo do script** (`characterMovement.cs:392`), não a velocidade real. Contra uma parede, a animação de corrida toca |
| `VerticalSpeed` | `rb.linearVelocity.y` | — |
| `IsGrounded` | `onGround` | verdadeiro 0,18 u antes de tocar o chão (§2.4) |
| `IsWallSliding` / `IsWallJumping` | campos públicos | `IsWallJumping` não é usado por nenhuma transição |

O flip (`:56-76`) usa `spriteRenderer.flipX` a partir do **input** e força o personagem a olhar para **longe** de qualquer parede tocada, inclusive no chão. Correndo contra uma parede, ele fica de costas para ela.

### 1.5 Câmera: `CameraFollow.cs`, `CameraBoundary.cs`, `ParallaxCamera.cs`

- A câmera que roda nas fases é a do **MainMenu**: `Main Camera` com `CameraFollow` e `DontDestroyOnLoad` (`CameraFollow.cs:26-37`). As cenas do build não têm câmera. O `Main Camera.prefab` (ortho 6, offset z −20) só aparece em cenas fora do build.
- Serializado no MainMenu: `offset = (0, 1.5, −10)`, `smoothTime = 0.2`, `snapToTargetOnStart = 1`, `maxTeleportDistance = 20`, ortho size **5**, sem Pixel Perfect Camera, sem Cinemachine.
- `LateUpdate`: `SmoothDamp` nos dois eixos, com clamp em `CameraBoundary` (`:116-125`). Sem look-ahead, sem zona morta e sem tratamento diferente para o eixo vertical.
- O alvo chega por `SceneController.OnPlayerSpawned` (`:68-74`), com `FindWithTag("Player")` de fallback a cada 0,5 s (`:143-151`).
- `CameraBoundary` (MonoBehaviour com `minX/maxX/minY/maxY` públicos, `CameraBoundary.cs:5-8`). Valores por fase: Primeira `(−20; 88,5) × (0; 54)`, Quarta `(−19,5; 39) × (0; 50)`, Quinta `(2; 60,3) × (−1; 60)`. `FindFirstObjectByType` (`CameraFollow.cs:130`): se uma fase não tiver boundary, os limites da fase anterior continuam valendo.
- `ParallaxCamera` (`ParallaxCamera.cs:15-26`) lê a posição da câmera no `Update`, e a câmera só se move no `LateUpdate`. O parallax anda **um frame atrasado**.

### 1.6 Scripts mortos

| Script | Em uso? | Evidência |
|---|---|---|
| `VerticalJumpController.cs` | **Não.** O GUID `ffcf733e188a55442b9e7e1fab3035d6` não aparece em nenhuma cena ou prefab | usa o Input Manager antigo (`Input.GetButtonDown("Jump")`, `:73`, `:81`; funciona porque `activeInputHandler = 2` (Both)). Curiosamente, usa a fórmula parabólica certa `v0 = 2h/t` (`:182`) |
| `MovementDiagnostic.cs` | **Não** (GUID `adf843128925b7340b588d13e4aa9e0c` sem referências) | classe vazia |
| `Assets/PlayerControls.cs` | **Não é instanciado** em lugar nenhum (`new PlayerControls()` não existe) | wrapper gerado (`generateWrapperCode: 1`); o PlayerInput usa o **asset** `.inputactions`, não a classe |

### 1.7 `Assets/scripts/PlayerData.cs` (stats e habilidades)

Singleton DDOL (`:24-42`), criado automaticamente em `AfterSceneLoad` (`:71-75`) e persistido em PlayerPrefs.

| Campo | Default | Efeito no movimento |
|---|---|---|
| `agility` | 1 | `maxSpeed += 0,5·ag`, `acceleration += ag` |
| `strength` | 1 | `jumpHeight += 0,1·str` |
| `maxAirJumps` | 1 | pulos aéreos |
| `canWallJump` | true | libera **só o pulo** na parede; o deslize acontece sempre (§6, P09) |
| `canGrapplingHook` | true | liga o `GrapplingHook` |

Pela loja (`ShopManager.cs:107-128`): +1 pulo aéreo (até 2 compras), agilidade +3 (até 3 compras), força +3 (até 3 compras). `RefreshStats()` é chamado logo depois da compra (`ShopManager.cs:136`).

### 1.8 Onde outros scripts tocam o player

| Script | O que faz com o player | Linhas |
|---|---|---|
| `SceneController` | instancia `playerPrefab` (Cyborg) no `SpawnPoint` ao carregar os índices 2, 3 e 4; se já houver alguém com a tag `Player`, move esse jogador via `transform.position`. Dispara `OnPlayerSpawned(GameObject)`. Destrói o player no menu e no EndGame | `SceneController.cs:9`, `:70-123` |
| `Timer` | escuta `OnPlayerSpawned` → `GetComponent<characterMovement>()`, com fallback `FindFirstObjectByType`; ao zerar, chama `player.Die()` | `Timer.cs:34-60`, `:124-128` |
| `EnemyBullet` | no `Awake` de **cada bala**: `FindFirstObjectByType<PlayerData>()`, `<characterMovement>()`, `<Timer>()` e um `FindObjectsByType<Transform>(Include)`; ao acertar (tag `Player` ou layer `Player`), chama `Die()`, ativa o GameOver e salva | `EnemyBullet.cs:19-34`, `:82-146` |
| `EndGoal` | `CompareTag("Player")` → `SceneController.NextLevel()` | `EndGoal.cs:25-36` |
| `EnemyAI` | raycast em `playerLayer` para decidir o tiro; colide fisicamente com o player (corpo kinematic, collider sólido) | `EnemyAI.cs:67-85` |
| `Minimap` | `OnPlayerSpawned` + `FindWithTag("Player")`; lê só `transform.position` | `Minimap.cs:46-98` |
| `ShopManager` | `FindFirstObjectByType<characterMovement>()?.RefreshStats()` | `ShopManager.cs:136` |

### 1.9 Input

**Asset em uso pelo player:** `Assets/PlayerControls.inputactions` (GUID `c42fe0711a2b7e644b5e232539b1fb35`), atribuído ao `PlayerInput` do prefab.

| Action | Tipo | Control type | Bindings | Interactions / processors |
|---|---|---|---|---|
| `Movement` | Value | Axis | 1D Axis ←/→ e 1D Axis A/D | nenhum |
| `Jump` | **Value** | **Axis** | Space | nenhum (`WasPressedThisFrame`/`WasReleasedThisFrame` funcionam pelo press point) |
| `Grapple` | Button | — | G | nenhum |

- **Sem gamepad, sem control schemes, sem eixo vertical** (↑/↓ para mira, fast-fall ou dash), sem tecla alternativa de pulo.
- `PlayerInput`: `Behavior = Invoke Unity Events` (2), mapa padrão `Player`. O evento de `Movement` chama `characterMovement.OnMovement`, **que não existe** (listener órfão no `Cyborg.prefab`; o Inspector mostra *Missing*). O script lê as actions por polling.
- **Project-wide actions**: `ProjectSettings/EditorBuildSettings.asset` registra `Assets/InputSystem_Actions.inputactions` (GUID `2bcd2660…`) como `com.unity.input.settings.actions`. Ele tem mapa `Player` completo (Move Vector2 WASD/setas/stick, Jump Space/buttonSouth, Sprint, Crouch, Interact com Hold, Attack…) e `UI`, com control schemes Keyboard&Mouse/Gamepad/Touch/Joystick/XR. É habilitado globalmente, mas **nenhum script o lê**. O `EventSystem` usa as `DefaultInputActions` do pacote.
- `ProjectSettings/ProjectSettings.asset`: `activeInputHandler: 2` (Both). Não há `.inputsettings` (update mode padrão: *Dynamic Update*, ou seja, eventos processados por frame).

### 1.10 Estado dos bugs do RELATORIO_BUGS que tocam o movimento

| # | Bug | Estado atual | Evidência |
|---|---|---|---|
| 1 | Timer/GameOver apontando para o prefab | ✅ Timer corrigido. ⚠️ **Resíduo**: `Cyborg.prefab → characterMovement.gameOverScreen` aponta para o **root do asset `Canvas.prefab`** (`fileID 8525063621205404531`). Como não é nulo, o fallback do `Awake` nunca roda, e `Die()` faz `SetActive(true)` no asset, sem efeito visível. O GameOver só aparece porque Timer e EnemyBullet o ativam por conta própria | `Cyborg.prefab`, `characterMovement.cs:133`, `:575-577` |
| 3 | Três PlayerData | ✅ singleton; removido do Cyborg | `PlayerData.cs:24-97` |
| 5 | `DisableMovement` não fazia nada | ✅ `canMove` é lido (`:264`, `:351`). ⚠️ A gravidade continua agindo (`v` zerado por passo, depois +g → desce 0,76 u/s no ar), e o gancho ignora `canMove`/morte | `characterMovement.cs:351-355`; `GrapplingHook.cs:74-100` |
| 6 | Pulo extra pelo coyote | ✅ `coyoteTimeCounter = 0` no `Jump` e `onGround` exige `vy ≤ 0,1` | `:488`, `:290` |
| 7 | Gancho prendendo para sempre | ✅ timeout e detecção de travamento; sprite do HookTip existe (`Assets/Sprites/upgrades/hooktip.png`) | `GrapplingHook.cs:127-148`, `:177-185` |
| 10 | Upgrades só na próxima fase | ✅ `RefreshStats()` | `:232-237` |
| 12 | Moedas farmáveis | ❌ sem mudança (salva a cada moeda). Some com a decisão D2 | `:533-546` |
| 13 | Não dá para dar Play direto numa fase | ⚠️ parcial. O PlayerData se autocria, mas as fases **não têm câmera nem SceneController**. **Para iterar o feel do movimento é preciso partir do MainMenu** | cenas do build |
| 22 | Código morto | ❌ `VerticalJumpController` e `MovementDiagnostic` continuam no projeto | §1.6 |

---

## 2. Valores reais serializados

### 2.1 `characterMovement` (código × prefab × runtime)

"Runtime" = depois do `LoadPlayerStats` com o `PlayerData` padrão (`agility = 1`, `strength = 1`, `maxAirJumps = 1`, `canWallJump = true`).

| Campo | Default no código | **Prefab** | Runtime efetivo |
|---|---|---|---|
| `maxSpeed` | 10 | **10** | **10,5** |
| `acceleration` | 50 | **40** | **41** |
| `deceleration` | 70 | **30** | 30 |
| `turnSpeed` | 60 | **30** | 30 |
| `airAcceleration` | 30 | **20** | 20 |
| `airDeceleration` | 40 | **30** | 30 |
| `airTurnSpeed` | 25 | **50** | 50 |
| `jumpHeight` | 4 | **2,5** | **2,6** |
| `timeToApex` | 0,4 | **0,37** | 0,37 |
| `upwardMovementMultiplier` | 1 | **1** | 1 (sem efeito) |
| `downwardMovementMultiplier` | 1 | **0,95** | 0,95 **por passo** |
| `coyoteTime` | 0,1 | **0,1** | 0,1 |
| `speedYLimit` | 20 | **20** | 20 (simétrico) |
| `jumpBufferTime` | 0,1 | **0,1** | 0,1 |
| `airJumpHeightMultiplier` | 1 | **2** | 2 |
| `hangTime` | 0,2 | **0,3** | 0,3 (na prática quase nunca atua, P11) |
| `wallCheckDistance` | 0,1 | **0,15** | 0,15 |
| `wallSlideSpeed` | 2 | **2** | 2 |
| `wallJumpForce` | (5, 9) | **(8, 14)** | (8, 14) |
| `wallJumpTime` | 0,2 | **0,3** | 0,3 |
| `wallLayer` | 0 | **64 (Ground)** | 64 |
| `groundCheck` | null | **filho `GroundCheck`** | — |
| `groundCheckRadius` | 0,2 | **0,258** | 0,258 (raio em unidades de mundo, não é escalado) |
| `groundLayer` | 1 | **64 (Ground)** | 64 |
| `ceilingCheck` | null | **null** → criado em runtime | local (−0,05; 0,3) |
| `ceilingCheckRadius` | 0,15 | **0,15** | 0,15 |
| `ceilingLayer` | 1 | **1 (Default)** 🔴 | 1: o fallback só troca se for 0 (`:190`) |
| `rb` | — | Rigidbody2D do próprio objeto | — |
| `gameOverScreen` | — | **asset `Canvas.prefab`** ⚠️ | — |
| `playerData`, `gameController`, `playerAnimator` | — | nulos | resolvidos em runtime |
| `jumpSpeed`, `gravMultiplier`, `defaultGravityScale` | — | 0 (serializados só para debug) | 7,027 · 3,872 · 1 |

> Na cena `QuartaFase.unity` (fora do build, com marcadores de conflito) existe um override `ceilingLayer.m_Bits = 64` na instância do Cyborg. Alguém já corrigiu o teto ali, mas a correção **não chegou ao prefab**, que é o que o build usa.

### 2.2 `GrapplingHook`

| Campo | Código | **Prefab** |
|---|---|---|
| `grappleRadius` | 15 | **9** |
| `grapplePullSpeed` | 20 | **20** |
| `launchBoostForce` | 35 | **25** (impulso; massa 1 → Δv = 25 u/s) |
| `hookTravelSpeed` | 60 | **40** |
| `minDistanceToFinish` | 1,0 | **0,5** |
| `whatIsGrappleable` | 0 | **256 (layer 8 "Gancho")** |
| `whatIsObstacle` | 0 | **64 (Ground)** |
| `grappleCooldown` | 0,5 | **0,5** |
| `maxGrappleDuration` / `stuckCheckInterval` / `stuckDistanceThreshold` | 3 / 0,3 / 0,05 | **3 / 0,3 / 0,05** |
| `firePoint` / `hookTipTransform` / `ropeRenderer` | — | filhos `FirePoint` (local 0,051; −0,012) / `HookTip` / `RopeVisual` |

Alvos: só o filho `Gancho` do `DroneHook.prefab` (layer 8, `CircleCollider2D` trigger r = 0,5 × escala 0,1 = **0,05 u**). Primeira fase: 0 drones · Quarta: 1 · Quinta: 2.

### 2.3 Rigidbody2D

| Propriedade | Valor | Comentário |
|---|---|---|
| Body type | **Dynamic** | — |
| Mass / auto mass | 1 / não | — |
| Linear / angular damping | 0 / 0 | — |
| Gravity scale | **1** no prefab | o script troca por passo entre **0** e **3,872** |
| Material | **nenhum** | atrito padrão 0,4, bounciness 0 (P07) |
| Interpolate | **Interpolate** (1) | ✅, mas o script lê e escreve `transform.position` (P16) |
| Sleeping mode | Start Awake | — |
| Collision detection | **Continuous** | — |
| Constraints | **Freeze Rotation Z** (4) | — |
| Include/Exclude layers | 0 / 0 | — |

### 2.4 Collider e checks, em unidades de mundo (escala 1,7 aplicada)

| Elemento | Local | **Mundo** (relativo ao pivô) |
|---|---|---|
| BoxCollider2D (sem material, edge radius 0, não trigger) | size 0,2997 × 0,7408; offset (−0,0516; −0,0869) | **0,510 × 1,259 u**; centro (−0,088; −0,148); pés em **−0,777**; topo em **+0,482** |
| GroundCheck (`OverlapCircle`) | pos (−0,051; −0,412), r 0,258 | centro (−0,087; −0,700), r **0,258** → alcança **0,18 u abaixo dos pés** |
| CeilingCheck (criado em runtime) | pos (−0,05; 0,3), r 0,15 | centro (−0,085; +0,51) → alcança **0,18 u acima da cabeça**, mas na layer errada |
| Raios de parede | borda do bounds, altura do centro | 1 raio de 0,15 u por lado, na altura −0,148 |
| Sprite | 64 × 64 px, PPU 64, pivô central | **1,7 × 1,7 u** (37,6 px/u; os tiles têm 32 px/u) |

O collider tem um offset horizontal de −0,088 u e **não espelha** com `flipX`. A hitbox se desloca em relação ao desenho quando o personagem vira.

### 2.5 GameObject, render, Animator e PlayerInput

| Item | Valor |
|---|---|
| Layer / tag | **3 "Player"** / **"Player"** (filhos HookTip/RopeVisual/FirePoint também na layer 3; GroundCheck na 3) |
| SpriteRenderer | material URP Sprite-Lit padrão (GUID de pacote), `flipX = 0`, sorting layer id −2073534527, order 1 |
| Animator | `Player_Controller.controller`, update mode Normal, culling Always Animate, sem root motion, `AnimatePhysics = 0` |
| PlayerInput | asset `PlayerControls.inputactions`, *Invoke Unity Events*, listener órfão `OnMovement` |

### 2.6 Instanciação e overrides nas cenas do build

- `PrimeiraFase`, `QuartaFase 1` e `QuintaFase` **não contêm o Cyborg**. Ele é instanciado pelo `SceneController` do MainMenu (`playerPrefab: {fileID: 1227645096870838732, guid: fbe9b19b6ebd70f4183d1a6885db563c}`), com `gameLevelIndexes = [2,3,4]`. **Não há prefab overrides nas fases do build**: valem exatamente os valores do `.prefab`.
- Cenas fora do build com instância do Cyborg: `QuartaFase` (override de `ceilingLayer` = 64), `SextaFase` (só transform) e `TerceiraFase` (overrides **órfãos** de campos do antigo `PlayerData`: `canGrapplingHook`, `canWallJump`, `coinCount`).
- Spawn points (mundo): Primeira (−27,3; 17,6) · Quarta (−28,21; 40,45) · Quinta (−4,93; 8,42).

---

## 3. Configurações de projeto que afetam o movimento

| Configuração | Valor | Impacto |
|---|---|---|
| `TimeManager` Fixed Timestep | **0,02 s (50 Hz)** | lógica por passo (`×0,95`) depende do dt; altura do pulo quantizada em passos de 0,14 u; latência input→física de até 20 ms, mais até 20 ms de interpolação |
| Maximum Allowed Timestep | 0,333 | — |
| `Physics2DSettings` gravity | (0, −9,81) | o script multiplica por `gravityScale = 3,872` → **37,98 u/s²** |
| Velocity / position iterations | 12 / 3 | — |
| Default contact offset | 0,01 | — |
| Simulation mode | **FixedUpdate** (0) | — |
| Sub-stepping | desligado | — |
| **Queries hit triggers** | **1 (sim)** | `OverlapCircle` do teto pega moedas e o EndGoal (triggers na layer Default) |
| Queries start in colliders | 1 | — |
| Auto sync transforms | 0 | teleporte por `transform.position` só vale no próximo step |
| Default material | nenhum | atrito 0,4 em tudo |
| Layer collision matrix | **tudo colide com tudo** (`ffff…`) | player colide com inimigos (layer 10) e com decorações que tenham collider |
| `TagManager`: layers | 0 Default · 3 **Player** · 6 **Ground** · 7 Wall (sem uso) · 8 **Gancho** · 9 Decorations · 10 Enemy | `groundLayer`/`wallLayer`/`whatIsObstacle` = 64 = layer 6 |
| Tags relevantes | `Player`, `SpawnPoint`, `Coin`, `GameOver`, `Ground` | contratos (§8) |
| QualitySettings | Standalone usa o nível 5 (Ultra) → **vSync 1**; `VSyncSettings.cs:17-27`/`:53-80` aplica a preferência salva (e `targetFrameRate = refresh`) só quando a tela de Configurações é aberta | render a 60/144 Hz com física a 50 Hz → interpolação indispensável |
| Pixels per unit | player **64** (× escala 1,7); tiles LDtk **32** (`Assets/Scenes/ldtk/base.ldtk.meta: _pixelsPerUnit: 32`); tileset do grid de colisão 32 | **1 tile = 1 u**; densidade de texel diferente entre player e cenário |
| Pixel Perfect Camera | **não existe** nas cenas do jogo | pixel art com escala 1,7 e câmera sem snap → shimmer |
| Câmera (MainMenu, DDOL) | ortho **5** (10 u de altura; 17,8 u de largura em 16:9), `CameraFollow` smoothTime 0,2, offset (0; 1,5) | §7.7 |

---

## 4. Animator (`Assets/Sprites/PlayerAnimations/Cyborg/Player_Controller.controller`)

### 4.1 Parâmetros

`Speed` (float) · `IsGrounded` (bool) · `VerticalSpeed` (float) · `IsWallJumping` (bool, **sem uso**) · `IsWallSliding` (bool) · `JumpTrigger` (trigger) · `DoubleJumpTrigger` (trigger)

### 4.2 Estados e clipes (default: **Idle**)

| Estado | Clipe | Frames usados / sprites fatiados na sheet | Keys (60 fps) | Duração | Loop |
|---|---|---|---|---|---|
| Idle | `PlayerIdleAnimation.anim` | 4 / 4 (sheet de 7 células) | 0; 0,167; 0,3; 0,467 | 0,48 s | sim |
| Run | `PlayerRunAnimation.anim` (sheet `NewCyborgRunSprite-Sheet.png`) | 6 / 6 | 0; 0,1; 0,25; 0,367; 0,517; 0,617 (espaçamento irregular) | 0,63 s | sim |
| Jump | `PlayerJumpAnimation.anim` | **1** / 4 (um único key em t = 0,333) | — | 0,35 s | não |
| DoubleJump | `DoubleJumpAnimation.anim` | 6 / 6 (cambalhota) | ~0,05–0,07 s por frame | 0,3 s | sim |
| Fall | `PlayerFallAnimation.anim` | **2** / 2 | 0; 0,083 | 0,1 s | não |
| WallSlide | `PlayerWallSlideAnimation.anim` | **1** / 1 (reusa um frame da sheet de pulo) | — | 0,017 s | sim |
| Climb | `PlayerClimbAnimation.anim` | 6 / 6 | 0,083 s por frame | 0,43 s | sim · **inalcançável** (sem transições) |

### 4.3 Transições

| De → Para | Condição | Exit time | Duração |
|---|---|---|---|
| Idle → Fall | `!IsGrounded` | não | 0 |
| Idle → Jump | `JumpTrigger` | não | 0 |
| Idle → Run | `Speed > 0,1` | não | 0 |
| Run → Fall | `!IsGrounded` | não | 0 |
| Run → Jump | `JumpTrigger` | não | 0 |
| Run → Idle | `Speed < 0,1` | não | 0 |
| Jump → Fall | `VerticalSpeed < 0,1` | não | 0 |
| DoubleJump → Fall | `VerticalSpeed < 0,1` | não | 0 |
| Fall → WallSlide | `IsWallSliding` | não | 0 |
| **Fall → Idle** | `IsGrounded` | não | **0,25 s (fixa)** |
| WallSlide → Fall | `!IsWallSliding` | não | 0 |
| WallSlide → Idle | `IsGrounded` | não | 0 |
| Any State → DoubleJump | `DoubleJumpTrigger` (pode ir para si mesmo) | não | 0 |

**Problemas de animação** (detalhados em P12):
- Não há Land nem **Fall → Run**. Pousando em corrida, o fluxo é Fall → Idle (0,25 s de transição, sem interrupção) → Run. A corrida aparece ≥ 0,25 s depois do pouso.
- `JumpTrigger` disparado em Fall (pulo de coyote) **não é consumido**: não existe Fall → Jump. O trigger fica pendurado e toca Jump **no próximo pouso**.
- Em Idle, `!IsGrounded` vem antes de `JumpTrigger` na lista. Dependendo da ordem de execução entre `characterMovement.Update` e `CharacterAnimator.Update` (não definida), a transição vai para Fall em vez de Jump, e o trigger também vaza.
- O wall jump não dispara nenhuma animação (`IsWallJumping` não é usado).

### 4.4 Arte disponível (feedback possível sem arte nova)

| Sheet (`Assets/Sprites/PlayerAnimations/Cyborg/…`) | Tamanho | Conteúdo visível | Uso hoje |
|---|---|---|---|
| `CyborgJump/CyborgJump.png` (= `CyborgFall.png` = `CyborgWallSlide/CyborgJump.png`: **a mesma imagem 3×**) | 448 × 64 (6 frames desenhados) | **ciclo completo de pulo**: antecipação/agachado → impulso → subida → ápice (braço para cima) → queda → pouso | Jump usa 1 frame, Fall 2, WallSlide 1 |
| `CyborgIdle/CyborgIdle.png` | 448 × 64 (4 frames) | respiração | 4 frames |
| `CyborgRunSprite/NewCyborgRunSprite-Sheet.png` | 384 × 64 (6) | corrida | Run |
| `CyborgRunSprite/CyborgRunSprite.png` | 448 × 64 (6) | corrida (versão antiga) | sem uso |
| `CyborgDoubleJump/CyborgDoubleJump-Sheet.png` | 384 × 64 (6) | cambalhota | DoubleJump |
| `CyborgClimb/CyborgClimb.png` | 448 × 64 (6) | escalada (braços alternando) | inalcançável |

Com a arte atual dá para fazer: antecipação de pulo, subida/ápice/queda separados, **pouso** (último frame da sheet de pulo), escalada/wall grab (sheet Climb), cambalhota do pulo duplo, e squash & stretch/partículas por código. **Não existem**: frame dedicado de wall slide, dash, turn/skid, hit/morte, gancho (pose de disparo).

---

## 5. Níveis e colisão

### 5.1 Pipeline

- As fases vêm do **LDtk** (`Assets/Scenes/ldtk/base.ldtk`, grid **32 px**, PPU **32** → **1 tile = 1 u**), importadas pelo LDtk-Unity com `_useCompositeCollider: 1` e `_geometryType: 0` (**Outlines**, ou seja, contornos/arestas e não polígonos preenchidos).
- Cada fase é uma instância do prefab importado do `.ldtkl` (`World_Level_4` na Primeira, `Quarta_Fase`, `Quinta_Fase`), com **milhares de overrides de tiles e de `m_CompositePaths` na cena**: foram editadas depois do import.
- Colisão: layer IntGrid → `Tilemap` + `TilemapCollider2D` (usado pelo composite) + **`CompositeCollider2D`** + `Rigidbody2D` **Static** (override `m_BodyType: 2`), na **layer 6 (Ground)**. O tile de colisão (`1_Industrial_Tileset_1B_1.asset`) tem `m_ColliderType: 1` (Sprite), com sprite de 32 × 32 px a PPU 32.
- Sem `PhysicsMaterial2D` nos colliders visíveis na cena. O que o importador gera fica na Library, não versionado; por padrão não há material.
- **Paredes invisíveis na layer Default**: `Right Wall` e `Up Wall` (Primeira), `Right Wall` e `Left Wall` (Quarta), `Lateral` × 2 (Quinta). **Não contam como chão nem como parede** para os checks (que usam a layer 6), mas **contam como teto** (`ceilingLayer = Default`).
- **PrimeiraFase**: o composite tem override `m_Offset.y = −0,25`. As superfícies de colisão ficam 0,25 u abaixo do grid de tiles. Quarta e Quinta não têm esse offset, então a "altura do pé" em relação ao cenário muda de fase para fase.
- **Não existem**: one-way platforms (nenhum `PlatformEffector2D`), plataformas móveis, rampas (0 arestas diagonais nos composites), espinhos ou hazards, kill-zones de queda. Plataforma fina especial: a placa `hotel-sign/collider` na Quarta (Box 2,26 × 0,15 u, sólida, layer 6).

### 5.2 Geometria por fase

Reconstruída dos `m_CompositePaths` serializados (coordenadas locais ao nível; tudo alinhado ao grid de 1 u).

| Fase (build idx) | Tamanho (tiles) | Spawn (local) → Goal (local) | Elementos | Degraus (arestas verticais mais comuns) | Pisos (arestas horizontais) |
|---|---|---|---|---|---|
| PrimeiraFase (2) | 128 × 66 | (3; 24) → (126; 25): **corrida horizontal** | 1 inimigo, 3 moedas, 0 drones | 1 u (90×), 2 u (16×), 3 u (5×), 4 u (6×); paredes de 24–36 u | 1–4 u, e trechos de 60 u e 86 u |
| QuartaFase 1 (3) | 80 × 56 | (1; 50) → (77; 50): **mesma altura, mas separada por uma coluna de 5 × 38 u** | 1 drone, 0 inimigos | 1 u (63×), 2 u (23×), 3 u (8×), 4 u (5×); chaminé de **3 u de largura × ~30 u** logo após o spawn | 1–5 u |
| QuintaFase (4) | 80 × 50 | (4; 15) → (76; 51): **subida de ~36 u** | 2 drones, 3 inimigos, 20 moedas | 1 u (105×), 2 u (22×), 3 u (10×); paredes de 9–13 u e 36–50 u | 1–8 u, alguns de 11–18 u |

Padrões observados:
- **Primeira**: um corredor que termina numa escadaria de degraus de 1 u, seguida de 4 pilares de 4 u de largura separados por **vãos de 3 u** que descem até o fundo do nível (sem piso nem kill-zone). Quem cai num vão fica preso até o timer acabar. Depois vem uma sequência de plataformas e o goal.
- **Quarta**: spawn e goal no topo, na mesma altura (y ≈ 50), separados por um pilar de 5 × 38 u e um bloco de teto (x 9–20, y 54–56). Por baixo, uma chaminé de 3 u de largura desce ~30 u até um corredor no fundo. Do corredor se sobe pela direita até uma sala (x 45–69, y 20–41) cujas plataformas ficam 9–11 u acima umas das outras. No topo há um poço de 4 × 7 u (x 56–59, y 42–48). O drone do gancho fica embaixo, em (60; 17), e não ajuda na subida. A rota que o modelo acha (§5.3) passa **por cima** do bloco do teto, perto do spawn.
- **Quinta**: subida contínua em "zigue-zague" com plataformas de 2 a 8 u, degraus de até 3 u e drones perto do meio.

### 5.3 Alcançabilidade aproximada

Rodei uma busca em largura sobre as superfícies onde dá para ficar em pé. A partir de cada plataforma, o modelo simula saltos e quedas com **a lógica real do `characterMovement`**: subida linear, corte, arrasto de 0,95, aceleração no ar, wall slide, wall jump (8; 14) com trava de 0,3 s, pulo aéreo de 14,05 u/s, teto que "gruda" (P04) e gancho com lançamento de 25 u/s. A colisão usa a caixa real de 0,51 × 1,26 u contra a grade reconstruída. O conjunto de estratégias é finito: segurar 1, 8, 19 ou 30 passos; direções com trocas em momentos fixos; pulo aéreo em momentos fixos; wall jump "para longe" ou "volta para a parede e usa o pulo aéreo recarregado". Por isso o resultado é **indicativo**, com possíveis falsos negativos (rotas criativas) e falsos positivos (timings muito precisos). As paredes invisíveis de borda (layer Default) não contam para o wall slide, como no jogo.

| Fase | Kit base (sem pulo aéreo, wall jump e gancho) | + truque P02 | + só wall jump | + só gancho | + só 1 pulo aéreo | Kit atual (1 aéreo + wall jump, com ou sem gancho) | Altura de pulo que o kit base precisaria (mesmo modelo) |
|---|---|---|---|---|---|---|---|
| **PrimeiraFase** | ✅ | ✅ | ✅ | (sem drones) | ✅ | ✅ | a atual (2,6) basta |
| **QuartaFase 1** | ❌ desce e não volta a subir | ❌ | ❌ | ❌ | ❌ | ✅ **só com a sequência wall jump → pulo aéreo recarregado** (`airJumpsUsed = 0` no `WallJump`, `:464`), subindo por cima do bloco do teto (y ≈ 56) | ≈ **6 u** (5 u ainda falha) |
| **QuintaFase** | ❌ para em y ≈ 30–32 (goal em 51) | ❌ (32) | ❌ (45) | ❌ (30) | ✅ | ✅ | ≈ **4 u** (3,5 u ainda falha) |

Conclusões para o refactor e para a decisão D1 do plano:
- Com o controlador atual, **Quarta e Quinta não são completáveis com o kit base** que o plano propõe (`MaxAirJumps = 0`, sem wall grab, sem gancho). A Quinta pede o pulo duplo. A Quarta pede pulo duplo **mais** wall jump, e ainda uma interação específica (o wall jump recarrega o pulo aéreo) que o controlador novo precisa **preservar ou substituir** conscientemente.
- O gancho, com os drones nas posições atuais, **não é necessário** em nenhuma fase pelo modelo.
- Se a D1 for mantida ("toda fase completável com o kit base"), sobram três caminhos: pulo base bem mais alto (≈ 4 u para a Quinta; ≈ 6 u para a Quarta, o que muda o caráter do jogo); editar a geometria (degrau intermediário no trecho de mais de 3,5 u da Quinta e na subida da Quarta); ou garantir mobilidade nas primeiras ofertas (a alternativa já citada na D1).
- **Vãos horizontais** não são o gargalo: o maior vão obrigatório observado é de 3–4 u, e o pulo atual cobre 8,6 u na horizontal. O gargalo é **vertical**. Na Quinta há um trecho que exige subir mais de 3,5 u de uma vez. Na Quarta, a saída do spawn exige ≈ 6 u (por cima do bloco do teto), e a sala da direita tem plataformas 9–11 u acima umas das outras.

---

## 6. Problemas de feel e de código

| ID | Sev. | Onde | O que acontece | O que o jogador sente |
|---|---|---|---|---|
| **P01** | 🔴 | `characterMovement.cs:402-415`, `:506` | O pulo do chão sobe em **velocidade constante** (`jumpSpeed = h/t = 7,03 u/s`) com **gravidade 0** enquanto o botão está pressionado. Ao atingir `jumpHeight`, `vy` vai de 7,03 para **0 em um passo** (desaceleração equivalente de ~350 u/s²). Depois cai com 37,98 u/s². O `gravityScale` alterna entre 0 e 3,872 conforme o ramo. A subida linear em si existe em jogos bons (o Celeste segura a velocidade por até 0,2 s); o problema é **não ter a fase de desaceleração por gravidade**, com o corte seco no lugar do arco | "elevador", "bati num teto invisível". Ápice sem arco, subida lenta e queda rápida (0,38 s subindo 2,67 u; 0,44 s caindo a mesma altura, chegando a 10 u/s). Difícil de prever onde se vai pousar |
| **P02** | 🔴 | `:283-287` + `:402-427` | Soltar o botão durante a subida troca para o ramo 3 com `vy = 7,03` intacto → +0,65 u balísticos. Soltar **logo antes** do corte (0,36–0,38 s) dá **3,11–3,25 u**; segurar até o fim dá **2,67 u** | "segurar mais = pular menos"; alturas inconsistentes; a altura máxima vira truque de frame (e sobe 3 tiles, o que o design não prevê) |
| **P03** | 🔴 | `:416-420`, `:434-439`, `:419/:431/:436` | Quando `Jump()` roda com o botão **já solto** (tap curto dentro do jump buffer antes de pousar, ou press+release no mesmo frame), cai no ramo 2: `vy = 0` **a cada passo** até algo limpar `currentlyJumping`, o que só acontece em um novo press+release. Na 1ª ocorrência também liga o hang (0,32 s sem gravidade). Depois o `hangTimer` fica em −0,02 para sempre (float nunca volta a 0), e o personagem **anda no ar** ao sair de plataformas: flutua, ou desce a 0,76 u/s se a gravidade estiver ligada | "o jogo comeu meu pulo", "fiquei flutuando". No kit base (`MaxAirJumps = 0`) acontece **em todo pulo bufferizado com tap** |
| **P04** | 🔴 | prefab `ceilingLayer = 1`; `:190`; `:400-415` | O teto (tiles, layer 6) **não é detectado**. Segurando pulo embaixo de um teto, o ramo 1 continua impondo `vy = 7,03` e `g = 0` → o jogador **gruda no teto** até soltar. Como `queriesHitTriggers = 1`, **moedas e o EndGoal (triggers na Default)** e as paredes invisíveis contam como teto: o pulo morre 0,18 u antes de pegar a moeda. Não há corner correction | "fiquei preso no teto", "o pulo morreu do nada perto da moeda"; bater a quina da cabeça num canto por 1 px trava a subida |
| **P05** | 🔴 | `CameraFollow.cs:9-10`, `:116-125`; câmera do MainMenu | Ortho 5 (tela de **17,8 × 10 u**), SmoothDamp 0,2 s nos dois eixos, offset +1,5 u, sem look-ahead. Atraso em regime ≈ `v · smoothTime`: **2,1 u** a 10,5 u/s → só **6,8 u (0,65 s)** de visão à frente. Na queda a 15,2 u/s: **3,0 u** de atraso + 1,5 de offset → pivô a **0,46 u da borda inferior**, pés fora da tela. A câmera também sobe e desce **a cada pulo** | "pulando no escuro", quedas cegas, câmera "balançando" |
| **P06** | 🟠 | `:423-424`, `:441` | A queda usa `vy *= 0,95` **por FixedUpdate** (arrasto dependente do passo): velocidade terminal **15,19 u/s** (= g·dt/0,05); `speedYLimit = 20` nunca atua na queda. Se o timestep mudar para 0,01, a terminal vira ~7,6 u/s e todas as quedas mudam. Não existe "fall gravity" nem gravidade no ápice ajustáveis | queda "melosa" no começo e sem teto claro; ajuste frágil (mexer no timestep muda o jogo) |
| **P07** | 🟠 | Rigidbody2D/BoxCollider2D sem material; `:364-392` | O script define a velocidade e o solver aplica **atrito 0,4** do chão (impulso ≈ μ·g·dt = 0,30 u/s por passo): aceleração efetiva **~25,8 u/s²** (Inspector: 41), velocidade real **~10,2 u/s** (Inspector: 10,5), frenagem ~45 u/s². Contra paredes, o atrito freia `vy` ao empurrar contra elas (*estimado*) | aceleração "lamacenta"; os números do Inspector não correspondem ao que se sente; grudar levemente em paredes |
| **P08** | 🟠 | `:9-17`, `:370-387` | `turnSpeed 30 < acceleration 41`: inverter no chão leva **~0,66 s**. No ar: `airAcceleration 20` (0 → máx em **0,53 s**), inverter leva **0,72 s**. Soltando o direcional, o personagem desliza **1,05 u** no chão e **1,84 u** no ar | "sabonete", difícil corrigir no ar, overshoot em plataformas de 1–2 u |
| **P09** | 🟠 | `:306-313`, `:324`, `:389`, `:395-398`, `:445-457` | Wall jump **só enquanto desliza** (`vy < 0`); **trava 100 % do input horizontal por 0,3 s**; impulso fixo (8; 14) sem variante neutra/escalada; o deslize ativa **sem segurar em direção à parede** e **mesmo sem a habilidade** (o clamp não checa `enableWallJump`). Detecção por **1 raio** no meio do corpo | "o jogo roubou o controle"; paredes "grudentas" ao passar raspando; wall jump falha perto de quinas e quando ainda está subindo |
| **P10** | 🟠 | `:273-344` (Update) × `:346-443` (Fixed); `TimeManager` 0,02 | Física a **50 Hz** com render a 60–144 Hz: input aplicado em passos de 20 ms, mais até 20 ms de atraso da interpolação. O pulo é executado no **Update**, escrevendo `rb.linearVelocity` fora do passo. Por isso **não há perda de press**, mas a lógica fica dividida entre dois relógios. Altura quantizada em 0,14 u | resposta "um pouco mole"; pequenas variações de altura entre pulos |
| **P11** | 🟠 | `:367`, `:429-432`, `:436` | `previousVelocityY` recebe a velocidade **atual** antes das alterações, então a condição de apex hang do pulo aéreo nunca é verdadeira; `hangTimer` nunca volta a 0. **O apex hang não existe na prática**; só aparece no caminho bugado de P03 | ápice sem "respiro"; pulo duplo sem controle fino no topo |
| **P12** | 🟠 | `Player_Controller.controller`; `characterMovement.cs:495-499`; `CharacterAnimator.cs:36-52` | Fall → Idle com **0,25 s fixos** e sem Fall → Run/Land; Jump de **1 frame**; `JumpTrigger` vaza no Fall (coyote) e toca Jump no pouso; ordem de transição do Idle favorece Fall; `IsGrounded` fica verdadeiro 0,18 u antes do contato | o boneco "atrasa" em relação ao controle; glitches no pouso; o pulo parece "sem impulso" |
| **P13** | 🟠 | `Assets/PlayerControls.inputactions` | Só teclado (←/→, A/D, Space, G). **Sem gamepad**, sem eixo vertical (mira, fast-fall, dash), sem tecla alternativa de pulo | impossível jogar com controle; um jogo de plataforma de precisão sem gamepad perde muito |
| **P14** | 🟠 | `GrapplingHook.cs:168-169`, `:187-210`, `:230-240`; `characterMovement.cs:358-362`, `:441` | Mira automática no alvo mais próximo; puxão em velocidade fixa ignorando o input; lançamento no vetor **do disparo**; o componente vertical é cortado em **20 u/s** por `speedYLimit` (impulso de 25). O gancho continua funcionando depois de `Die()`/`DisableMovement()` | gancho "trilho", pouca expressão; o upgrade de força não se sente na vertical |
| **P15** | 🟡 | `:404`, `:475`, `GrapplingHook.cs:168`; `:558`, `SceneController.cs:111` | Com interpolação ligada, `transform.position` não é igual a `rb.position` no Update/FixedUpdate: altura-alvo e direção do gancho medidas pela pose interpolada; teleportes por `transform.position` (em vez de `rb.position`/`Teleport`) | variações pequenas de altura; um frame de "lerp" no respawn |
| **P16** | 🟡 | `CharacterAnimator.cs:56-76` | Flip por **input** (não por velocidade) e forçado para **longe** de qualquer parede tocada, inclusive no chão. Hitbox com offset −0,088 u que não espelha | corre "de costas" contra paredes; a hitbox parece mudar ao virar |
| **P17** | 🟡 | `:392`; `CharacterAnimator.cs:36` | `Speed` = alvo do script, não a velocidade real | anima corrida parado contra a parede |
| **P18** | 🟡 | `:110-153`, `:242-259`, `GrapplingHook.cs:56`, `EnemyBullet.cs:19-34`, `Timer.cs:57`, `ShopManager.cs:136`; prefab `gameOverScreen` | Acoplamento: `FindFirstObjectByType`, `GameObject.Find("Canvas")`, campos públicos lidos por outro script, `gameOverScreen` → asset `Canvas.prefab`, listener órfão `OnMovement`. `EnemyBullet` varre todos os Transforms e faz `Debug.Log` **a cada bala** | pequenos engasgos a cada tiro; fragilidade (dificulta o refactor) |
| **P19** | 🟡 | sprites (PPU 64 × 1,7) vs tiles (PPU 32); sem Pixel Perfect; `ParallaxCamera.cs:15-26` | Densidade de texel diferente e escala não inteira; câmera sem snap; parallax 1 frame atrasado. Obs.: o Rigidbody2D **já usa Interpolate** e a câmera segue no `LateUpdate`, então **não** há jitter de câmera por falta de interpolação | shimmer da pixel art em movimento; parallax tremendo |
| **P20** | 🟡 | cena `PrimeiraFase` (composite `m_Offset.y = −0,25`) | Superfícies 0,25 u abaixo do grid só nessa fase | pé "afundando" ou "flutuando" conforme a fase |
| **P21** | 🟡 | níveis; `:550-566` sem chamadores | Não há kill-zone nem respawn; `ResetToSpawnPoint` não é chamado por ninguém | cair num vão = esperar o timer acabar |
| **P22** | 🟡 | `:504-510`, `LoadPlayerStats :210` | `g = 2h/t²`: **aumentar a altura do pulo aumenta a gravidade** (e a velocidade de queda terminal). `airJumpHeightMultiplier = 2` é um remendo que compensa `jumpSpeed = h/t` | upgrade de altura deixa a queda mais pesada; o tuning fica acoplado |
| **P23** | 🟡 | `:351-355` | `canMove = false`/morte zera `v` por passo, mas a gravidade segue ligada | o corpo "desce devagar" durante cutscenes ou a tela de morte |
| **P24** | 🟡 | player sem AudioSource/partículas | Sem SFX de pulo/pouso, poeira, squash & stretch, screen shake, hit-stop | pouco retorno, ações "secas" |
| **P25** | 🟡 | `:290`, `:44-46`; §2.4 | Chão por `OverlapCircle` (r 0,258) lido no `Update` a partir do `GroundCheck` (transform interpolado): acusa chão 0,18 u **antes** do contato, não considera inimigos nem a layer Default, e o raio praticamente iguala a largura do collider (sem folga nem margem de borda) | pouso "antecipado" na animação; bordas de plataforma sem perdão além do coyote |

---

## 7. Métricas derivadas (valores reais, `dt = 0,02`)

Constantes efetivas: `h = 2,5 + 0,1·1 = 2,6 u` · `tApex = 0,37 s` · `maxSpeed = 10 + 0,5·1 = 10,5 u/s` · `accel = 40 + 1 = 41 u/s²`.

### 7.1 Gravidade e velocidades de pulo

| Grandeza | Conta | Valor |
|---|---|---|
| Velocidade de subida (ramo 1) | `h / tApex = 2,6 / 0,37` | **7,027 u/s** |
| Gravidade efetiva | `2h / tApex² = 5,2 / 0,1369` | **37,98 u/s²** (3,87 g) |
| `gravMultiplier` (= gravityScale fora do pulo) | `37,98 / 9,81 / 1` | **3,872** |
| Gravidade por passo | `37,98 · 0,02` | 0,760 u/s por passo |
| Velocidade do pulo aéreo | `7,027 · 2` | **14,05 u/s** (= `2h/t`, o `v0` de uma parábola) |
| Parábola "ideal" com os mesmos h e t | `v0 = 2h/t` | 14,05 u/s; tempo de voo `2t` = 0,74 s |

### 7.2 Pulo do chão (implementação real, simulada)

| Tempo segurando | Ápice | Tempo até o ápice | Tempo no ar (plano) | Distância a 10,5 u/s |
|---|---|---|---|---|
| 1 passo (tap, 0,02 s) | 0,72 u | 0,20 s | 0,42 s | 4,4 u |
| 0,10 s | 1,28 u | 0,28 s | 0,58 s | 6,1 u |
| 0,20 s | 1,99 u | 0,38 s | 0,76 s | 8,0 u |
| 0,30 s | 2,69 u | 0,48 s | 0,92 s | 9,7 u |
| **0,36–0,38 s** (solta logo antes do corte) | **3,11–3,25 u** | 0,54–0,56 s | 1,02–1,06 s | **10,7–11,1 u** |
| **segurando até o fim** (corte em 19 passos) | **2,67 u** | **0,38 s** | **0,82 s** | **8,6 u** |

Contas do caso "segurando": `7,027 · 0,02 = 0,1405 u/passo` → `⌈2,6 / 0,1405⌉ = 19 passos` → `y = 2,670 u` em `t = 0,38 s`. Depois disso, `vy` vai a 0 e começa a queda (§7.3). Na queda de 2,67 u gastam-se 0,44 s, e o pouso acontece a **−10,3 u/s**. Faixa útil do pulo variável: 0,72 → 2,67 u (razão 3,7:1). Faixa com o truque: até 3,25 u.

### 7.3 Queda

| Grandeza | Conta | Valor |
|---|---|---|
| Velocidade terminal | `v* = −g·dt / (1 − 0,95) = −0,760 / 0,05` | **−15,19 u/s** (o clamp de 20 nunca atua) |
| Cair 2,6 u / 5 u / 10 u / 20 u / 30 u | simulação | 0,44 / 0,64 / 1,02 / 1,70 / 2,36 s (sem arrasto seria 0,37 / 0,51 / 0,73 / 1,03 / 1,26 s) |
| Velocidade depois de 0,2 / 0,4 / 1,0 s | simulação | −6,1 / −9,8 / −14,0 u/s |
| Wall slide | `wallSlideSpeed` | **2 u/s** (0,5 s por tile) |

### 7.4 Horizontal

| Grandeza | Sem atrito (valor de Inspector) | **Com atrito padrão 0,4** (*estimado*) |
|---|---|---|
| Velocidade máxima no chão | 10,5 u/s | **~10,2 u/s** |
| 0 → máx no chão | `10,5 / 41` = **0,26 s** (1,49 u) | 90 % em **0,36 s**, 99 % em 0,40 s (ganho líquido `0,82 − 0,30 = 0,52 u/s` por passo ≈ 25,8 u/s²) |
| Parar no chão (soltando) | `10,5 / 30` = 0,35 s · `10,5² / 60` = 1,84 u | **0,24 s · 1,05 u** (30 + 15,2 u/s²) |
| Inverter no chão (+máx → −máx) | 0,62 s (turn 30, depois accel 41) | **0,66 s** |
| 0 → máx no ar | `10,5 / 20` = **0,53 s** | igual (sem contato) |
| Parar no ar (soltando) | `10,5 / 30` = 0,35 s · 1,84 u | igual |
| Inverter no ar | 0,72 s (turn 50, depois accel 20) | igual |
| Coyote a velocidade máxima | `0,1 s · 10,2` | ~1,0 u além da borda |

Atrito: impulso máximo por passo = `μ · (g·dt) = 0,4 · 0,760 = 0,304 u/s` → equivalente a **15,2 u/s²** contra o movimento sempre que o personagem "escorrega" sobre o chão, ou seja, sempre, já que o script impõe a velocidade.

### 7.5 Pulo aéreo, parede e combinações

| Grandeza | Valor |
|---|---|
| Pulo aéreo (`v0 = 14,05`, balístico) | **+2,46 u** em 0,36 s (o contínuo seria 2,6; o Euler discreto perde ~`v0·dt/2`) |
| Altura máxima com 1 pulo aéreo no ápice | 2,67 + 2,46 ≈ **5,1 u** (≈ 5,7 u com o truque de P02) |
| Wall jump `(8; 14)` | ápice **+2,44 u** em 0,36 s; no fim da trava (0,3 s): **x = 2,40 u**, y = +2,38 u, `vy = +2,6 u/s` |
| Chaminé de 3 u (vão livre 3 − 0,51 = 2,49 u) | atravessa em 0,31 s (≈ fim da trava), ainda subindo; precisa esperar `vy < 0` (≈ 0,06 s) para o próximo wall jump; ganho ≈ 2,4 u por salto |

### 7.6 Gancho

| Grandeza | Conta | Valor |
|---|---|---|
| Alcance | `grappleRadius` | **9 u** (mira automática no alvo visível mais próximo) |
| Viagem da ponta | `9 / 40` | até **0,225 s** |
| Puxão | `(9 − 0,5) / 20` | até **0,425 s** → total até o lançamento ≈ **0,65 s** |
| Lançamento | impulso 25 / massa 1 | **Δv = 25 u/s** na direção do disparo |
| Lançamento vertical puro | clamp `speedYLimit` | 20 u/s → **+5,1 u** acima do ponto de soltura (5,27 u contínuo) |
| Lançamento horizontal puro | segurando para frente: decai a 20 u/s² até 10,5 | 0,73 s, **~12,9 u**; soltando: decai a 30 u/s² até 0 (0,83 s, ~10,4 u) |
| Cooldown / timeout / travamento | — | 0,5 s / 3 s / < 0,05 u em 0,3 s |

### 7.7 Câmera

| Grandeza | Conta | Valor |
|---|---|---|
| Área visível | `2 · 5 = 10` u de altura; `10 · 16/9` | **17,8 × 10 u** (tiles) |
| Atraso horizontal a 10,5 u/s | SmoothDamp ≈ mola criticamente amortecida com `ω = 2/smoothTime`; erro em regime `= v · smoothTime` | **2,1 u** → visão à frente `8,9 − 2,1 = 6,8 u` = **0,65 s** |
| Atraso na queda terminal | `15,19 · 0,2` | **3,0 u** → pivô `1,5 + 3,0 = 4,5 u` abaixo do centro (a tela vai até 5) |
| Atraso na subida do pulo | `7,03 · 0,2` | 1,4 u |
| Tempo para atravessar meia tela | `8,9 / 10,5` | 0,85 s (no Celeste, ≈ 1,8 s) |

### 7.8 Referência: Celeste convertido para tiles

Constantes do `Player.cs` do Celeste (8 px = 1 tile) para calibrar a conversa. **Não são alvo**: a câmera e o tamanho do personagem aqui são outros.

| Grandeza | Celeste (px) | Celeste (tiles) | Cyborg hoje (u = tiles) |
|---|---|---|---|
| Velocidade máx. de corrida | 90 px/s | 11,25 t/s | 10,5 (10,2 real) |
| Aceleração de corrida | 1000 px/s² | 125 t/s² (0 → máx em 0,09 s) | 41 (≈ 26 real) |
| Multiplicador no ar | ×0,65 | 81 t/s² | 20 t/s² (×0,49) |
| Gravidade | 900 px/s² (metade perto do ápice se segurar pulo: `abs(vy) < 40 px/s`) | 112,5 t/s² | 38 t/s² |
| Velocidade de pulo / tempo de pulo variável | 105 px/s por até 0,2 s | 13,1 t/s | 7,03 t/s até o corte |
| Queda máx. (normal / segurando ↓) | 160 / 240 px/s | 20 / 30 t/s | 15,2 (arrasto) |
| Coyote / buffer | 0,1 s / ~0,08 s | — | 0,1 / 0,1 |
| Trava do wall jump / velocidade horizontal | 0,16 s / 130 px/s | 16,25 t/s | 0,3 s / 8 t/s |
| Corner correction (subida) | 4 px | 0,5 t | nenhuma |
| Altura do pulo (derivada) | ≈ 28 px | ≈ 3,5 t | 2,67 |
| Área visível | 320 × 180 px | 40 × 22,5 t | 17,8 × 10 |

---

## 8. Pontos de integração e contratos que o novo controlador precisa respeitar

### 8.1 Quem chama o quê no player

| Chamador | Chamada / leitura | Onde |
|---|---|---|
| `Timer` | `GetComponent<characterMovement>()` no `OnPlayerSpawned`; `FindFirstObjectByType<characterMovement>()`; `.Die()` | `Timer.cs:48`, `:57`, `:127` |
| `EnemyBullet` | `FindFirstObjectByType<characterMovement>()`; `.Die()`; detecção por **tag `Player` ou layer `Player`** | `EnemyBullet.cs:23`, `:84`, `:117` |
| `ShopManager` | `FindFirstObjectByType<characterMovement>()?.RefreshStats()` | `ShopManager.cs:136` |
| `CharacterAnimator` | `GetComponent<characterMovement>()`; lê `currentHorizontalVelocity`, `rb`, `onGround`, `isWallSliding`, `isWallJumping`, `horizontalInput`, `isTouchingRightWall`, `isTouchingLeftWall` | `CharacterAnimator.cs:9`, `:36-75` |
| `characterMovement` → Animator | `SetTrigger("JumpTrigger" / "DoubleJumpTrigger")` direto | `characterMovement.cs:497-498` |
| `characterMovement` → `GrapplingHook` | `IsGrappling`, `CancelGrapple()` | `:358`, `:587` |
| `GrapplingHook` → `Rigidbody2D` | escreve `linearVelocity` e `AddForce` | `GrapplingHook.cs:169`, `:232-233` |
| `EnemyAI` | raycast em `playerLayer` (layer 3) | `EnemyAI.cs:71-76` |
| `EndGoal`, `SceneController`, `CameraFollow`, `Minimap` | **tag `Player`** (`CompareTag`, `FindWithTag`, `FindGameObjectsWithTag`) | `EndGoal.cs:31`; `SceneController.cs:73`, `:118`; `CameraFollow.cs:145`; `Minimap.cs:87` |
| `SceneController` | `Instantiate(playerPrefab)` na posição/rotação do `SpawnPoint` (tag `SpawnPoint`); reposiciona o player via `transform.position` | `SceneController.cs:86-114` |
| **ninguém** | `DisableMovement()`, `EnableMovement()`, `ResetToSpawnPoint()`: API pública **sem chamadores** hoje, mas o plano vai precisar delas | — |

### 8.2 Eventos

- `SceneController.OnPlayerSpawned : Action<GameObject>` (`SceneController.cs:9`). É disparado **também quando o player já existia** (`:82`). Ouvintes: `CameraFollow`, `Timer`, `Minimap`.
- O plano (§3.4) migra para `PlayerSpawned`, `PlayerDied` (emissor: characterMovement), `LevelTimeExpired` (ouvinte: characterMovement) e `PlayerStatsChanged` (ouvintes: characterMovement e GrapplingHook).

### 8.3 Dependências de cena, prefab e asset

- O `playerPrefab` do `SceneController` no **MainMenu** referencia `Cyborg.prefab` por GUID `fbe9b19b6ebd70f4183d1a6885db563c` e **fileID do GameObject raiz** `1227645096870838732`.
- O PlayerInput do prefab referencia `PlayerControls.inputactions` pelo GUID `c42fe0711a2b7e644b5e232539b1fb35`; o script busca as actions **por nome** (`"Player"`, `"Movement"`, `"Jump"`, `"Grapple"`).
- As layers **6 (Ground)** e **8 (Gancho)** e a tag `Player` são contratos implícitos com as fases, os inimigos e os drones.

### 8.4 O que o plano roguelike espera (Etapa 2) × o que existe

| `StatType` / flag | Campo atual | Observação para o refactor |
|---|---|---|
| `MaxSpeed` | `maxSpeed` (10 + 0,5·agility) | valor-base "de hoje" = **10,5** (10,2 real com atrito) |
| `Acceleration` | `acceleration` (40 + agility) | só a aceleração no chão; `deceleration` e `turnSpeed` **não** são stats. "+15 % aceleração" pouco muda enquanto o atrito comer 15 u/s² |
| `AirAcceleration` | `airAcceleration` (20) | não afeta `airDeceleration` (30) nem `airTurnSpeed` (50). "Controle Aéreo +20 %" só melhora a aceleração no mesmo sentido |
| `JumpHeight` | `jumpHeight` (2,5 + 0,1·strength) | com `tApex` fixo, **g = 2h/t² sobe junto** (P22). Decidir se o upgrade mantém a gravidade ou o tempo |
| `CoyoteTime` | `coyoteTime` (0,1) | ok |
| `WallSlideSpeed` | `wallSlideSpeed` (2) | ok |
| `MaxAirJumps` | `PlayerData.maxAirJumps` (1) | kit base = **0**. Com 0, o bug P03 vira rotina, e Quarta e Quinta ficam impossíveis pelo modelo da §5.3 |
| `GrappleRadius` | `GrapplingHook.grappleRadius` (9) | ok |
| `GrappleCooldown` | `grappleCooldown` (0,5) | ok |
| `GrappleLaunchForce` | `launchBoostForce` (25) | o clamp **`speedYLimit = 20`** já corta o vertical: **"+40 %" (35) não muda nada na vertical** |
| `AbilityFlags.WallGrab` | `PlayerData.canWallJump` → `enableWallJump` | hoje só libera o **pulo**; o **deslize acontece sem a flag** (`:306-313`, `:395-398`). O plano diz "desbloqueia deslizar e pular" → o novo controlador precisa bloquear os dois |
| `AbilityFlags.GrapplingHook` | `PlayerData.canGrapplingHook` | lido a cada frame; ok |

**Conflito de planejamento.** A tarefa 2.1 pede testes de "valores de referência" (`jumpSpeed = 7,027`, gravidade 37,98) e a 2.4 exige "sensação do movimento base idêntica". Se o refactor de movimento vier antes ou junto, a "referência" passa a ser o **novo** controlador. Recomenda-se ajustar o texto das tarefas 2.1/2.4: os testes de referência devem congelar o `PlayerBaseStats` do controlador novo, não os números do atual.

---

## 9. Restrições e oportunidades para o refactor

### 9.1 O que não pode quebrar

1. **Tag `Player` e layer 3 "Player"** no GameObject raiz. Usadas por EndGoal, SceneController, CameraFollow, Minimap, EnemyBullet e EnemyAI.
2. **`Cyborg.prefab` com o mesmo GUID** (`fbe9b19b6ebd70f4183d1a6885db563c`) e o **mesmo fileID raiz** (`1227645096870838732`). É o que o `SceneController` do MainMenu instancia. Duplicar o prefab e apontar o MainMenu para a cópia exige editar a cena.
3. **API usada por fora** enquanto o Event Bus não chega (Etapa 1.3): `Die()` idempotente, `RefreshStats()` (até o ShopManager sair), `IsGrappling`/`CancelGrapple()`. Se a classe mudar de nome, `Timer`, `EnemyBullet`, `ShopManager` e `CharacterAnimator` quebram na compilação (`characterMovement` aparece como tipo em 4 scripts).
4. **Campos lidos pelo `CharacterAnimator`**, ou reescrever o CharacterAnimator junto (recomendado: ler de uma struct/estado exposto pelo controlador).
5. **Parâmetros do Animator** (`Speed`, `IsGrounded`, `VerticalSpeed`, `IsWallSliding`, `JumpTrigger`, `DoubleJumpTrigger`), ou reescrever o controller junto.
6. **Alcance mínimo do pulo**: as fases têm muitos degraus de **2 u** e vários de **3 u** (§5.2). Um pulo novo abaixo de ~2,7 u de altura útil ou abaixo de ~8 u de distância pode travar rotas. A **recarga do pulo aéreo pelo wall jump** (`:464`) é usada na única rota encontrada para a Quarta. Validar pela §5.3 e por playtest com o kit base (decisão D1).
7. **Layers 6 e 8** para chão/parede/obstáculo do gancho e alvos do gancho. As paredes invisíveis de borda estão na **Default**; o controlador novo precisa decidir se elas contam como parede/teto.
8. **Fluxo de morte**: travar input **e** física (inclusive o gancho), mostrar o GameOver da cena e não o do asset.

### 9.2 O que pode ser reaproveitado

- **Coyote time e jump buffer** (a ideia e os valores 0,1/0,1), o **consumo do coyote** no pulo e o `vy ≤ 0,1` para `onGround`. Só é preciso que o buffer funcione com o botão já solto (P03).
- Separação ground/air × accel/decel/turn (a estrutura é boa; os valores e o atrito não).
- **Timeout e detecção de travamento do gancho**, máquina de estados do gancho e visuais (LineRenderer, HookTip, FirePoint).
- Rigidbody2D dinâmico com **Interpolate + Continuous + Freeze Rotation**. Basta trocar as leituras de `transform.position` por `rb.position`, adicionar um `PhysicsMaterial2D` com atrito 0 e fazer o teleporte por `rb.position`.
- Composite colliders em **Outlines** alinhados ao grid (sem rampas nem degraus subtile): ótimo para um controlador com casts (`BoxCast`/`Rigidbody2D.Cast`) e para corner correction.
- Arte: a **sheet de pulo tem 6 frames** (antecipação, subida, ápice, queda, pouso) e a de **escalada** tem 6. Dá para fazer Land, Apex e WallGrab sem arte nova.
- `CameraBoundary` como fonte de limites (trocar a busca por evento).

### 9.3 Riscos de YAML e GUID

| Ação | Risco | Como mitigar |
|---|---|---|
| **Renomear a classe mantendo o arquivo e o `.meta`** (`characterMovement.cs`, GUID `07cf2a0327132794e97b608e52064c88`) | O Unity liga o componente pelo **GUID do script** e resolve a classe pelo **nome do arquivo**. Renomear só a classe (ex.: `PlayerController` dentro de `characterMovement.cs`) quebra ("script class cannot be found"). Renomear arquivo **e** classe juntos pelo Editor preserva o GUID | renomear pelo Editor (arquivo + classe) ou `git mv` do `.cs` **e** do `.meta` juntos; nunca apagar o `.meta` |
| **Trocar o script do componente** no prefab (novo MonoBehaviour com outro GUID) | todos os valores serializados daquele componente (§2.1) somem; o listener `OnMovement` já é órfão; `CharacterAnimator`/`Timer`/`EnemyBullet` param de achar o tipo | criar o componente novo **ao lado**, copiar os valores por script de Editor, migrar as referências e só então remover o antigo; ou reescrever dentro do mesmo arquivo/GUID |
| **Renomear campos serializados** | o valor volta ao default do código, e os defaults do código são **diferentes** dos do prefab (§2.1–2.2: 17 campos do `characterMovement` e 6 do `GrapplingHook` divergem) | `[FormerlySerializedAs("nomeAntigo")]` em cada campo renomeado; ou ScriptableObject `PlayerBaseStats` preenchido com os valores da §2.1 |
| **Mudar os defaults no código** achando que muda o jogo | o prefab sobrescreve tudo; nada muda | editar o prefab (ou o SO) e documentar |
| **Mover scripts para `Assets/_Roguelike`/asmdef** | o GUID se mantém se o `.meta` for junto; mas o asmdef `Roguelike.Core` **não pode** referenciar o Assembly-CSharp | manter MonoBehaviours no Assembly-CSharp (o plano já prevê); lógica pura (física de pulo, timers de buffer/coyote) pode ir para o Core e ser testada em EditMode |
| **Editar `Cyborg.prefab` com colegas editando cenas** | conflitos YAML (vide `QuartaFase.unity` com 68 marcadores) | Smart Merge (tarefa 0.5); avisar o time; editar o prefab num commit isolado |
| **Overrides órfãos** (`TerceiraFase`: campos do antigo PlayerData; `QuartaFase`: `ceilingLayer`) | lixo que reaparece se campos de mesmo nome forem recriados | limpar pelo Editor (*Revert*/*Apply*) depois do refactor |
| **Trocar o asset de input** (ex.: migrar para `InputSystem_Actions`) | o PlayerInput guarda o GUID do asset e os eventos por **ID de action**; o script busca por nome | manter os nomes `Player/Movement/Jump/Grapple` ou atualizar o script e o PlayerInput juntos; remover o listener órfão |

### 9.4 Oportunidades e recomendações iniciais

1. **Um modelo de pulo só**: velocidade inicial `v0` e gravidades de subida e queda separadas (ou "tempo segurando" à la Celeste com gravidade e meia gravidade no ápice). Nada de `vy = 0` abrupto, nada de multiplicador por passo, nada de `gravityScale` trocando por frame. Aplicar a gravidade no próprio controlador (com `gravityScale = 0` fixo) deixa tudo independente do dt.
2. **Input robusto**: buffer que funciona com o botão solto (pulo cortado ao sair, não pulo anulado), variable jump por "corte" (`vy *= fator` ao soltar, uma vez), leitura por frame com consumo no `FixedUpdate`, gamepad e eixo vertical.
3. **Checks por cast** a partir de `rb.position` (`BoxCast` de chão/teto/paredes com *skin*), **corner correction** (≈ 0,25–0,5 u) e **ledge forgiveness**, e as layers de teto/parede certas (incluir ou não a *Default*).
4. **Material com atrito 0** no player, para os números do Inspector serem os números do jogo.
5. **Wall slide/jump** gated por `AbilityFlags.WallGrab`, com trava de input curta e gradual, wall jump permitido também subindo, raios em 2–3 alturas.
6. **Câmera**: look-ahead na direção do movimento, zona morta vertical (não seguir pulos pequenos), look-down em quedas longas, ortho maior (6–7) ou ajuste pelo tamanho das salas, e snap de pixel (Pixel Perfect Camera ou arredondamento). Mover o `ParallaxCamera` para `LateUpdate` depois da câmera.
7. **Timestep**: manter a interpolação e considerar `0,01` ou `1/60` depois de tornar a lógica independente do dt.
8. **Animator**: estados Apex/Land usando a sheet de pulo completa, Fall → Run direto, triggers consumidos em todos os estados aéreos (ou trocar triggers por estado explícito vindo do controlador), wall jump com animação.
9. **Kill-zone/respawn** (`ResetToSpawnPoint` via `rb.position`) antes de endurecer o kit base.
10. **Iteração de feel**: um "Play direto na fase" funcional (câmera e spawner de debug na cena) e um overlay de debug (altura/distância do último pulo, velocidade real) aceleram muito o tuning.
