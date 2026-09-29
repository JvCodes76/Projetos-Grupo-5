# Pesquisa técnica: como a movimentação de plataformas fluidos é implementada

> **Para que serve:** base para o PRD e o SPEC do refactor de movimento do jogo (Unity 6, 2D, plataforma cyberpunk com timer por fase e estrutura de roguelike de velocidade).
> **Escopo:** física, controle e lógica do personagem, incluindo as "ajudas invisíveis" que ficam dentro do controlador (coyote time, buffer, correção de quina etc.).
> **Fora de escopo:** game feel/juice (câmera, partículas, screenshake, squash & stretch, hitstop, áudio), que outro agente cobre.
> **Data da pesquisa:** 29/09/2026.

## Convenções

- `[n]` aponta para a referência numerada na seção 13.
- **n/c** = não confirmado. Não achei fonte primária ou confiável com esse número, então ele não aparece.
- **(sim.)** = valor que eu derivei por simulação ou álgebra a partir das constantes do código ou do guia citado. O script de simulação reproduz a ordem de operações do `Player.cs` do Celeste a 60 Hz. **Não é número oficial.**
- **(inf.)** = inferência minha a partir do código. A fonte não diz isso explicitamente.
- Unidades: `px` = pixel; `t` = tile; `u` = unidade Unity; `f` = frame a 60 Hz; `alt` = alturas do personagem (Celeste: hitbox de 11 px; Sonic: 39 px).
- **Eixo Y:** Celeste e Sonic usam Y para baixo, por isso a velocidade de pulo é negativa. Unity usa Y para cima.

---

## 1. Sumário executivo

1. **O controle é código de gameplay, não física de engine.** Celeste usa um sistema próprio de Actors e Solids com AABBs inteiros [3]. Sonic usa sensores e subpixels [10][11]. Super Meat Boy usa "garbage physics" e só aproveita o Box2D como solver; nas palavras de Tommy Refenes, "Nothing in the game is physically accurate" [35]. Hollow Knight roda sobre Rigidbody2D, mas sobrescreve `velocity` todo passo fixo [20][21].
2. **A corrida gira em torno de "velocidade alvo + Approach".** No Celeste: `Speed.X = Approach(Speed.X, MaxRun*moveX, RunAccel*mult*dt)` [1]. No Sonic: aceleração (acc), desaceleração (dec) e atrito (frc) separados, com `dec` 10,7× maior que `acc` [11]. A sensação vem da razão entre aceleração, desaceleração e virada. Não vem de "física real".
3. **Aceleração alta em jogos de precisão, baixa em jogos de momento.** O Celeste chega ao MaxRun em 0,09 s [1]. O Sonic leva cerca de 2,1 s até o top speed (sim., a partir de [11]). SMB se define como "instant-on instant-off" [36].
4. **O pulo variável aparece em duas formas: segurar a velocidade ou cortar a velocidade.** O Celeste mantém `Speed.Y = −105 px/s` por até 0,2 s enquanto o botão está pressionado (`varJumpTimer`) [1]. O HK aplica `JUMP_SPEED` durante N passos fixos (`jump_steps`) [21]. O Sonic limita Y Speed a −4 px/f quando o botão é solto [11].
5. **A gravidade não é constante.** O Celeste aplica meia gravidade no ápice com o botão pressionado (|vy| < 40 px/s) [1][4] e fast fall ao segurar para baixo (queda máxima de 160 para 240 px/s) [1]. Pittman formaliza isso como uma trajetória feita de uma "série de arcos parabólicos", cada um com sua gravidade [44].
6. **As ajudas invisíveis são sistemáticas e pequenas:** coyote de 0,1 s, buffer de cerca de 4 frames, correção de quina de 4 px (½ tile), wall jump a até 2 px da parede, reembolso de estamina, retenção de velocidade por 0,06 s depois de bater numa parede e empurrão para cima de semissólidos [1][4][7]. Maddy resume assim: todas "alargam janelas de tempo ou posição a favor do jogador" [4].
7. **A posição é inteira e o resto subpixel fica guardado.** O Celeste acumula o movimento num `remainder`, arredonda, anda 1 px por vez e chama um callback ao colidir [3]. O Sonic usa 256 subpixels e só colide em pixels inteiros [11]. O guia da Higher-Order Fun recomenda o mesmo padrão [46].
8. **Os eixos são resolvidos separadamente, X antes de Y, com callbacks de colisão.** É nesses callbacks que ficam as ajudas: correção de quina, retenção de velocidade, "dash slide" [1][3][46].
9. **A máquina de estados é explícita, com callbacks de update, begin, end e coroutine.** O Celeste tem 23 estados inteiros. Os devs defendem manter tudo num arquivo grande e sequencial, porque a ordem das operações importa [1][2].
10. **O momento é preservado por regra, não por acidente.** No Celeste, o dash nunca reduz uma velocidade horizontal maior na mesma direção [1]. Acima do MaxRun, o freio é mais fraco (`RunReduce`) se o jogador segurar a direção [1]. O SpeedRunners distingue "Max Speed" (limite no chão) de "Overmax" (conseguido no ar, no gancho ou em rampa), e o atrito do chão puxa de volta para o Max [41].
11. **Os recursos aéreos recarregam no contato.** O dash do Celeste recarrega no chão, depois de um cooldown de recarga de 0,1 s [1]. O dash e as asas do HK recarregam ao pousar, ao quicar com o nail e ao tocar parede [14][18].
12. **A tech de speedrun nasce da combinação de regras simples.** Super = dash horizontal + pulo com coyote ativo. Hyper = a mesma coisa agachado. Wavedash = hyper que começa no ar. Ultra = o multiplicador de 1,2× do "dash slide" aplicado de novo [1][6]. Para um jogo por tempo isso é bom, desde que as regras sejam consistentes e legíveis.
13. **O tempo é fixo e determinístico.** O Celeste roda travado em 60 fps [5] e o Sonic a 60 Hz [11]. O Unity faz física a 0,02 s (50 Hz) por padrão [49], então janelas definidas em frames não se traduzem 1:1.
14. **No Unity, um controlador cinemático próprio reproduz essas regras melhor que um Rigidbody2D dinâmico.** Casts com resolução por eixo, ou `Rigidbody2D.Slide` [47], são preferíveis a um Rigidbody2D dinâmico com `gravityScale` trocado a cada frame (ver seção 11).

---

## 2. Celeste (prioridade máxima)

### 2.1 Fontes e grau de confiança

- **Fonte primária:** o `Player.cs` oficial, publicado pela equipe no repositório `NoelFB/Celeste` em 01/03/2018. A licença MIT cobre só esse código [1][2]. Quase todos os números desta seção saem dele.
  - O arquivo é anterior ao patch 1.4, que adicionou o botão "Crouch Dash" [6].
  - As classes `Input` (tempos de buffer), `Actor` e `Solid` **não foram publicadas**.
- **Artigos da própria Maddy Thorson:** "Celeste & TowerFall Physics" [3] e "Celeste & Forgiveness" [4].
- **Porta C# do Celeste Classic (PICO-8),** no mesmo repositório [5].
- **Wikis da comunidade,** para nomes de tech e janelas em frames [6][7]. A celeste.ink bloqueou o acesso direto; uso apenas trechos que apareceram na busca.

### 2.2 Modelo de física: unidades, tick e integração

| Item | Valor | Fonte |
|---|---|---|
| Resolução interna | 320×180 | [4] |
| Tile | 8 px (Maddy: "2 pixels… a quarter of a tile") | [4] |
| Tick | 60 fps travado ("Celeste is locked to 60fps") | [5] |
| Unidades das constantes | px/s e px/s², multiplicadas por `Engine.DeltaTime`; timers em segundos | [1] |
| Integração | Euler semi-implícito: o estado muda `Speed` e depois chama `MoveH(Speed.X*dt)`, `MoveV(Speed.Y*dt)` | [1] |
| Hitbox em pé | `Hitbox(8, 11, -4, -11)`: 8×11 px, origem nos pés | [1] |
| Hitbox agachado | 8×6 px | [1] |
| Hurtboxes | 8×9 px e 8×4 px, menores que as hitboxes | [1] |

Em tiles, Madeline tem 1 t de largura e 1,375 t de altura. A tela mostra 40×22,5 tiles.

### 2.3 Colisão: Actors, Solids e resto subpixel

Maddy descreve o sistema de física (o mesmo do TowerFall) assim [3]:

- Todos os colisores são **AABBs**.
- Posições e tamanhos são **inteiros**.
- Actors e Solids **nunca se sobrepõem**, salvo casos especiais.
- **Solids não interagem entre si.**

A API mínima do Actor é esta (trecho do artigo [3]):

```csharp
public void MoveX(float amount, Action onCollide) {
    xRemainder += amount;
    int move = Round(xRemainder);
    if (move != 0) {
        xRemainder -= move;
        int sign = Sign(move);
        while (move != 0) {
            if (!collideAt(solids, Position + new Vector2(sign, 0))) {
                Position.X += sign; move -= sign;   // anda 1 px
            } else { if (onCollide != null) onCollide(); break; }
        }
    }
}
```

Pontos importantes:

- **O Actor não tem velocidade.** Quem controla velocidade, aceleração e gravidade é a subclasse. `Player` guarda `Speed` e passa `Speed*dt` para `MoveH`/`MoveV` [1][3].
  - No código real, o resto subpixel se chama `movementCounter`. O patch `Actor` do mod loader Everest mostra isso [8].
- **O callback de colisão é o ponto de extensão.** `Player` passa `OnCollideH`/`OnCollideV`, e é ali que ficam a correção de quina, a retenção de velocidade, o "dash slide" e o zeramento de velocidade [1].
- **Solids móveis** usam o mesmo esquema de resto. Antes de se mover, o Solid monta a lista de Actors que estão `IsRiding` nele. Ao se mover, ele **empurra** quem ficou sobreposto e **carrega** quem estava apoiado. Empurrar tem prioridade sobre carregar. Se o Actor ficar esmagado contra outro Solid, chama `Squish` [3].
  - A Madeline "cavalga" um Solid quando está em cima dele ou agarrada na lateral: `IsRiding` retorna true em `StClimb` se houver Solid a 1 px na direção de `Facing` [1][3].
- **Chão:** o jogo só considera o personagem no chão se `Speed.Y >= 0`. O teste é `CollideFirst<Solid>(Position + Vector2.UnitY)`, ou `JumpThru` visto "de fora", a 1 px abaixo [1].

### 2.4 Máquina de estados

A `StateMachine` do Monocle é um componente com estados inteiros e quatro callbacks por estado [1]:

```csharp
StateMachine.SetCallbacks(StNormal, NormalUpdate, null,          NormalBegin, NormalEnd);
StateMachine.SetCallbacks(StClimb,  ClimbUpdate,  null,          ClimbBegin,  ClimbEnd);
StateMachine.SetCallbacks(StDash,   DashUpdate,   DashCoroutine, DashBegin,   DashEnd);
// update() devolve o próximo estado (int); a coroutine roda em paralelo ao update
```

- **Estados:** são 23 no total. Os que importam para movimento: `StNormal`(0), `StClimb`(1), `StDash`(2), `StSwim`(3), `StBoost`(4), `StRedDash`(5), `StLaunch`(7), `StDreamDash`(9) e `StStarFly`(19) [1]. O resto é de cutscene. Os próprios devs dizem que esses deveriam ser outra entidade [2].
- **Transições:** saem do valor de retorno. `NormalUpdate` retorna `StClimb` quando o jogador agarra, ou `StartDash()` (que devolve `StDash`) quando `CanDash`. `ClimbUpdate` volta para `StNormal` quando o jogador solta, pula ou fica sem estamina [1].
- **Coroutine para ações com duração:** o dash é uma `IEnumerator` que espera 1 frame, fixa a direção e a velocidade, faz `yield return DashTime` e então aplica a velocidade de saída [1].
- **Opinião dos devs** [2]: não separariam estados em classes, porque "due to how much interaction there is between states… this would turn into a giant messy web of references". Preferem um arquivo grande porque "the player behavior code needs to be very tightly ordered and tuned". Também não escrevem teste unitário para o Player.

### 2.5 Ordem das operações em `Player.Update()` [1]

1. **Vars e timers:**
   - detecta o chão;
   - `wallSlideTimer` e `wallBoostTimer`;
   - no chão, reseta `AutoJump`, estamina e wall slide;
   - `jumpGraceTimer` (coyote);
   - cooldowns de dash e recarga;
   - `varJumpTimer`;
   - `forceMoveX` (substitui o input horizontal);
   - `Facing`;
   - `lastAim` (mira do dash);
   - `wallSpeedRetention`;
   - `hopWaitX`.
2. **`base.Update()`:** roda o callback do estado atual, que calcula `Speed`.
3. **Assistências pós-estado:**
   - **JumpThru assist:** subindo dentro de um semissólido, sobe a 40 px/s.
   - **Dash floor snapping:** num dash horizontal no ar com chão a até 3 px, encosta no chão.
   - **Desagachar** ao cair.
4. **`MoveH(Speed.X*dt, onCollideH)`**, depois **`MoveV(Speed.Y*dt, onCollideV)`**. X sempre antes de Y.
5. Água, sprite, carga segurada, triggers.

> A ordem (timers → decisão do estado → assistências → mover X → mover Y) é o que o README chama de "tightly ordered". Isso deve virar requisito explícito do SPEC.

### 2.6 Corrida

| Constante | Valor | Uso |
|---|---|---|
| `MaxRun` | 90 px/s | velocidade alvo |
| `RunAccel` | 1000 px/s² | aproximação do alvo, incluindo frear até 0 e virar |
| `RunReduce` | 400 px/s² | só quando \|Speed.X\| > MaxRun **e** o input aponta na mesma direção |
| `AirMult` | 0,65 | multiplica acel/redução no ar |
| `HoldingMaxRun` | 70 px/s | carregando objeto |
| `DuckFriction` | 500 px/s² | agachado no chão, desacelera até 0 |
| `WalkSpeed` | 64 px/s | andar de cutscene |

Código real, resumido [1]:

```csharp
float mult = onGround ? 1 : AirMult;                // no gelo do Core: mult *= .3f
if (Math.Abs(Speed.X) > max && Math.Sign(Speed.X) == moveX)
    Speed.X = Calc.Approach(Speed.X, max * moveX, RunReduce * mult * Engine.DeltaTime);
else
    Speed.X = Calc.Approach(Speed.X, max * moveX, RunAccel  * mult * Engine.DeltaTime);
```

Tempos derivados (sim.):

| Situação | Chão | Ar |
|---|---|---|
| 0 → MaxRun | 0,090 s (5,4 f) | 0,138 s (8,3 f) |
| Soltar o direcional (90 → 0) | 0,090 s | 0,138 s |
| Virar (90 → −90) | 0,180 s | 0,277 s |

- **Soltar o direcional freia com `RunAccel`, não com `RunReduce`.** Ao soltar (moveX = 0), o alvo vira 0 e cai no ramo `RunAccel`. Resultado: soltar freia rápido, mas **segurar a direção preserva o excesso de velocidade**. Uma velocidade de 260 no ar só decai 260 px/s² se o jogador segurar para frente; se soltar, decai 650 px/s² (inf. do código).
- **O input horizontal é digital:** `moveX` vale −1, 0 ou 1 (`Input.MoveX.Value`). `forceMoveX` o substitui por um tempo depois de wall jump e climb hop [1].
- **Modificadores de ambiente:**
  - gelo do "Core" frio: ×0,3 na aceleração no chão;
  - espaço: ×0,6 em max run, queda máxima e gravidade (`SpacePhysicsMult`) [1].

### 2.7 Pulo

| Constante | Valor | Uso |
|---|---|---|
| `JumpSpeed` | −105 px/s | velocidade vertical inicial |
| `JumpHBoost` | 40 px/s | somado a `Speed.X` na direção do input |
| `VarJumpTime` | 0,2 s | janela de "segurar velocidade" |
| `Gravity` | 900 px/s² | |
| `HalfGravThreshold` | 40 px/s | abaixo disso, com o botão pressionado, a gravidade cai para ×0,5 |
| `CeilingVarJumpGrace` | 0,05 s | tolerância ao bater a cabeça logo no começo do pulo |
| `JumpGraceTime` | 0,1 s | coyote time |

Código real, resumido [1]:

```csharp
// Jump()
Speed.X += JumpHBoost * moveX;  Speed.Y = JumpSpeed;  Speed += LiftBoost;
varJumpSpeed = Speed.Y;  varJumpTimer = VarJumpTime;  jumpGraceTimer = 0;
Input.Jump.ConsumeBuffer();

// NormalUpdate(), em ordem:
float mult = (Math.Abs(Speed.Y) < HalfGravThreshold && (Input.Jump.Check || AutoJump)) ? .5f : 1f;
Speed.Y = Calc.Approach(Speed.Y, max, Gravity * mult * Engine.DeltaTime);   // max = queda máxima
if (varJumpTimer > 0) {
    if (AutoJump || Input.Jump.Check) Speed.Y = Math.Min(Speed.Y, varJumpSpeed); // segura −105
    else varJumpTimer = 0;                                                       // soltou: acabou
}
```

**Como funciona o pulo variável:** enquanto o botão está pressionado, por até 0,2 s, a velocidade vertical fica **presa em −105 px/s**, uma subida em velocidade constante. Soltar não corta a velocidade; só encerra a fase de "hold", e daí a gravidade assume. Depois disso vem a desaceleração normal, com meia gravidade abaixo de 40 px/s se o botão continuar pressionado.

Altura em função do tempo de botão (sim. a 60 Hz, sem LiftBoost):

| Botão pressionado | Altura do ápice | Tempo até o ápice | Tempo no ar (mesmo nível) |
|---|---|---|---|
| 1 frame (toque) | 7,0 px (0,88 t) | ~0,12 s | ~0,25 s |
| 5 frames | 14,0 px (1,75 t) | ~0,18 s | ~0,38 s |
| 10 frames | 22,75 px (2,84 t) | ~0,27 s | ~0,52 s |
| 20+ frames (máximo) | **28,5 px (3,56 t ≈ 2,6 alt)** | **~0,35 s** | **~0,68 s** |

Forma contínua do pulo máximo (inf., mesma lógica):

```
h = v·T_var + (v² − 40²)/(2g) + 40²/(2·g/2)
  = 105·0,2 + (105² − 40²)/1800 + 40²/900
  ≈ 21 + 5,24 + 1,78
  ≈ 28,0 px

t_apex ≈ 0,2 + 65/900 + 40/450
       ≈ 0,361 s
```

Distância horizontal num terreno plano (sim., segurando para frente o tempo todo):

| Pulo | Distância horizontal | Altura do ápice |
|---|---|---|
| Correndo (90 + 40 = 130 px/s) | ~65 px ≈ 8,1 t | 28,5 px |
| Super (260 px/s) | ~118 px ≈ 14,8 t | 28,5 px |
| Hyper (325 px/s, vy ×0,5) | ~137 px ≈ 17,2 t | ~13 px |

Outros detalhes do código:

- **Ceiling grace:** ao bater a cabeça, a correção de quina é tentada primeiro (seção 2.12). Se ela falhar, o jogo zera `varJumpTimer`, **exceto** se o pulo começou há menos de 0,05 s [1].
- **`AutoJump`:**
  - ao fim do dash, `AutoJump = true` [1];
  - enquanto for true, o jogo trata o pulo como "segurado": meia gravidade no ápice e hold de velocidade, até pousar ou pular de novo (inf. da lógica de `Update`) [1];
  - quiques (`Bounce`) usam `AutoJumpTimer = 0,1 s` (`BounceAutoJumpTime`) [1].
- **O pulo é consumido do buffer (`ConsumeBuffer`).** Um único aperto não gera dois pulos, mesmo com o buffer ainda ativo [1].

### 2.8 Queda

| Constante | Valor | Uso |
|---|---|---|
| `MaxFall` | 160 px/s | teto normal de queda (20 t/s) |
| `FastMaxFall` | 240 px/s | teto ao segurar para baixo (30 t/s) |
| `FastMaxAccel` | 300 px/s² | velocidade com que o teto transita entre 160 e 240 |

- A gravidade faz `Approach(Speed.Y, maxFall, …)`. Não há corte seco: a velocidade tende suavemente ao teto.
- O próprio teto também é aproximado (`maxFall = Approach(maxFall, fmf, 300*dt)`), então o fast fall "entra" em cerca de 0,27 s (sim.) [1].
- De 0 a 160 px/s: cerca de 0,18 s (sim.).

### 2.9 Paredes: wall slide, wall jump, escalada e estamina

**Wall slide.** Ativa quando todas estas condições valem:

- input para a parede (ou grab com input neutro);
- sem segurar baixo;
- `Speed.Y >= 0`;
- `wallSlideTimer > 0`;
- Solid a 1 px na direção de `Facing` [1].

O teto de queda vira `Lerp(MaxFall, WallSlideStartMax, wallSlideTimer/WallSlideTime)`:

- **começa em 20 px/s** (`WallSlideStartMax`);
- sobe até 160 px/s conforme o timer de **1,2 s** (`WallSlideTime`) se esgota, **só enquanto desliza**;
- o timer recarrega no chão, no pulo e no dash [1].

**Wall jump.** `WallJumpCheck(dir)` testa um Solid a `WallJumpCheckDist = 3 px`. Na prática, basta estar a **até 2 px** da parede, como diz Maddy [4]. O jump executa:

```csharp
if (moveX != 0) { forceMoveX = dir; forceMoveXTimer = WallJumpForceTime; }  // 0,16 s
Speed.X = WallJumpHSpeed * dir;   // 130 px/s = MaxRun + JumpHBoost
Speed.Y = JumpSpeed;              // −105 (+ LiftBoost); varJumpTimer = 0,2
```

- **O input é travado só se o jogador estava apertando uma direção.** Com input neutro não há `forceMoveX`, então a Madeline sobe e a velocidade de ar (650 px/s²) a traz de volta para a parede. Esse é o **"neutral jump"**, que escala paredes sem gastar estamina [1][6].
- **Parede móvel:** o wall jump herda o `LiftSpeed` dela [1].

**Escalar (grab):**

| Constante | Valor |
|---|---|
| `ClimbMaxStamina` | 110 |
| `ClimbUpCost` | 100/2,2 ≈ 45,45 por s subindo |
| `ClimbStillCost` | 10 por s parado |
| Custo descendo | 0 |
| `ClimbJumpCost` | 110/4 = 27,5 |
| `ClimbTiredThreshold` | 20 (abaixo disso não agarra) |
| `ClimbUpSpeed` | −45 px/s |
| `ClimbDownSpeed` | 80 px/s |
| `ClimbSlipSpeed` | 30 px/s |
| `ClimbAccel` | 900 px/s² |
| `ClimbNoMoveTime` | 0,1 s (congelado ao agarrar) |
| `ClimbGrabYMult` | 0,2 (agarrar mata 80% da velocidade de queda) |
| `ClimbCheckDist` | 2 px |
| `ClimbUpCheckDist` | 2 px (agarra borda até 2 px acima e sobe junto) |

- Com estamina cheia, a Madeline sobe por cerca de 2,4 s, uns 13 tiles (sim.), aguenta 11 s parada ou faz cerca de 4 climb jumps.
- A estamina volta ao máximo ao tocar o chão [1].

**Climb jump e reembolso de estamina** ("Stamina Refunds" [4]). Pular agarrado sem input horizontal:

1. gasta 27,5 de estamina e dá um pulo normal;
2. arma `wallBoostTimer = 0,2 s`;
3. se o jogador apertar **para longe da parede** dentro dessa janela, o jogo aplica `Speed.X = 130` e **devolve** os 27,5 de estamina. O climb jump vira wall jump depois do fato [1][4].

Pular agarrado com input para longe da parede é um wall jump direto [1].

**Climb hop.** Ao passar do topo da parede, o jogo aplica `Speed.Y = min(Speed.Y, −120)` e `Speed.X = 100` na direção da parede, e força o input por 0,2 s. Com Solid móvel, `hopWaitX` "espera" o jogador subir acima do Solid antes de liberar a velocidade X [1].

**Super wall jump ("wallbounce").** Pular perto de uma parede durante um dash **reto para cima** (ou em `DashAttacking` com `DashDir = (0,−1)`) aplica:

- `Speed.X = 170` (`MaxRun + 2·JumpHBoost`);
- `Speed.Y = −160`;
- `varJumpTime = 0,25 s` [1].

Maddy diz que essa janela de distância é maior, "I think it's 5 pixels" [4]. No `Player.cs` publicado, porém, o super wall jump usa o mesmo `WallJumpCheck` de 3 px (inf.: provavelmente mudou depois de 2018). A constante `SuperWallJumpForceTime = 0,2` está declarada mas **não é usada** no arquivo [1].

### 2.10 Dash

| Constante | Valor | Uso |
|---|---|---|
| `DashSpeed` | 240 px/s | 30 t/s |
| `DashTime` | 0,15 s | 36 px ≈ 4,5 t em linha reta |
| `EndDashSpeed` | 160 px/s | velocidade de saída na direção do dash, se `DashDir.Y <= 0` |
| `EndDashUpMult` | 0,75 | aplicado à componente Y se estiver subindo |
| `DashCooldown` | 0,2 s | entre dashes |
| `DashRefillCooldown` | 0,1 s | não recarrega no chão logo após dashar |
| `DashAttackTime` | 0,3 s | janela em que o jogador "está atacando com dash" |

Sequência [1]:

1. **`CanDash`:** `Input.Dash.Pressed` (com buffer) + `dashCooldownTimer <= 0` + `Dashes > 0`.
2. **`StartDash`:** `Dashes--` e `ConsumeBuffer()`.
3. **`DashBegin`:**
   - `Freeze(0,05 s)`, um hitstop global de 3 frames;
   - arma cooldowns e `dashAttackTimer`;
   - guarda `beforeDashSpeed`;
   - **zera `Speed`**.
4. **`DashCoroutine`:**
   - `yield return null` (1 frame);
   - lê `dir = lastAim` (vetor de mira de `Input.GetAimVector(Facing)`, atualizado a cada `Update`);
   - aplica a regra de preservação de momento:

     ```csharp
     var newSpeed = dir * DashSpeed;
     if (Math.Sign(beforeDashSpeed.X) == Math.Sign(newSpeed.X) && Math.Abs(beforeDashSpeed.X) > Math.Abs(newSpeed.X))
         newSpeed.X = beforeDashSpeed.X;          // dash nunca te deixa mais lento na horizontal
     Speed = newSpeed;
     ```
   - depois vem `yield return DashTime` e a saída: `Speed = DashDir * EndDashSpeed` (se não for para baixo), Y ×0,75 se estiver subindo, `AutoJump = true`, volta para `StNormal`.
5. **Durante `StDash`:**
   - `NormalUpdate` não roda, então **não há gravidade nem controle**;
   - `DashUpdate` só verifica super, super wall jump e wall jump [1].
6. **Recarga:** no chão sobre Solid ou JumpThru, fora de espinhos, e com `dashRefillCooldownTimer <= 0` [1].

**Janela de mira atrasada (inf.).** A direção é lida depois do freeze de 3 frames e mais 1 frame de `yield`. Isso bate com a janela de "demodash" documentada pela comunidade: soltar o baixo em até 4 frames (≈0,066 s) muda a direção [6]. Na prática, é uma tolerância para diagonais apertadas "com atraso".

### 2.11 Super, hyper, wavedash e ultra: como saem do código

- **Super.** Durante um dash **horizontal** (`DashDir.Y == 0`), pular com `jumpGraceTimer > 0` (no chão ou no coyote) chama `SuperJump()`:
  - `Speed.X = 260 · Facing` (`SuperJumpH`). O valor é **atribuído**, não somado.
  - `Speed.Y = −105` [1].
- **Hyper.** Mesmo caminho, mas **agachado**: X ×1,25 = **325 px/s** e Y ×0,5 = −52,5 px/s [1].
- **Dash slide.** Um dash diagonal para baixo que toca o chão (no início do dash ou ao pousar, em `OnCollideV`) vira horizontal:
  - `DashDir.Y = 0`;
  - `Speed.Y = 0`;
  - `Speed.X *= 1,2` (`DodgeSlideSpeedMult`);
  - agacha [1].

  Por isso um pulo logo depois vira hyper.
- **Wavedash.** É o dash slide iniciado no ar: dash diagonal para baixo que pousa ainda dentro do `DashTime` e pulo em seguida [6] (inf. da ligação com o código).
- **Extended super/hyper** [6]. Pular depois que o dash recarregou no chão mantém o dash disponível. O `DashRefillCooldown` de 0,1 s é o que define a janela (inf.).
- **Ultra.** Um dash diagonal para baixo já com velocidade alta preserva o X maior (regra do `beforeDashSpeed`). Ao tocar o chão, ganha ×1,2 [1][6]. Reaplicado em sequência, "there is no known limit" [6].
- **Bunnyhop / corner boost.**
  - Cada `Jump()` soma +40 px/s (`JumpHBoost`) na direção do input [1].
  - Pular antes do `RunReduce` agir preserva velocidade acima do MaxRun.
  - O corner boost (climb jump nos 5 a 7 px do topo da parede mantendo o momento [6]) combina com `wallSpeedRetentionTimer` (seção 2.12) (inf.).

### 2.12 Ajudas invisíveis: catálogo completo do `Player.cs`

| Ajuda | Como funciona | Parâmetro |
|---|---|---|
| **Coyote time** | `jumpGraceTimer = 0,1` enquanto no chão; decrementa no ar; pular com timer > 0 conta como pulo do chão | 0,1 s ≈ 5 frames de ar [1][6] |
| **Buffer de input** | `Input.Jump.Pressed` / `Input.Dash.Pressed` continuam true por alguns frames após o aperto; as ações chamam `ConsumeBuffer()` | "4 frames", e só se o botão ainda estiver pressionado (comunidade [7]). Valor exato n/c: a classe `Input` não foi publicada. Indício: o mod ExtendedVariantMode usa 0,08 s ao tornar o Grab "bufferável" [9] |
| **Meia gravidade no ápice** | ×0,5 com \|vy\| < 40 px/s e o botão pressionado (ou `AutoJump`) | [1][4] |
| **Correção de quina ao subir** | Em `OnCollideV` com vy < 0: tenta deslocar 1..4 px para o lado (esquerda se vx ≤ 0, direita se vx ≥ 0) e 1 px para cima; se livre, move e **não perde a velocidade** | `UpwardCornerCorrection = 4 px` [1] |
| **Ceiling var-jump grace** | Bater a cabeça nos primeiros 0,05 s não cancela o pulo variável | 0,05 s [1] |
| **Correção de quina no dash horizontal** | Em `OnCollideH` durante dash com vy = 0: procura espaço ±1..4 px na vertical e "escorrega" pela quina | `DashCornerCorrection = 4 px` [1][4] |
| **Correção de quina no dash para baixo** | Em `OnCollideV` com vy > 0 durante dash iniciado no ar: desloca até 4 px para o lado se ali não houver chão (passa pela borda) | 4 px [1] |
| **Agachar automático no dash** | Dash no chão contra um vão baixo onde cabe agachado vira agachamento | [1] |
| **Dash floor snapping** | Dash horizontal no ar com chão a ≤ 3 px: encosta no chão (viabiliza super "baixo") | `DashVFloorSnapDist = 3 px` [1] |
| **Pop em semissólido** | Dash horizontal atravessando um JumpThru com o pé a ≤ 6 px abaixo do topo: sobe para cima dele | `DashHJumpThruNudge = 6 px` [1][4] |
| **JumpThru assist** | Subindo por dentro de um semissólido: +40 px/s para cima | `JumpThruAssistSpeed = −40` [1] |
| **Duck correction** | Soltar o agachar sob teto: desliza até 4 px, a 50 px/s, para achar onde ficar em pé | 4 px e 50 px/s [1] |
| **Wall jump a distância** | Checa parede a 3 px (vão ≤ 2 px) | `WallJumpCheckDist = 3` [1][4] |
| **Grab tolerante** | Agarra borda até 2 px acima e encosta na parede em até 2 px | `ClimbUpCheckDist`, `ClimbCheckDist` [1] |
| **Retenção de velocidade na parede** | Ao bater em parede, guarda `Speed.X`; se a parede "sumir" em até 0,06 s (p. ex., passou da quina), devolve a velocidade | `WallSpeedRetentionTime = 0,06 s` [1] |
| **Reembolso de estamina** | Climb jump + input para fora em até 0,2 s vira wall jump e devolve a estamina | 0,2 s [1][4] |
| **Lift momentum storage** | Pular de plataforma móvel soma a velocidade dela; o boost ainda vale "por alguns frames" depois que ela parou | Caps em 2.13 [1][4]; duração n/c |
| **Sair da plataforma subindo** | Se a plataforma sobe e o jogador sai andando da borda, herda `LiftBoost.Y` | [1] |
| **Hitbox de dano menor** | Hurtbox 8×9 contra hitbox 8×11 | [1] |

Frase-guia de Maddy: "All are centered around widening timing or positioning windows, so that everything is fudged a tiny bit in the player's favor" [4].

### 2.13 Lift boost (momento de plataformas)

`LiftBoost` é o `LiftSpeed` do Solid em que o jogador está, limitado assim [1]:

- **X:** entre ±250 px/s (`LiftXCap`);
- **Y:** só valores para cima, até −130 px/s (`LiftYCap`); velocidade para baixo é ignorada.

O valor é somado em:

- `Jump`, `SuperJump`, `WallJump` e `SuperWallJump`;
- dash;
- soltar o grab;
- ficar sem estamina.

`LaunchedBoostCheck` marca "launched" (efeito visual) quando o lift boost passa de 100 px/s e a velocidade total passa de 220 px/s [1].

### 2.14 Origem: Celeste Classic (PICO-8, 30 fps)

A porta C# mostra a versão mínima do mesmo desenho, em **px/frame a 30 fps** [5]:

- `maxrun = 1`;
- `accel = 0.6` no chão e `0.4` no ar;
- `deccel = 0.15` acima do max;
- `gravity = 0.21`, com `×0.5` se |vy| ≤ 0.15 (meia gravidade no ápice já existia);
- `maxfall = 2` (0,4 deslizando na parede);
- pulo `−2`;
- wall jump checa `±3 px` e dá `vx = maxrun+1`;
- **buffer de 4 frames** (`jbuffer = 4`) e **coyote de 6 frames** (`grace = 6`);
- dash com velocidade 5 (diagonal ×0,707) desacelerando para 2 em 4 frames.

A arquitetura do jogo completo é esta mesma, refinada.

### 2.15 O que levar do Celeste para o refactor

- Parametrizar tudo em **unidades de mundo por segundo**, com timers em segundos. O "feel" é um conjunto de constantes pequenas, não uma simulação.
- Usar **pulo com hold de velocidade constante** (`varJumpTimer`) + **meia gravidade no ápice** + **fast fall**, em vez de gravityScale arbitrário.
- Colocar **as ajudas no callback de colisão**, separadas por eixo.
- Seguir as **regras de momento**: dash não reduz X maior; `RunReduce` só com a direção pressionada; `JumpHBoost` aditivo; super e hyper com valores fixos.
- **Consumir o buffer** ao executar a ação.

---

## 3. Sonic (clássico 16-bit, com notas do Mania)

Fonte: Sonic Physics Guide (SPG), da Sonic Retro [10]. O site original bloqueia acesso automatizado com um desafio anti-bot, então li o espelho offline em Markdown [11]. O espelho reproduz o texto do SPG sob GFDL e aponta a página original de cada seção.

### 3.1 Modelo

- **Tick:** 60 Hz (NTSC). Posições e velocidades em pixels + **subpixels (1 px = 256 spx)**. **A colisão ignora subpixels** [11].
- **Estado:**
  - `Ground Speed` é a velocidade escalar ao longo do chão;
  - `X/Y Speed` derivam dela: `X = GS·cos(θ)`, `Y = GS·−sin(θ)`;
  - `Ground Angle` é hexadecimal de 0 a 255 [11].
- **Terreno:** blocos de 16×16 com *height arrays* de 16 valores e um array de ângulos [11].
- **Sensores do personagem:**

  | Sensores | Função | Posição |
  |---|---|---|
  | A e B | chão | pés, ±Width Radius |
  | C e D | teto | cabeça |
  | E e F | parede | Push Radius 10, 8 px mais baixos no plano |

  Tamanhos do Sonic: Width Radius 9 (19 px de largura), Height Radius 19 (**39 px de altura**); rolando, 7 e 14 [11].
- **Quatro modos de colisão** (chão, parede direita, teto, parede esquerda), escolhidos pelo ângulo. Os sensores giram em 90°, o que permite loops [11].
- **Snap ao chão:** a tolerância de "grudar" é `min(|X Speed| + 4, 14)` px no S2 em diante. Quanto mais rápido, mais o personagem cola em descidas [11].
- **Ordem por frame no chão** [11]:
  1. slope factor;
  2. checagem de pulo;
  3. input e atrito;
  4. sensores de parede (com a posição futura);
  5. rolar;
  6. mover;
  7. sensores de chão (ângulo e snap);
  8. escorregar.
- **Ordem por frame no ar** [11]:
  1. soltou o pulo? então cap de −4;
  2. input;
  3. air drag;
  4. mover;
  5. gravidade (depois de mover);
  6. colisões.

### 3.2 Constantes (Sonic normal)

| Constante | Valor original | px/s (×60) | Normalizado (bloco 16 px; Sonic 39 px) |
|---|---|---|---|
| Aceleração (acc) | 12 spx/f² = 0,046875 px/f² | 168,75 px/s² | 10,5 t/s² |
| Desaceleração (dec) | 128 spx = 0,5 px/f² | 1800 px/s² | 112,5 t/s² |
| Atrito (frc) | 12 spx = 0,046875 | 168,75 px/s² | — |
| Top speed | 6 px/f | 360 px/s | 22,5 t/s · 9,2 alt/s |
| Aceleração no ar | 24 spx = 0,09375 (2× acc) | 337,5 px/s² | — |
| Gravidade | 56 spx = 0,21875 px/f² | 787,5 px/s² | 49,2 t/s² |
| Força do pulo | 6,5 px/f (Knuckles 6) | 390 px/s | 24,4 t/s |
| Cap do pulo variável | Y Speed ≥ −4 px/f ao soltar | 240 px/s | — |
| Slope factor (andando) | 0,125 px/f² × sin(θ) | — | — |
| Slope factor (rolando) | subindo 0,078125; descendo 0,3125 | — | — |
| Atrito rolando / dec rolando | 6 spx / 32 spx | — | — |
| Cap de X Speed rolando | 16 px/f | 960 px/s | — |
| Super Sonic | acc 48 spx, dec 1 px, top 10 px/f, pulo 8 px/f | — | — |

Todos os valores da tabela vêm de [11] (SPG: Forces e Slope Physics).

Derivados (sim./álgebra):

- **0 → top speed:** 128 f ≈ **2,13 s**.
- **Parar só com atrito:** também cerca de 2,13 s.
- **Virar a 6 px/f com `dec`:** 12 f = 0,2 s.
- **Pulo máximo no plano:** cerca de 97 a 100 px (≈ 6,1 blocos ≈ 2,5 alt), ápice em cerca de 30 f = 0,5 s.
- **Pulo mínimo** (soltar no 1º frame de ar, cap −4): cerca de 37 a 39 px em cerca de 0,3 s.

### 3.3 Regras que importam

- **Corrida:** `acc` quando o input aponta no sentido da velocidade, `dec` quando aponta contra, `frc` sem input.
  - **Quirk:** se `dec` inverter o sinal, `Ground Speed` vira ±0,5 no sentido novo [11].
- **Top speed:**
  - no Sonic 1, apertar a direção acima do top speed **reseta** para o top;
  - dos jogos seguintes em diante, só **não acelera** acima do top [11].

  É a diferença entre "cap duro" e "cap macio", e o cap macio preserva momento vindo de rampas e molas.
- **Pulo perpendicular ao chão:** `X -= jf·sin(θ)`, `Y -= jf·cos(θ)`, somado à velocidade existente [11]. Pular numa rampa lança na diagonal.
- **Air drag:** só quando −4 < Y Speed < 0, perto do topo da subida. Subtrai da X Speed uma pequena fração dela mesma a cada frame [11]; ver a fórmula no SPG.
- **Slope factor:**
  - `GS -= slope·sin(θ)`: rampas aceleram na descida e freiam na subida;
  - rolando, o efeito é assimétrico: 0,078 subindo e 0,3125 descendo [11];
  - esse é o motor de momento do Sonic.
- **Escorregar:**
  - com |GS| < 2,5 em ângulo íngreme, desgruda e trava o input por 30 frames (`control lock`);
  - no S3, só desliza ±0,5 [11];
  - resultado: ninguém "anda devagar pelo teto".
- **Pouso:** a velocidade vertical é convertida em `Ground Speed` conforme o ângulo:

  | Superfície | Nova Ground Speed |
  |---|---|
  | plana | X Speed |
  | rampa | Y·0,5·−sign(sin θ) |
  | íngreme | Y·−sign(sin θ) |

  Cair numa rampa vira velocidade [11].
- **Rolling:** não acelera, só atrito. Pulo rolando **trava o controle aéreo** nos clássicos, mas **não** no CD nem no Mania [11].
- **Spindash (S2):**
  - cada aperto soma 2 (máx. 8) a `spinRev`, com arrasto;
  - ao soltar, `GS = 8 + floor(spinRev)/2`, até 12;
  - no CD, é carga de 45 frames que resulta em 12 [11].
- **Mania:**
  - soma a gravidade no frame do pulo;
  - corrige o "jump delay": no clássico, o frame do pulo não move o personagem [11].

**Lição para o refactor:** momento vindo do terreno (rampas) exige um cap macio, pouso que converte velocidade vertical em horizontal e aceleração baixa. Isso conflita com a precisão do Celeste. Para um jogo por tempo, vale como **camada opcional** (rampas de impulso, conversão de queda em velocidade em superfícies específicas), não como base.

---

## 4. Hollow Knight

**Fontes e limites.** Os números numéricos de `HeroController` (campos públicos serializados no prefab do Knight) **não estão documentados publicamente**. Os mods citam os nomes dos campos, mas não os valores. O que segue combina wikis (valores medidos pela comunidade), código de mods (arquitetura) e inferência.

### 4.1 Modelo

- **Engine:** Unity. O Knight é controlado por uma classe C# `HeroController`. Inimigos e interações usam PlayMaker (FSM visual) [23]. O `cState` guarda flags booleanas de estado (p. ex. `cState.attacking`, `cState.wallSliding`, `cState.onGround`) [20][22].
- **Rigidbody2D com `gravityScale`**, com a velocidade setada diretamente no `FixedUpdate` [20][21]. O mod SkillUpgrades faz a escalada funcionar "substituindo `WALLSLIDE_SPEED` por 0 no FixedUpdate" e checando `gravityScale` [20].
- **Campos que os mods referenciam** [20][21]:
  - corrida: `RUN_SPEED`, `RUN_SPEED_CH` (Sprintmaster), `RUN_SPEED_CH_COMBO` (Sprintmaster + Dashmaster), `WALK_SPEED`, `UNDERWATER_SPEED`;
  - pulo e queda: `JUMP_SPEED`, `JUMP_STEPS`, `MAX_FALL_VELOCITY`;
  - dash: `DASH_SPEED`, `DASH_SPEED_SHARP`, `DASH_TIME`, `SHADOW_DASH_TIME` (cooldown do shade dash);
  - parede: `WALLSLIDE_SPEED`.
  - Valores numéricos: **n/c**, exceto os da tabela 4.2.

### 4.2 Valores confirmados pela wiki

| Item | Valor | Fonte |
|---|---|---|
| Corrida base | 8,3 u/s | [12] |
| Com Sprintmaster | 10 u/s (só no chão) | [12] |
| Sprintmaster + Dashmaster | 11,5 u/s | [12] |
| Cooldown do dash | 0,6 s (0,4 s com Dashmaster) | [13][14] |
| Cooldown do Shade Cloak | 1,5 s, independente do dash comum | [15] |
| Carga do Crystal Heart | 0,8 s | [16] |
| Sharp Shadow | +40% de distância de dash | [15] |

Normalização em tiles ou alturas do personagem: **n/c**. O tamanho do colisor do Knight em unidades não foi confirmado, e HK não tem grade de tiles de gameplay publicada.

### 4.3 Comportamento

- **Corrida.** Os mods que reescrevem o movimento atribuem `velocity.x = direção × RUN_SPEED` diretamente, sem rampa de aceleração [21]. Isso é consistente com a resposta instantânea observada (inf.).
- **Pulo em "passos".** A reimplementação de `Jump()` num mod mostra o padrão [21]:

  ```csharp
  if (jump_steps > JUMP_STEPS) { CancelJump(); return; }
  rb2d.velocity = new Vector2(rb2d.velocity.x, JUMP_SPEED);  // velocidade constante enquanto sobe
  jump_steps++;                                               // conta passos de FixedUpdate
  ```

  É o mesmo desenho do Celeste: **velocidade de subida constante durante uma janela limitada pelo botão**, depois gravidade. Valores n/c.
- **Dash.** É horizontal (para baixo só com Dashmaster). **Não carrega momento:** depois do dash, sem input, o Knight para na horizontal (o mod SkillUpgrades descreve esse comportamento ao comparar com seu dash vertical) [20]. O dash recarrega ao pousar, ao quicar com o nail ou ao agarrar ou pular da parede [14].
- **Mantis Claw.**
  - Pressionar para a parede gruda e desliza (`WALLSLIDE_SPEED`, n/c).
  - Pular sai na diagonal para longe da parede.
  - Pode ser repetido à vontade.
  - Esquerda + direita juntos fazem cair na velocidade normal [17].
- **Monarch Wings (pulo duplo).** A altura varia com o tempo de botão, como no pulo normal. Recarrega no chão, no pogo e na parede. O 1º frame da animação não levanta o Knight [18].
- **Pogo (nail-bounce).** Um golpe para baixo que acerta inimigo, espinho ou objeto dá quique vertical e **recarrega dash e asas** [14][18][19]. É o principal "reset aéreo" do jogo.
- **Crystal Heart (super dash).** Carga de 0,8 s no chão ou na parede. Voa na horizontal até bater em parede ou tomar dano, ou até o jogador cancelar com pulo ou com novo toque [16].

**Lição.** A HK mostra que é possível ter precisão com Rigidbody2D, **desde que** o código sobrescreva a velocidade todo passo e trate a física da engine só como "mover e colidir". As mecânicas de reset (pogo recarrega recursos) são o que cria as rotas rápidas.

---

## 5. Hollow Knight: Silksong

**Status.** Lançado em 04/09/2025 [24]. Os valores internos **não estão confirmados**. Existem repositórios de decompilação e mods, mas não validei nenhum número por fonte confiável. Consultei um guia de movimento com números em faixas vagas, como "roughly 10–20% faster" [57], e **descartei** esses números por falta de metodologia.

| Mecânica | O que é confirmado | Fonte |
|---|---|---|
| **Swift Step** (dash + sprint) | Tocar dá um dash, no chão ou no ar. Segurar Baixo + tocar dá um dash para baixo no ar, e segurar prolonga. **Segurar SPRINT corre**: a wiki registra "112% faster" e pulos mais longos. Rotas longas usam sprint → pulo → dash no ar | [25] |
| **Cling Grip** | Na parede, Hornet "briefly sticks… before slowly sliding down". Pular dá wall jump. Dá para subir com pulos repetidos contra a parede ou segurando para a parede + SPRINT. Sem estamina documentada | [26] |
| **Clawline** (arpão) | Custa 1 fio de Silk. Tem tolerância vertical para acertar argolas. Em argolas, Hornet fica pendurada até pular. Em inimigos, faz o dano e puxa Hornet (com um pequeno lançamento para cima) | [27] |
| **Drifter's Cloak** | Segurar pulo no ar plana e sobe em correntes de ar. Não funciona enquanto o pulo duplo (Faydown) está disponível [30]; um patch removeu a troca entre os dois no ar via Baixo+Pulo [28] | [28][30] |
| **Faydown Cloak** | Pulo duplo | [30] |
| **Silk Soar** | Baixo + Arpão (carregar). Lança a agulha para cima e Hornet "shoots up" até o teto, atravessando transições de sala. Pular, atacar ou tomar dano cancela | [29] |
| **Pogo depende do Crest** | Hunter: golpe para baixo **diagonal** ("long dash strike diagonally downwards"), o que dificulta quicar. Wanderer: golpe **reto para baixo**, quique sem atraso | [31][32] |
| **Ledge grab automático** | Relatado por guias e por jogadores, incluindo inconsistências (p. ex., segurando o planar) e interação com "auto sprint" | [33] (trechos de busca) |
| **Tech emergente** | Speedrunners usam a cura para "stall mid-air long enough to grab a ledge", quicam no próprio corpo, entre outras | [34] |

**Diferenças para o HK, qualitativas (Wikipedia: "moving more acrobatically" [24]):**

- a corrida tem **dois níveis** (andar e sprint segurando), em vez de charms de velocidade;
- o **pogo é diagonal** no kit inicial;
- há **agarre de borda**;
- a mobilidade gasta **recurso** (Silk no Clawline e no Silk Soar).

**Lição para o projeto** (cyberpunk com gancho e timer):

- sprint como estado com teto de velocidade separado;
- arpão como "puxar até o ponto" com custo (hoje o `GrapplingHook` do projeto já puxa até o ponto, ver 11.7);
- ledge grab como ajuda invisível (útil, mas precisa de regras claras para não ficar "inconsistente").

---

## 6. Super Meat Boy

**Fontes.** Entrevista de Tommy Refenes à equipe Isetta [35], o índice anotado da conversa com Casey Muratori na HandmadeCon 2015 [36], o postmortem da Team Meat [37] e notas de speedrun da SDA [38]. **Constantes numéricas: n/c**, com a única exceção abaixo.

- **Arquitetura:**
  - engine própria em C++ (camadas core, engine e game);
  - Box2D modificado, usado só como solver de colisão;
  - "All of Meat Boy's weird controls are done in-game, not in-engine" [35].
- **"Garbage physics":** tudo foi ajustado por iteração ("Nothing in the game is physically accurate") [35]. O Forever manteve "the exact same feel", mas removeu o controle de direção (auto-runner) e, com ele, "air friction for when you're turning around in the air" [35].
- **O que Tommy detalha na HandmadeCon** (índice [36]):
  - **Método de tuning:** montar situações e ajustar variáveis até permitir atravessá-las (44:33).
  - **Mecânica exemplo:** o "S-jump", possível graças ao controle aéreo + atrito aéreo (49:13).
  - **Estado rastreado:** limiares de movimento e grandezas acompanhadas frame a frame (50:55).
  - **Wall jump com 200 ms de tolerância** ("Wall-jump has a 200ms leeway, but we didn't use edge roll-off") (54:07). O único número confirmado: 0,2 s ≈ 12 f.
  - **Controle e design de fase se complementam** (57:22).
  - **Sensação "instant-on instant-off"**, e por isso input digital, não analógico (59:18).
- **Botão de correr:** existe um botão de corrida (segurar para correr). A tech "sprint jumping" é "touch wall, let go of run, jump, hold run": sai da parede com menos velocidade e recupera rápido, num arco mais fechado [38].
- **Quirks de colisão como tech:** wall jump na borda de meio-bloco; wallslides com "low random chance"; o bug de autofire, em que o jogo passa a considerar pulo pressionado todo frame [38].
- **Design ao redor do movimento:** sem vidas, respawn quase imediato, fases curtas e o objetivo sempre à vista [37]. Isso sustenta a tentativa e erro de alta frequência, a mesma lógica de um jogo por tempo.

**Lição.**

- Controle aéreo forte **com** atrito aéreo, ou seja, frear no ar ao virar, permite correções em "S".
- Um tempo de tolerância de wall jump (200 ms) substitui a necessidade de precisão de frame.
- Input digital simplifica a leitura da resposta.

---

## 7. SpeedRunners (DoubleDutch Games)

**Fontes.** Wikipedia [39], wiki da comunidade [40] e guias da comunidade na Steam [41][42][43]. **Constantes numéricas: n/c.** Engine: Microsoft XNA [39].

- **Max Speed e Overmax.**
  - "Max Speed" é a velocidade máxima correndo no chão sem boost.
  - Acima disso é "Overmax", indicado por uma sombra ou rastro.
  - **O atrito do chão puxa para o Max nos dois sentidos:** acelera se o jogador está abaixo e freia se está acima.
  - Por isso, em Overmax, jogadores experientes fazem **bunny hops** para tocar o chão o mínimo possível [41][42].
- **Pulo:** preserva o momento ("your character will continue to travel forwards") [40]. É variável: segurar pula mais alto [43].
- **Gancho (balanço de pêndulo):**
  - atira a 45° na direção em que o jogador olha e prende só em tetos brancos, enquanto o botão estiver pressionado [40];
  - funciona como "a transfer of speed from the direction you were going before you hit the grapple, into the direction you are heading when you release it" [41];
  - **cair mais antes de agarrar dá mais velocidade no balanço**; quanto mais rápido entra, mais rápido balança [41][42];
  - a corda é uma linha: atravessa plataformas, desde que o corpo e o gancho não batam [41][43];
  - o gancho tem **atraso de viagem** e um **cooldown constante contado a partir do disparo**, não da conexão [41];
  - técnicas: "drop grappling" (agarrar e soltar logo para cair rápido e emendar outro gancho) e "jump grappling" [41].
- **Rampas:**
  - uma rampa descendente convertem velocidade, inclusive a de queda, em velocidade ao longo dela;
  - pousar perto da base dá mais ganho;
  - cair em chão plano perde "ALL of your speed";
  - é preciso estar **segurando** a direção da rampa no contato, porque a direção padrão é a do movimento [41];
  - rampas de teto aumentam a velocidade de queda [41].
- **Boost:** medidor consumível, recarregado em pontos do mapa. Não funciona durante o gancho. Serve para corrigir trajetória no ar [41][42].
- **Outros:** slide no ar cai mais rápido [42]; wall climb em paredes especiais, pulando de um lado para o outro [42][43].

**Lição.** Para um jogo de velocidade, os pontos centrais são:

- separar explicitamente **teto de velocidade "de chão"** e **velocidade acumulada**;
- definir **fontes de momento** (queda, rampa, gancho, boost) e **drenos** (atrito do chão, bater em parede, obstáculos);
- deixar o jogador decidir quando tocar o chão.

O gancho de pêndulo transforma altura em velocidade. Isso é diferente do "puxar até o ponto" do Silksong e do gancho atual do projeto.

---

## 8. Referências gerais de implementação

### 8.1 Kyle Pittman, "Building a Better Jump" (GDC 2016) [44]

- **Modele o jogador como projétil e desenhe a trajetória no papel.** Com altura de pico `h` e tempo até o pico `t_h`:

  `v0 = 2h / t_h`   e   `g = −2h / t_h²`
- **Com velocidade horizontal `v_x` e distância até o pico `x_h`:**

  `v0 = 2h·v_x / x_h`   e   `g = −2h·v_x² / x_h²`
- **Quebre a trajetória em arcos parabólicos,** mantendo posição e velocidade contínuas e trocando só a gravidade:
  - fast fall = gravidade maior depois do pico;
  - pulo variável = gravidade maior ao soltar;
  - pulo duplo = novo arco.
- **Integração:**
  - Euler é "instável";
  - Velocity Verlet é melhor;
  - com aceleração constante, `pos += vel·Δt + ½·acc·Δt²; vel += acc·Δt` é **exato**;
  - o erro quando a aceleração muda é `Δacc·Δt²`.

**Aplicação ao projeto.** O `characterMovement.cs` atual calcula `jumpSpeed = jumpHeight / timeToApex`, que é metade do `v0` de Pittman. Ele sobe em velocidade constante (gravidade 0) até atingir `jumpHeight` (ver 11.7). É o modelo de "hold" do Celeste, então a fórmula de Pittman **não** se aplica diretamente. Para o modelo do Celeste, use a fórmula de 2.7.

### 8.2 GMTK Platformer Toolkit [45]

Ensaio interativo com mais de 30 variáveis:

- velocidade máxima, aceleração, desaceleração, **turn speed**;
- **air acceleration/control/brake**;
- altura do pulo, **tempo até o ápice**, **gravidade de descida**, **jump cutoff**;
- **coyote time**, **jump buffer**, pulos aéreos, velocidade terminal.

Há scripts Unity para download na página (não baixados aqui). Os nomes de campos do `characterMovement.cs` atual (`turnSpeed`, `airTurnSpeed`, `upwardMovementMultiplier`, `downwardMovementMultiplier`, `speedYLimit`, `jumpBufferTime`, `coyoteTime`) seguem essa família de parâmetros.

### 8.3 Higher-Order Fun, "The guide to implementing 2D platformers" [46]

- **Quatro tipos de mundo:** tile puro, **tile "smooth"** (a escolha de Celeste e Sonic), bitmask e vetorial/física. Com engine de física, é preciso tomar cuidado para o jogo não ficar com cara de "generic physics-platformer".
- **Aceleração:** média ponderada ou soma com clamp de overshoot. "It's important to integrate the acceleration into the speed **before** moving the character, otherwise you'll introduce a one-frame lag into character input." Zere a velocidade no eixo ao colidir.
- **Quatro formas de controlar o pulo:** impulso, aceleração aérea, controle de subida (impulso contínuo ou gravidade suprimida enquanto o botão estiver pressionado, com limite de tempo) e pulos múltiplos.
- **One-way platforms:** só contam como obstáculo se o personagem **estava inteiramente acima antes do movimento**. Checar `vy > 0` está errado.
- **Plataformas móveis:** mova as plataformas antes dos personagens e carregue quem estava apoiado.
- **Subpixel:** posição inteira + resto em float, como no Celeste. A animação de antecipação deve ser **cosmética**: a ação acontece imediatamente.

---

## 9. Tabela comparativa normalizada

**Legenda:**

- **Celeste:** `t` = tile de 8 px; `alt` = 11 px.
- **Sonic:** `t` = bloco de 16 px; `alt` = 39 px.
- Tempos em segundos e em frames a 60 Hz.
- HK e Silksong estão em `u` (unidades Unity do próprio jogo), sem normalização confirmada.

| Parâmetro | Celeste (original → normalizado) | Sonic 16-bit (original → normalizado) | Hollow Knight | Silksong | Super Meat Boy | SpeedRunners |
|---|---|---|---|---|---|---|
| Velocidade máxima de corrida | 90 px/s → **11,25 t/s · 8,2 alt/s** [1] | 6 px/f = 360 px/s → **22,5 t/s · 9,2 alt/s** [11] | 8,3 u/s; 10 (Sprintmaster); 11,5 (combo) [12] | "112% faster" com sprint (wiki) [25]; base n/c | n/c | "Max Speed" n/c; Overmax sem teto documentado [41] |
| Tempo 0 → máx. (chão) | 0,09 s (5,4 f) (sim.) | ~2,13 s (128 f) (sim.) | ~instantâneo (inf.) [21] | n/c | "instant-on" [36] | n/c |
| Parar sem input (chão) | 0,09 s | ~2,13 s (atrito = acc) | ~instantâneo (inf.) | n/c | "instant-off" [36] | n/c |
| Virar (máx. → −máx.) | 0,18 s | 0,2 s até 0 com `dec` (12 f) | n/c | n/c | n/c | n/c |
| Aceleração no ar / chão | ×0,65 [1] | ×2 (air acc = 2×acc) [11] | n/c | n/c | "air control + air friction" [36] | boost no ar [41] |
| Gravidade | 900 px/s² → 112,5 t/s² | 787,5 px/s² → 49,2 t/s² | `gravityScale` (n/c) | n/c | n/c | n/c |
| Velocidade inicial do pulo | 105 px/s → 13,1 t/s | 390 px/s → 24,4 t/s | `JUMP_SPEED` (n/c) | n/c | n/c | n/c |
| Mecanismo do pulo variável | hold de vy constante por ≤ 0,2 s | cap vy ≥ −4 px/f ao soltar | vy constante por ≤ `JUMP_STEPS` [21] | n/c | n/c | sim (segurar = mais alto) [43] |
| Pulo máximo | 28,5 px → **3,56 t · 2,6 alt** (sim.) | ~97–100 px → ~6,1 t · 2,5 alt (sim.) | n/c | n/c | n/c | n/c |
| Pulo mínimo | ~7 px → 0,9 t (sim.) | ~37–39 px → 2,4 t (sim.) | n/c | n/c | n/c | n/c |
| Tempo até o ápice (máx.) | ~0,35 s (~21 f) (sim.) | ~0,5 s (~30 f) (sim.) | n/c | n/c | n/c | n/c |
| Queda máxima | 160 px/s → 20 t/s | sem teto documentado para S1/S2/S3K; o SPG cita 16 px/f só para o Sonic CD [11] | `MAX_FALL_VELOCITY` (n/c) | n/c | n/c | n/c |
| Fast fall | 240 px/s → 30 t/s (segurar baixo) | — | — | — | n/c | slide no ar cai mais rápido [42] |
| Meia gravidade no ápice | sim, ×0,5 com \|vy\| < 40 px/s | não (há air drag no topo) | n/c | n/c | n/c | n/c |
| Coyote time | 0,1 s (6 f; 5 f de ar) | não descrito no SPG | n/c | n/c | "didn't use edge roll-off" [36] (provável: sem coyote) | n/c |
| Buffer de pulo | ~4 f (comunidade) [7]; n/c no código | não descrito | n/c | n/c | n/c | n/c |
| Correção de quina | 4 px = 0,5 t (subida e dash) | snap ao chão até 14 px [11] | n/c | ledge grab automático (relatos) [33] | n/c | n/c |
| Wall slide | teto de 20 px/s (2,5 t/s) subindo até 160 px/s em 1,2 s | — | `WALLSLIDE_SPEED` (n/c) | "briefly sticks… slowly sliding" [26] | wallslide com chance aleatória (bug) [38] | paredes especiais [42] |
| Wall jump | vx 130 px/s (16,25 t/s), vy −105; input travado 0,16 s | — | diagonal para longe [17] | sim [26] | tolerância de **200 ms** [36] | sim [42] |
| Distância máx. da parede | 3 px de checagem (vão ≤ 2 px = 0,25 t) | — | n/c | n/c | n/c | n/c |
| Dash: velocidade / duração | 240 px/s (30 t/s) por 0,15 s → 4,5 t | spindash: GS 8–12 px/f (480–720 px/s) [11] | `DASH_SPEED` / `DASH_TIME` (n/c) | n/c | — | boost (medidor) [41] |
| Cooldown do dash | 0,2 s; recarga no chão após 0,1 s | — | 0,6 s (0,4 com Dashmaster) [14] | n/c | — | gancho: cooldown constante (n/c) [41] |
| Momento no dash | preserva X maior; sai a 160 px/s | — | para ao terminar [20] | n/c | — | — |
| Recarga aérea | chão | — | chão, pogo, parede [14][18] | pogo/chão (n/c detalhado) | — | — |

---

## 10. Conversão das constantes do Celeste para Unity (1 tile = 1 u = 8 px)

**Regras de conversão:**

- **Distâncias e velocidades:** dividir por 8.
- **Tempos, multiplicadores e estamina:** iguais.
- **Sinal:** o Celeste tem Y para baixo. No Unity, `JumpSpeed −105 px/s` vira **+13,125 u/s**, e a gravidade vira **−112,5 u/s²** (em módulo 112,5).
- **`gravityScale` equivalente:** com `Physics2D.gravity = −9,81` (valor do projeto), é 112,5 / 9,81 ≈ **11,47**.
- **Personagem maior:** se o personagem do projeto não tiver 1,375 u de altura, multiplique distâncias e velocidades por `H_projeto / 1,375` para manter as relações em "alturas de personagem".

| Constante (Celeste) | Celeste | Unity | Observação |
|---|---|---|---|
| Tile | 8 px | 1 u | |
| Tela 320×180 | px | 40 × 22,5 u | ortho size 11,25 para enquadrar igual |
| Hitbox em pé / agachado | 8×11 / 8×6 px | 1 × 1,375 / 1 × 0,75 u | |
| Hurtbox em pé / agachado | 8×9 / 8×4 px | 1 × 1,125 / 1 × 0,5 u | |
| `MaxRun` | 90 px/s | **11,25 u/s** | |
| `RunAccel` | 1000 px/s² | **125 u/s²** | também freia e vira |
| `RunReduce` | 400 px/s² | **50 u/s²** | só acima do máx. segurando a direção |
| `AirMult` | 0,65 | 0,65 | |
| `HoldingMaxRun` | 70 px/s | 8,75 u/s | |
| `WalkSpeed` | 64 px/s | 8 u/s | |
| `DuckFriction` | 500 px/s² | 62,5 u/s² | |
| `DuckCorrectCheck` / `DuckCorrectSlide` | 4 px / 50 px/s | 0,5 u / 6,25 u/s | |
| `Gravity` | 900 px/s² | **112,5 u/s²** | gravityScale ≈ 11,47 |
| `HalfGravThreshold` | 40 px/s | **5 u/s** | |
| `MaxFall` | 160 px/s | **20 u/s** | |
| `FastMaxFall` | 240 px/s | **30 u/s** | |
| `FastMaxAccel` | 300 px/s² | 37,5 u/s² | |
| `JumpSpeed` | −105 px/s | **+13,125 u/s** | |
| `JumpHBoost` | 40 px/s | **5 u/s** | aditivo |
| `VarJumpTime` | 0,2 s | 0,2 s | |
| `JumpGraceTime` (coyote) | 0,1 s | 0,1 s | |
| `CeilingVarJumpGrace` | 0,05 s | 0,05 s | |
| `UpwardCornerCorrection` | 4 px | **0,5 u** | |
| `WallSpeedRetentionTime` | 0,06 s | 0,06 s | |
| `WallJumpCheckDist` | 3 px | **0,375 u** | vão efetivo ≤ 0,25 u |
| `WallJumpForceTime` | 0,16 s | 0,16 s | só se havia input horizontal |
| `WallJumpHSpeed` | 130 px/s | **16,25 u/s** | |
| `WallSlideStartMax` | 20 px/s | **2,5 u/s** | |
| `WallSlideTime` | 1,2 s | 1,2 s | |
| `BounceSpeed` / `SuperBounceSpeed` | −140 / −185 px/s | 17,5 / 23,125 u/s | molas e quiques; var time 0,2 s |
| `SuperJumpH` | 260 px/s | **32,5 u/s** | |
| `DuckSuperJumpXMult` / `YMult` | 1,25 / 0,5 | 1,25 / 0,5 | hyper = 40,625 u/s em X |
| `DodgeSlideSpeedMult` | 1,2 | 1,2 | dash slide / ultra |
| `SuperWallJumpSpeed` | −160 px/s | **+20 u/s** | |
| `SuperWallJumpH` | 170 px/s | **21,25 u/s** | |
| `SuperWallJumpVarTime` | 0,25 s | 0,25 s | |
| `DashSpeed` | 240 px/s | **30 u/s** | diagonal normalizada ≈ 21,2 u/s por eixo (inf.) |
| `EndDashSpeed` | 160 px/s | **20 u/s** | |
| `EndDashUpMult` | 0,75 | 0,75 | |
| `DashTime` | 0,15 s | 0,15 s | 4,5 u |
| `DashCooldown` | 0,2 s | 0,2 s | |
| `DashRefillCooldown` | 0,1 s | 0,1 s | |
| `DashAttackTime` | 0,3 s | 0,3 s | |
| `DashCornerCorrection` | 4 px | **0,5 u** | |
| `DashVFloorSnapDist` | 3 px | 0,375 u | |
| `DashHJumpThruNudge` | 6 px | 0,75 u | |
| `JumpThruAssistSpeed` | −40 px/s | +5 u/s | |
| `ReboundSpeedX` / `ReboundSpeedY` | 120 / −120 px/s | 15 / +15 u/s | ricochete de bloco; var 0,15 s |
| `ClimbMaxStamina` | 110 | 110 | |
| `ClimbUpCost` / `ClimbStillCost` / `ClimbJumpCost` | 45,45/s · 10/s · 27,5 | idem | |
| `ClimbTiredThreshold` | 20 | 20 | |
| `ClimbUpSpeed` / `ClimbDownSpeed` / `ClimbSlipSpeed` | −45 / 80 / 30 px/s | +5,625 / −10 / −3,75 u/s | |
| `ClimbAccel` | 900 px/s² | 112,5 u/s² | |
| `ClimbCheckDist` / `ClimbUpCheckDist` | 2 / 2 px | 0,25 / 0,25 u | |
| `ClimbNoMoveTime` | 0,1 s | 0,1 s | |
| `ClimbGrabYMult` | 0,2 | 0,2 | |
| `ClimbHopY` / `ClimbHopX` | −120 / 100 px/s | +15 / 12,5 u/s | |
| `ClimbHopForceTime` / `ClimbJumpBoostTime` | 0,2 / 0,2 s | idem | |
| `LiftXCap` / `LiftYCap` | 250 / −130 px/s | 31,25 / +16,25 u/s | |

**Cuidado com o passo de tempo.**

- Timers em segundos convertem direto. **Janelas em frames não.**
- A 50 Hz (padrão do Unity, `Fixed Timestep = 0.02` também no `TimeManager.asset` do projeto):
  - 0,1 s = 5 ticks, contra 6 a 60 Hz;
  - 0,05 s = 2,5 ticks, que é ambíguo.
- A distância percorrida em cada tick também muda (o dash anda 0,6 u por tick em vez de 0,5 u), o que afeta a correção de quina e as checagens de 1 px.
- Recomendação na seção 11.3.

---

## 11. Padrões de arquitetura de controlador para Unity 6 / C#

### 11.1 Três caminhos

| Abordagem | Como é | Prós | Contras |
|---|---|---|---|
| **A. Cinemático próprio** (estilo Actor do Celeste) | `Rigidbody2D` Kinematic (ou só colisor), movimento via `Rigidbody2D.Cast`/`BoxCast` por eixo, X depois Y, com callbacks `OnCollideH`/`OnCollideV` | Controle total da ordem das operações; ajudas invisíveis triviais (correção de quina = tentar ±N offsets no callback); sem atrito nem quique da engine; determinístico | Mais código (slopes, plataformas móveis, one-way); precisa tratar "skin" e contatos na mão; Kinematic só colide com Dynamic por padrão, a menos que `useFullKinematicContacts` esteja ligado [53] |
| **B. Dinâmico com velocidade sobrescrita** (estilo HK; é o que o projeto faz hoje) | `Rigidbody2D` Dynamic; código seta `linearVelocity` em `FixedUpdate`; gravidade via `gravityScale` | Pouco código; colisões e triggers "de graça"; bom com objetos físicos | O solver interfere: atrito em paredes (gruda), ricochete, contato em emendas de tiles, depenetração empurrando; troca de `gravityScale` por frame vira estado escondido; corner correction e checagens de 1 px ficam mais difíceis |
| **C. `Rigidbody2D.Slide`** (Unity 2023.1+/6) | Kinematic + `Slide(velocity, deltaTime, SlideMovement)`: desliza ao longo das superfícies em iterações, com gravidade e escorregamento em rampas opcionais [47][48] | Resolve deslizamento, rampas e ancoragem (`surfaceAnchor`) sem escrever solver; `useSimulationMove` escolhe mover já ou via `MovePosition` [47][48] | É uma "caixa preta" para regras finas: corner correction e speed retention ainda precisam de código próprio; a ordem X→Y não é garantida (o slide é vetorial) |

**Recomendação para um plataforma de precisão por tempo:** usar **A**, com **C** como utilitário para rampas se houver rampas. Os motivos:

- cada ajuda da seção 2.12 depende de saber **em qual eixo** e **em qual pixel** a colisão aconteceu;
- o modelo do Celeste (Approach + hold de pulo + callbacks por eixo) cabe inteiro nesse desenho.

### 11.2 Onde ler input e onde simular

- **Ler input em `Update`** e **gravar eventos com carimbo**, por exemplo `jumpPressedAt = tempoDeSimulação`. Consumir esses eventos no passo de simulação.
  - `WasPressedThisFrame()` lido em `FixedUpdate` pode perder o aperto (frame sem tick) ou vê-lo duas vezes (dois ticks no mesmo frame).
  - O Input System permite processar eventos em Dynamic Update, em Fixed Update ou manualmente [55].
- **Buffer como timer, com consumo explícito** (padrão `ConsumeBuffer` do Celeste): a ação que usa o aperto zera o buffer [1].
- **Integrar a aceleração na velocidade antes de mover,** senão surge 1 frame de atraso [46].

### 11.3 Passo de tempo: 0,02 s contra 60 Hz

| Opção | Como | Comentário |
|---|---|---|
| Manter 50 Hz | `Time.fixedDeltaTime = 0.02` (padrão [49]) e constantes em segundos | Simples, mas as janelas "em frames" das referências não batem, e a movimentação fica a 50 Hz sobre tela a 60/120/144 Hz. Exige interpolação |
| **Ir para 60 Hz** | `Time.fixedDeltaTime = 1/60f` | Paridade com Celeste e Sonic; custo baixo num 2D |
| Loop próprio | `Physics2D.simulationMode = Script` e acumulador no `Update` chamando `Tick(1/60)` N vezes e `Physics2D.Simulate` quando necessário [51] | Controle total e replays determinísticos (ghosts de speedrun); mais trabalho |

- **Renderização entre ticks:**
  - `Rigidbody2D.interpolation = Interpolate` usa a posição anterior e suaviza [52];
  - num controlador próprio, interpolar o transform visual entre o estado anterior e o atual;
  - evitar setar `transform.position` direto num corpo interpolado.
- Com "Fixed updates", "each frame has one fixed update or none at all" quando o framerate é maior que a taxa fixa, e "one or more" quando é menor [50]. Isso reforça a necessidade de desacoplar input e simulação.

### 11.4 Subpixel e grade

- Num jogo de pixel art, guardar a posição lógica numa grade (p. ex. 1/8 u, ou 1/PPU) com **resto float**, como o `movementCounter` do Celeste [3][8][46], dá:
  - colisões estáveis;
  - checagens "a N pixels" com significado;
  - alinhamento visual.
- Se a arte não exigir grade, dá para usar float puro. Nesse caso, as checagens de 1 px viram distâncias fixas (0,125 u), e o `contactOffset` do Physics2D precisa ser considerado nos casts (o projeto usa 0,01).

### 11.5 Máquina de estados

- **Pequena, com callbacks:** um `enum` de estados, `Update()` que retorna o próximo estado, `Begin()`/`End()` e, opcionalmente, uma coroutine ou timer para ações com duração, como o dash (padrão do Monocle [1]).
  - Estados sugeridos: Normal, Dash, WallClimb/Grab, Grapple, Dead/Respawn.
- **Evite uma classe por estado** se a interação entre estados for alta. É o argumento dos devs do Celeste [2].
- **Um meio-termo:** um `PlayerController` com a ordem de operações explícita e "módulos" de dados, como um `MovementProfile` em ScriptableObject com as constantes da seção 10. Isso também casa com o plano do projeto de ler `PlayerStats` (upgrades de roguelike alterando constantes, não código).

### 11.6 Esqueleto de tick (pseudo-C#, inspirado na seção 2.5)

```csharp
void Tick(float dt) {                          // chamado a 60 Hz
    UpdateTimers(dt);                          // coyote, buffer, varJump, cooldowns, forceMoveX
    groundInfo = ProbeGround();                // cast de 1 "pixel" para baixo, só se vy <= 0
    state = state switch {                     // decide velocidade; pode trocar de estado
        State.Normal => NormalUpdate(dt),
        State.Dash   => DashUpdate(dt),
        State.Climb  => ClimbUpdate(dt), _ => state };
    Assists(dt);                               // jump-thru assist, dash floor snap
    MoveX(velocity.x * dt, OnCollideH);        // cast; corner correction; speed retention
    MoveY(velocity.y * dt, OnCollideV);        // cast; corner correction de teto; dash slide
}
float Approach(float v, float target, float maxDelta) =>
    v < target ? Mathf.Min(v + maxDelta, target) : Mathf.Max(v - maxDelta, target);
```

### 11.7 Observações sobre o código atual (para o SPEC)

Lido de `Assets/scripts/characterMovement.cs`, `GrapplingHook.cs` e `ProjectSettings/*`:

- **Estrutura:** Rigidbody2D **Dynamic**. Input em `Update` (`WasPressedThisFrame`) e velocidade em `FixedUpdate` a **0,02 s**.
- **Detecção:** chão por `OverlapCircle`, parede por `Raycast`.
- **Pulo:**
  - `jumpSpeed = jumpHeight / timeToApex`;
  - subida com `gravityScale = 0` e velocidade constante até atingir `jumpHeight` (versão do "hold" do Celeste/HK, mas medida por altura, não por tempo);
  - `hangTime` zera a gravidade no ápice (versão "congelada" da meia gravidade).
- **Gancho:** "puxa até o ponto" (velocidade constante para o alvo e depois `AddForce` impulsivo). É o modelo Silksong/Clawline, não o pêndulo do SpeedRunners.
- **O que falta frente às referências:**
  - correção de quina;
  - retenção de velocidade;
  - buffer consumível unificado (hoje só para pulo);
  - preservação de momento explícita;
  - separação por eixo nas colisões.

---

## 12. Técnicas avançadas e tech de speedrun

| Técnica | Jogo | Mecanismo (código / regra) | Efeito | Desejável num jogo por tempo? |
|---|---|---|---|---|
| **Super** | Celeste | Pular durante um dash horizontal com coyote ativo → X = 260 px/s fixo [1] | Pulo longo e rápido | **Sim.** Regra simples, visível e ensinável; recompensa timing |
| **Hyper** | Celeste | Super agachado (dash diagonal para baixo que vira slide) → X ×1,25, Y ×0,5 [1] | Mais rápido e mais baixo | **Sim**, se ensinado no jogo (o Celeste só o apresenta no 8C/9 [6]) |
| **Wavedash** | Celeste | Hyper iniciado no ar (o dash diagonal pousa ainda dentro do `DashTime`) [1][6] | Hyper em espaço curto, mantendo o dash | **Sim com cautela.** Janela apertada; considerar alargar |
| **Extended super/hyper** | Celeste | Pular depois da recarga do dash (`DashRefillCooldown` 0,1 s) [1][6] | Mantém o dash para o ar | **Sim.** Cria decisão de rota |
| **Ultra** | Celeste | Dash diagonal para baixo com X alto preserva X; ao tocar o chão, ×1,2 [1][6] | Acúmulo de velocidade sem limite | **Talvez.** Ótimo para a expressividade de runners, mas precisa de um **cap** ou de fases que o comportem, senão quebra o balanceamento do timer |
| **Wallbounce** (super wall jump) | Celeste | Pular perto de parede durante um dash para cima → vx 170, vy −160, var 0,25 s [1] | Subida vertical rápida | **Sim.** Janela de distância generosa [4] |
| **Neutral jump** | Celeste | Wall jump sem input não trava a direção → volta à parede [1] | Escala sem estamina | **Sim.** Emerge naturalmente; documentar |
| **Corner boost** | Celeste | Climb jump no topo da parede preserva o momento (5–7 px do topo) [6]; provável ligação com a retenção de velocidade de 0,06 s (inf.) | Mantém a velocidade ao passar por quinas | **Talvez.** Tornar menos "pixel-perfect" |
| **Bunnyhop** | Celeste, SpeedRunners | Pular antes de o atrito do chão agir; +40 px/s de `JumpHBoost` por pulo (Celeste) [1]; atrito puxa para o Max (SR) [41] | Preserva excesso de velocidade | **Sim.** É o coração de um jogo de velocidade |
| **Demodash / crouch dash** | Celeste | Direção do dash lida ~4 f depois do aperto; agachado encolhe a hitbox [1][6] | Passa por vãos baixos | **Sim, como ação explícita** (o 1.4 adicionou um botão [6]); **não** como truque de input |
| **Liftboost** | Celeste | Soma a velocidade da plataforma (caps 250/−130 px/s) com persistência curta [1][4] | Pulos amplificados | **Sim**, se as plataformas móveis forem parte do design |
| **Spindash / rampas** | Sonic, SpeedRunners | Carga → GS 8–12 px/f [11]; slope factor e conversão de queda em velocidade [11][41] | Momento vindo do terreno | **Sim, pontualmente** (rampas de impulso marcadas) |
| **Drop grappling / swing** | SpeedRunners | Cair antes do gancho aumenta a velocidade do balanço; transferência de direção ao soltar [41] | Converte altura em velocidade | **Sim**, se o gancho virar pêndulo (hoje é "puxar") |
| **Pogo chain** | HK, Silksong | Quique recarrega dash e asas [14][18]; Silksong varia por crest [31][32] | Rotas aéreas e resets | **Sim**, se houver ataque para baixo; manter consistente |
| **Sprint → pulo → dash** | Silksong | Sprint aumenta a distância do pulo; dash no ar [25] | Travessia longa | **Sim**, com velocidade de sprint como estado |
| **Wall jump com run solto** (sprint jump) | SMB | Soltar o run no contato com a parede → arco mais fechado [38] | Subida mais rápida | **Talvez.** Pouco legível |
| **Pause buffer / múltiplos botões de pulo / autofire** | Celeste, SMB | Explora o sistema de input (pausa, dois dispositivos) [7][38] | Frames grátis | **Não.** Evitar por design: buffer generoso, ignorar input "duplicado", não processar input na pausa |
| **Cura para "parar" no ar** | Silksong | Animação de cura segura a queda [34] | Alcança bordas | **Não intencional.** Decidir se estados de ação param ou não a gravidade |

**Diretrizes para o projeto:**

1. Tech **intencional** nasce de regras gerais e consistentes (as do Celeste). Tech **acidental** nasce de bugs ou de input (pause buffer, autofire). A primeira fica; a segunda sai.
2. Todo multiplicador de velocidade reaplicável (ultra, drop grapple) precisa de um **cap** ou de ser considerado no tempo-alvo da fase.
3. Janelas de tech devem ser **em segundos, com buffer**, não "frame perfect". Isso vale em dobro a 50 Hz.
4. **Determinismo** (tick fixo + input por tick) permite ghosts e replays, que são valiosos num jogo por tempo.

---

## 13. Referências

1. Celeste, `Player.cs` (código-fonte oficial, MIT). https://raw.githubusercontent.com/NoelFB/Celeste/master/Source/Player/Player.cs
2. Celeste, README e `Source/Player/Readme.md` (notas dos devs). https://github.com/NoelFB/Celeste · https://github.com/NoelFB/Celeste/blob/master/Source/Player/Readme.md
3. Maddy Thorson, "Celeste & TowerFall Physics". https://maddymakesgames.com/articles/celeste_and_towerfall_physics/index.html (original no Medium: https://medium.com/@MattThorson/celeste-and-towerfall-physics-d24bd2ae0fc5)
4. Maddy Thorson, "Celeste & Forgiveness". https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html
5. Celeste Classic, porta C# (`Classic.cs`, `Emulator.cs`). https://github.com/NoelFB/Celeste/tree/master/Source/PICO-8
6. Celeste Wiki (Fandom), "Moves" (lido via API). https://celestegame.fandom.com/wiki/Moves
7. Celeste Wiki (celeste.ink), "Tech" e "Pause Buffering". Página bloqueada por verificação anti-bot; usados apenas os trechos exibidos pela busca. https://celeste.ink/wiki/Tech
8. Everest (mod loader do Celeste), patch `Actor.cs` (`movementCounter`). https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/Patches/Actor.cs
9. ExtendedVariantMode (mod), `Variants/BufferableGrab.cs` e `Variants/CoyoteTime.cs`. https://github.com/maddie480/ExtendedVariantMode
10. Sonic Retro, "Sonic Physics Guide" (original). https://info.sonicretro.org/Sonic_Physics_Guide
11. Espelho offline do SPG em Markdown (trentbrew/sonic-physics-guide; seções Forces, Slope Physics, Slope Collision, Terrain Collision, Main Game Loop, Characters, Special Abilities). https://github.com/trentbrew/sonic-physics-guide
12. Hollow Knight Wiki (Fandom), "Sprintmaster". https://hollowknight.fandom.com/wiki/Sprintmaster
13. Hollow Knight Wiki (Fandom), "Dashmaster". https://hollowknight.fandom.com/wiki/Dashmaster
14. Hollow Knight Wiki (Fandom), "Mothwing Cloak". https://hollowknight.fandom.com/wiki/Mothwing_Cloak
15. Hollow Knight Wiki (Fandom), "Shade Cloak". https://hollowknight.fandom.com/wiki/Shade_Cloak
16. Hollow Knight Wiki (Fandom), "Crystal Heart". https://hollowknight.fandom.com/wiki/Crystal_Heart
17. Hollow Knight Wiki (Fandom), "Mantis Claw". https://hollowknight.fandom.com/wiki/Mantis_Claw
18. Hollow Knight Wiki (Fandom), "Monarch Wings". https://hollowknight.fandom.com/wiki/Monarch_Wings
19. Hollow Knight Wiki (Fandom), "Nail" (seção Nail-bouncing). https://hollowknight.fandom.com/wiki/Nail
20. flibber-hk, HollowKnight.SkillUpgrades (código e README). https://github.com/flibber-hk/HollowKnight.SkillUpgrades
21. kot9pa16lvl, HKCelesteDash (reimplementação de `Jump`/movimento sobre `HeroController`). https://github.com/kot9pa16lvl/HKCelesteDash
22. Hollow Knight Modding API docs, "Classes". https://radiance.synthagen.net/apidocs/Classes.html
23. Team Cherry, "Inside the mind of a bug: Unity and PlayMaker". https://www.teamcherry.com.au/blog/inside-the-mind-of-a-bug-unity-and-playmaker
24. Wikipedia, "Hollow Knight: Silksong". https://en.wikipedia.org/wiki/Hollow_Knight:_Silksong
25. Hollow Knight Wiki, "Swift Step". https://hollowknight.wiki/w/Swift_Step
26. Hollow Knight Wiki, "Cling Grip". https://hollowknight.wiki/w/Cling_Grip
27. Hollow Knight Wiki, "Clawline". https://hollowknight.wiki/w/Clawline
28. Hollow Knight Wiki, "Drifter's Cloak". https://hollowknight.wiki/w/Drifter%27s_Cloak
29. Hollow Knight Wiki, "Silk Soar". https://hollowknight.wiki/w/Silk_Soar
30. Hollow Knight Wiki (Fandom), "Faydown Cloak". https://hollowknight.fandom.com/wiki/Faydown_Cloak
31. Hollow Knight Wiki, "Hunter Crest". https://hollowknight.wiki/w/Hunter_Crest
32. Hollow Knight Wiki, "Wanderer Crest". https://hollowknight.wiki/w/Wanderer_Crest
33. Relatos sobre o ledge grab do Silksong (apenas trechos de busca, não abertos): Steam, "Anyone else find grabbing ledges weirdly unreliable?" https://steamcommunity.com/app/1030300/discussions/0/595159120097973357/ · "How to fix Auto Sprint after a ledge grab?" https://steamcommunity.com/app/1030300/discussions/0/596286707636147581/ · allthings.how https://allthings.how/silksong-double-jump-gain-height-with-air-dash-and-pogo-chains/
34. Yahoo Tech, "Silksong player uses every speedrunning…". https://tech.yahoo.com/gaming/articles/silksong-player-uses-every-speedrunning-212546100.html
35. Isetta Engine, entrevista com Tommy Refenes, "The Engine Sandwich". https://isetta.io/interviews/TommyRefenes-interview/ (fonte md: https://github.com/Isetta-Team/Isetta-Website-Raw/blob/master/docs/interviews/TommyRefenes-interview.md)
36. Handmade Hero Episode Guide, HandmadeCon 2015: Tommy Refenes (índice anotado). https://guide.handmadehero.org/hmcon/2015/01/
37. Game Developer, "Postmortem: Team Meat's Super Meat Boy". https://www.gamedeveloper.com/audio/postmortem-team-meat-s-i-super-meat-boy-i-
38. Speed Demos Archive, "Super Meat Boy" (notas de tricks). https://speeddemosarchive.com/SuperMeatBoy.html
39. Wikipedia, "SpeedRunners". https://en.wikipedia.org/wiki/SpeedRunners
40. SpeedRunners Wiki (Fandom), "Controls" e "Tips". https://speedrunners.fandom.com/wiki/Controls · https://speedrunners.fandom.com/wiki/Tips
41. AbMundane ("Mundy"), "Mundy's Momentum Guide", Steam Guide. https://steamcommunity.com/sharedfiles/filedetails/?id=602551647
42. "Newcomer's Guide to SpeedRunners", Steam Guide. https://steamcommunity.com/sharedfiles/filedetails/?id=531895594
43. "Some advice a.k.a techniques basic and more advanced", Steam Guide. https://steamcommunity.com/sharedfiles/filedetails/?id=197223650
44. J. Kyle Pittman, "Math for Game Programmers: Building a Better Jump" (GDC 2016), slides. https://media.gdcvault.com/gdc2016/Presentations/Pittman_Kyle_BuildingBetterJump.pdf · vídeo: https://www.youtube.com/watch?v=hG9SzQxaCm8
45. Game Maker's Toolkit, "Platformer Toolkit". https://gmtk.itch.io/platformer-toolkit
46. Rodrigo Monteiro (Higher-Order Fun), "The guide to implementing 2D platformers". http://higherorderfun.com/blog/2012/05/20/the-guide-to-implementing-2d-platformers/
47. Unity 6 Scripting API, `Rigidbody2D.Slide`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D.Slide.html
48. Unity 6 Scripting API, `Rigidbody2D.SlideMovement`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D.SlideMovement.html
49. Unity Scripting API, `MonoBehaviour.FixedUpdate` ("0.02 seconds (50 calls per second) is the default"). https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.FixedUpdate.html
50. Unity 6 Manual, "Fixed updates". https://docs.unity3d.com/6000.1/Documentation/Manual/fixed-updates.html
51. Unity 6 Scripting API, `SimulationMode2D`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SimulationMode2D.html
52. Unity 6 Scripting API, `Rigidbody2D.interpolation`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D-interpolation.html
53. Unity 6 Manual, "Kinematic Body Type reference". https://docs.unity3d.com/6000.0/Documentation/Manual/2d-physics/rigidbody/body-types/kinematic/kinematic-body-type-reference.html
54. Unity 6 Scripting API, `Rigidbody2D.Cast`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody2D.Cast.html
55. Unity Input System 1.11, "Settings" (Update Mode). https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Settings.html
56. SpeedRunners 2 Wiki (fã), "Movement techniques". https://speedrunners2.run/guides/movement-techniques/
57. Game Truth, "Silksong Movement Guide". Consultado e **descartado** como fonte de números por falta de metodologia. https://www.gametruth.com/guides/hollow-knight-silksong-movement-guide-dashes-jumps-and-advanced-platforming/

---

## Apêndice A: fontes que falharam ou não confirmaram dados

| Fonte | Problema | O que foi feito |
|---|---|---|
| Medium (artigo de física da Maddy) | HTTP 403 | Lido no site da autora [3] |
| celeste.ink (Miraheze) | Verificação anti-bot (403) | Usados só trechos de busca [7] e a wiki Fandom via API [6] |
| info.sonicretro.org (SPG) | Desafio anti-bot (Anubis) | Lido o espelho offline [11], que cita a página original de cada seção |
| web.archive.org | Bloqueado ou conexão resetada | — |
| DeepWiki (decompilação do Silksong) | HTTP 429 | Nenhum valor interno do Silksong confirmado |
| Nexus Mods | HTTP 403 | — |
| speedrun.com (fóruns do SMB) | HTTP 403 | — |
| Constantes do `HeroController` do HK (`JUMP_SPEED`, `JUMP_STEPS`, `MAX_FALL_VELOCITY`, `DASH_SPEED`, `DASH_TIME`, `WALLSLIDE_SPEED`, gravidade) | Valores serializados no prefab, não publicados | Confirmados só os valores das wikis (seção 4.2) |
| Classe `Input` do Celeste (tempo exato de buffer) | Não publicada | Usada a documentação da comunidade (~4 frames) |
| Scripts do GMTK Platformer Toolkit | Não baixados (download via itch) | Só os parâmetros descritos na página |
| SMB e SpeedRunners | Sem constantes numéricas públicas | Exceção: tolerância de wall jump do SMB, 200 ms [36] |
