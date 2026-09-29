# Pesquisa — Game feel da movimentação

**Jogos analisados:** Celeste (foco principal), Hollow Knight, Hollow Knight: Silksong, Sonic (clássico e moderno), SpeedRunners, Super Meat Boy
**Destino:** PRD/SPEC do refactor de movimento — plataforma 2D pixel art cyberpunk, Unity 6, speedrun por fase com timer, estrutura roguelike, personagem Cyborg com gancho.
**Data da pesquisa:** 29/09/2026
**Escopo:** tudo o que faz o movimento *parecer* fluido, responsivo e satisfatório **além da física**. Constantes de física/controle (aceleração, gravidade, coyote time etc.) ficam com outro documento; aqui elas só aparecem pelo ângulo da **percepção**.

---

## 0. Como ler este documento

- **Convenções:** "f" = frames a 60 fps (1 f ≈ 16,7 ms). "px" = pixel de jogo (no Celeste, 1 tile = 8 px e a tela tem 320×180 [5]). Escala `(X; Y)` = multiplicador do sprite (1 = normal).
- **Nível de evidência** (aparece entre parênteses quando importa):
  - **(código)**: lido direto no código-fonte publicado (Celeste `Player.cs` [1], `Classic.cs` [3], Monocle [4], scripts MIT do Platformer Toolkit [24]).
  - **(dev)**: declaração de desenvolvedor (entrevista, changelog, postmortem).
  - **(análise)**: terceiros (GMTK, wikis, reviews, guias).
  - **(inferência)**: dedução minha a partir do código; não foi verificada em jogo.
  - **não confirmado**: não encontrei fonte que eu tenha lido. Nenhum número deste documento foi inventado; valores "sugeridos" do catálogo (§10) são pontos de partida de design e estão rotulados assim.
- As conversões s → frames são aritméticas (ex.: 0,05 s × 60 = 3 f).

---

## 1. Sumário executivo — princípios que se repetem

1. **A resposta aparece no mesmo frame do input.** No Celeste, `Jump()` aplica velocidade, squash `(0,6; 1,4)`, 4 partículas de poeira e som no mesmo update (código) [1]. Para o jogador, o que vale é o primeiro frame em que algo muda na tela, mesmo que a física "real" ainda esteja acelerando.
2. **O jogo trabalha pela intenção do jogador, não pela simulação.** Coyote time, buffer, correção de quina e janelas de wall jump mais largas foram feitos para ninguém perceber que estão lá (dev) [5][7]. O jogador só sente que o jogo é justo: "o jogo quer que você vença" [5].
3. **Feedback curto, pequeno e em camadas.** No dash do Celeste há freeze de 0,05 s, tremor direcional de 0,2 s, rumble forte, rastro de sombras, "slash" branco, distorção de tela, partículas e som (código) [1]. Cada camada é sutil. O GMTK descreve o jogo como "não é um festival de juice estilo Vlambeer" [7].
4. **A intensidade segue uma hierarquia.** No Celeste, o dash usa rumble Strong, a mola usa Medium, a aterrissagem usa Light e o pulo não tem rumble. A aterrissagem só solta 8 partículas quando a queda é rápida (código) [1]. Assim os eventos continuam distinguíveis entre si.
5. **O estado dos recursos aparece no próprio personagem, sem HUD.** O cabelo da Madeline fica vermelho, azul ou rosa e pisca branco ao recarregar. Com stamina baixa, o sprite pisca em vermelho, aparece suor e o controle vibra sem parar (código) [1]. O jogador lê o recurso sem tirar o olho da ação [7].
6. **Freeze curto serve de ênfase e também de janela de graça.** O changelog oficial diz que o freeze de 0,1 s das explosões "é efetivamente um período de graça de 0,1 s" (dev) [8]. Durante o freeze a engine continua lendo input [4].
7. **Câmera parada favorece precisão, câmera adiantada favorece velocidade.** "Se o jogo é sobre pular, dê fricção ao personagem e mantenha a câmera parada para você acertar cada pulo" (análise) [22]. O Sonic CD desloca o foco 64 px para a frente quando está rápido [25].
8. **A suavização de câmera independe do framerate e pode ser diferente por eixo.** O Celeste usa `1 − 0,01^dt` (código) [1]. Eiserloh recomenda tratar horizontal, vertical, subida e descida de forma assimétrica [15].
9. **Morrer precisa ser barato.** Sem vidas, respawn rápido, fases curtas e objetivo à vista (dev) [45]. "A penalidade é quanto tempo leva para voltar a jogar" (dev) [46].
10. **Num jogo de speedrun, o tempo já é a penalidade.** O timer continua rodando durante a morte e nunca fica oculto (Celeste, dev) [8]. Isso só é aceitável se o respawn for rápido e previsível.
11. **Registrar as tentativas transforma fracasso em informação.** O Super Meat Boy mantém o sangue nas paredes e mostra o replay de todas as tentativas [45][48]. No speedrun, o equivalente é o ghost do melhor tempo.
12. **A animação comunica velocidade.** No Sonic, a duração de cada frame da corrida é `8 − |velocidade|` [26]. O Celeste troca `RunSlow` por `RunFast` e usa `JumpFast`/`FallFast` quando está acima da velocidade máxima de corrida (código) [1].
13. **Animação não rouba o controle, e quando rouba é por pouco tempo e de propósito.** O dash do Celeste tira o controle por 0,15 s e depois devolve (análise) [7]. No Hollow Knight, o tempo sem input após levar dano é de 0,2 s, e há um amuleto que reduz isso (análise) [31].
14. **Opções de acessibilidade para o próprio feedback.** Screenshake 0/50/100% (Celeste passou a usar 50% como padrão), rumble 0/50/100%, modo fotossensível e velocidade do jogo (dev) [8][10]. O Hollow Knight adicionou multiplicadores de shake e rumble depois de críticas [29][30].
15. **Todo juice tem custo.** Excesso de shake atrapalha a leitura e causa náusea [14][30]. Juice demais pode tirar o contexto do jogo [19]. "Camera shake é como sal" [15].

---

## 2. Celeste (2018) — análise profunda

### 2.1 Contexto técnico relevante para o feel

- **Código:** a classe `Player` publicada tem cerca de 5.400 linhas [21] (o arquivo baixado tem 5.471). Estados, animação e efeitos ficam num arquivo só, de propósito: "o comportamento do jogador precisa ser muito bem ordenado e ajustado" (dev) [2]. Dois programadores, Maddy e Noel (dev) [2].
- **Engine:** Monocle/XNA com passo fixo. Todo `Update` usa `Engine.DeltaTime`. O **freeze** é um `FreezeTimer` global que pula o `scene.Update()` inteiro, mas `MInput.Update()` continua rodando antes disso (código) [4]:
  ```csharp
  // Monocle Engine.Update (resumo) [4]
  MInput.Update();                       // input continua sendo lido
  if (FreezeTimer > 0) FreezeTimer = Math.Max(FreezeTimer - RawDeltaTime, 0);
  else { scene.BeforeUpdate(); scene.Update(); scene.AfterUpdate(); }
  ```
- **Resolução e posições inteiras:** a resolução nativa é 320×180 [5]. Colisores ficam em posições inteiras e o movimento é acumulado em "remainder" até fechar 1 px [6]. Na renderização, `Sprite.RenderPosition` é arredondado para baixo (`Floor()`) (código) [1]. O resultado é que o sprite nunca aparece em posição sub-pixel e não "treme".
- **Buffers de input:** o `VirtualButton` do Monocle guarda o aperto por `BufferTime`. O contador diminui com `Engine.DeltaTime` e é zerado com `ConsumeBuffer()` (código) [4]. O `Player.cs` consome buffer do **Jump** e do **Dash** (`Input.Jump.ConsumeBuffer()`, `Input.Dash.ConsumeBuffer()`) (código) [1]. A **duração** do buffer está em `Input.cs`, que não foi publicado: **não confirmado**.

### 2.1b Como os números de controle são *percebidos* (sem tabela de física)

O GMTK mediu o Celeste quadro a quadro e traduziu os números em sensação (análise) [7]:
- **Arranque e parada muito curtos:** cerca de 6 f até a velocidade máxima e 3 f para parar. Segundo o GMTK, isso é "alto o bastante para Madeline parecer humana… curto o bastante para ela fazer quase instantaneamente o que você manda", e "raramente você escorrega da beira de uma plataforma".
- **Velocidade máxima baixa e sem botão de correr:** "rápido o bastante para parecer fluido, lento o bastante para você se sentir no controle o tempo todo".
- **Pulo baixo (~3× a altura do corpo), com bastante tempo no ápice:** "saltitante e animada, mas o mais longe possível de flutuante". O ápice com meia gravidade (§2.10) contribui para essa sensação.
- **Muito atrito no ar:** soltar o analógico faz a Madeline cair quase reta, o que facilita pousar com precisão.
- **Virada sem derrapagem** abaixo da velocidade máxima. A animação `Flip` cobre a virada (§2.3).
- **Três verbos com "texturas" diferentes:** o dash é "a opção mais caótica, mais difícil de controlar"; a escalada é "mais metódica, com controle preciso" (Maddy) (dev) [7]. A alternância entre os dois cria ritmo.
- **Método:** os números saíram de intuição, de playtest e de um ciclo em que personagem e fases mudam juntos. "Você precisa estar disposto a jogar coisas fora" (Noel) (dev) [7]. O conselho final dos dois: "garanta que o momento a momento seja bom… a sala poderia estar vazia" (dev) [7].

### 2.2 Squash & stretch (valores exatos)

Toda deformação é feita em `Sprite.Scale`, **só no sprite** (o colisor não muda). A volta ao normal é **linear**, `1,75` unidades de escala por segundo em cada eixo (código) [1]:

```csharp
// Player.UpdateSprite() [1]
Sprite.Scale.X = Calc.Approach(Sprite.Scale.X, 1f, 1.75f * Engine.DeltaTime);
Sprite.Scale.Y = Calc.Approach(Sprite.Scale.Y, 1f, 1.75f * Engine.DeltaTime);
// Calc.Approach = move em direção ao alvo no máximo 'maxMove' por chamada [4]
```

A 60 fps, isso dá ≈ 0,0292 de escala por frame. Tempo de recuperação = maior desvio ÷ 1,75.

| Evento (função) | `Sprite.Scale` inicial | Recuperação | Observações |
|---|---|---|---|
| Pulo (`Jump`) | (0,6; 1,4) | 0,229 s ≈ 14 f | + `Dust.Burst` 4 partículas para cima + som |
| Super/hyper jump (`SuperJump`) | (0,6; 1,4) | ≈ 14 f | som extra `jump_super` / `jump_superslide` |
| Wall jump / super wall jump | (0,6; 1,4) | ≈ 14 f | poeira diagonal saindo da parede (4) |
| Climb jump | (0,6; 1,4) via `Jump(false,false)` | ≈ 14 f | poeira e som próprios; rumble Light/Medium se estiver no ar |
| Mola (`Bounce`) | (0,6; 1,4) | ≈ 14 f | rumble Light/Medium |
| Super mola (`SuperBounce`) | (0,5; 1,5) | 0,286 s ≈ 17 f | + `DirectionalShake(−Y, 0,1 s)` + rumble Medium/Medium |
| Mola lateral (`SideBounce`) | (1,5; 0,5) | ≈ 17 f | + `DirectionalShake(±X, 0,1 s)` |
| **Aterrissagem** (`OnCollideV`) | `X = lerp(1; 1,6; s)`, `Y = lerp(1; 0,4; s)`, com `s = min(Vy/240; 1)` | 14–21 f | queda normal (Vy = 160): **(1,4; 0,6)**; fast-fall (Vy = 240): **(1,6; 0,4)** ≈ 0,343 s |
| Fast-fall (segurando ↓ no ar) | esticamento progressivo até (0,5; 1,5) | contínuo | começa quando Vy passa da metade entre 160 e 240 |
| Agachar | (1,4; 0,6) | ≈ 14 f | |
| Levantar / virar agachado | (0,8; 1,2) | 0,114 s ≈ 7 f | |
| Fim do respawn | (1,5; 0,5) | ≈ 17 f | "aterrissagem" ao reaparecer |
| Dash | **nenhum squash** | — | a ênfase vem de freeze, shake, rastro e slash (§2.4, §2.6) |

**Por que funciona (percepção):** o squash proporcional à velocidade de queda faz o jogador *sentir* a altura da queda. A volta é curta (7–21 f) e sempre termina antes do próximo pulo típico. O GMTK destaca "o squash no pulo, que enfatiza o movimento do corpo" e a poeira com rumble "para vender o impacto" (análise) [7]. Swink define **polish** como "qualquer efeito que realça a interação artificialmente, sem mudar a simulação subjacente", e cita squash & stretch e poeira como exemplos [20].

### 2.3 Animação

- **Passos sincronizados com os frames.** O som de passo toca nos frames **0 e 6** das animações `RunSlow`, `RunFast`, `Walk`, `RunWind` e `RunCarry`, no frame 6 de `RunStumble` e no frame 4 de `Flip`. Ao escalar, o som de "handhold" toca no frame 5 (código) [1]. O áudio sai do callback `OnFrameChange` do sprite, então nunca se desalinha da animação.
- **Variantes por velocidade/estado (código) [1]:**
  - `RunSlow` quando |Vx| < 45 px/s (metade da corrida máxima); `RunFast` acima disso.
  - `JumpFast`/`FallFast` quando |Vx| > corrida máxima ou em queda na velocidade terminal. A pose de ar muda em pulos lançados.
  - `Skid` quando o jogador inverte a direção acima da corrida máxima; `Flip` quando inverte abaixo dela. É a "virada" legível.
  - `RunStumble` (tropeço) quando aterrissa depois de cair mais de 50 px, na velocidade terminal e correndo no máximo. É um *follow-through* de queda grande.
  - Poses de contexto: `FrontEdge`/`edgeBack` na beira de plataformas, `LookUp` segurando ↑, `push` contra parede (com poeira nos frames 8 e 15), `Dangling` pendurada sem apoio para os pés, `ClimbLookBack` ao olhar para trás na parede, e idles com variações (espirro, coçar, estalar dedos).
- **Controle vs animação:** nenhuma dessas animações trava o input. A animação sempre segue o estado da física, nunca o contrário (código) [1]. Os devs admitem que o código de animação ("`if frame == x` dentro do player") é feio e que prefeririam algo orientado a dados (dev) [2]. Fica a lição para o nosso SPEC.
- **Cabelo como follow-through:** o cabelo é uma corrente de segmentos que segue o corpo com gravidade. Com 2 dashes ele passa a ter **5 segmentos** e balança em seno (`Dashes > 1`) (código) [1]. O vento também muda a direção dos segmentos.

### 2.4 Partículas, rastros e efeitos de tela

| Evento | Efeito (código [1]) | Quantidade / frequência |
|---|---|---|
| Pulo | `Dust.Burst(BottomCenter, Up, 4)` | 4 |
| Wall jump, climb jump, super wall jump | poeira diagonal saindo da parede | 4 |
| Aterrissagem com Vy ≥ 80 px/s | `Dust.Burst(Position, Up, 8)` | 8 |
| Wall slide | 1 partícula a cada 0,01 s enquanto o slide está nos primeiros ~35% | ~1 por frame |
| Derrapagem durante dash no chão | 1 partícula a cada 0,02 s | ~1 a cada 1,2 f |
| **Dash** | `P_DashA`/`P_DashB` a cada 0,02 s; `SlashFx.Burst` (contrail branco); `Displacement.AddBurst(Center, .4f, 8, 64, .5f)` (onda de distorção; semântica dos argumentos **não confirmada**, porque `Displacement.cs` não é público) | contínuo durante o dash |
| **Rastro/afterimage do dash** | `CreateTrail()` no início, **0,08 s** depois e no **fim** do dash (0,15 s) | 3 sombras por dash |
| Lançado (super jump, wall bounce etc.) | `SpeedRing` a cada 0,15 s enquanto a velocidade ≥ 140 px/s, por até 0,5 s | ~3 anéis |

- **A cor do rastro mostra o recurso *depois* do dash:** `TrailManager.Add(this, wasDashB ? NormalHairColor : UsedHairColor)`. Um dash gasto com 2 cargas deixa sombras vermelhas (ainda sobra 1). Um dash gasto com 1 carga deixa sombras azuis (acabou) (código) [1]. A duração do fade das sombras está em `TrailManager.cs`, não publicado: **não confirmado**.
- O GMTK resume: "o rastro de sombras e o contrail branco brilhante enfatizam velocidade e direção" (análise) [7].

### 2.5 Cabelo e sinais de estado (feedback de recurso)

Constantes (código) [1]:

| Estado | Cor | Comportamento |
|---|---|---|
| 1 dash disponível | `#AC3232` (vermelho) | instantâneo |
| 0 dashes | `#44B7FF` (azul) | `Color.Lerp` para azul a `6 × dt` (≈ 10% por frame), transição suave |
| 2 dashes | `#FF6DEF` (rosa) | + 5 segmentos de cabelo, balanço senoidal |
| Recarga (qualquer ganho de dash) | branco (`FlashHairColor`) | **0,12 s ≈ 7 f** de flash |
| Stamina baixa (< 20 de 110) | sprite inteiro alterna vermelho/branco a cada **0,05 s** (3 f) | + sprite de suor em "danger" + rumble Light/Short **todo frame** |

- **Recarregar o dash não toca som no `Player.cs`.** `RefillDash()` só muda o contador; o feedback é o flash branco do cabelo (código) [1]. O **Celeste Classic** (PICO-8) tocava um som ao recarregar no chão (`psfx(54)`) e tinha som de **dash falho** sem carga (`psfx(9)`) (código) [3]. O feedback de "tentei e não tinha recurso" é barato e muito útil.
- **Stamina ao escalar:** o rumble da escalada funciona como metrônomo do esforço. Subindo: pulso `Climb/Short` a cada **0,2 s**. Parado na parede, no ar: a cada **0,8 s** (código) [1]. O jogador *sente* o gasto sem ver número nenhum. A stamina (110 pontos) "não é mostrada ao jogador" (análise) [7].
- **Classic (30 fps):** a cor do cabelo por carga era vermelho (1), azul (0) e alternância branco/verde a cada 3 frames (2) (código) [3]. O princípio já estava lá no protótipo.

### 2.6 Freeze frames (hitstop global)

| Evento | Duração (código [1] / changelog [8]) | Frames a 60 fps |
|---|---|---|
| **Início do dash** (só se `Engine.TimeRate > 0,25`) | 0,05 s | **3** (o GMTK contou "uma pausa de quatro frames" [7]; a celeste.ink fala em 3 frames de freeze num dash de 15 f, via trecho de busca) |
| Início do dash no booster vermelho | 0,05 s | 3 |
| Saída de dream block | 0,05 s | 3 |
| Lançamento por explosão (`ExplodeLaunch`: bumpers etc.) | 0,1 s | 6; o changelog chama de "período de graça de 0,1 s" [8] |
| Coletar refill gem ("hitstun") | **0,10 → 0,05 s** (v1.2.0.0) [8] | 6 → 3 |
| Quebrar spinner na queda (cena especial) | 0,01 s | ~1 |

**Leituras importantes:**
- **Freeze como janela de graça:** durante o freeze o input é lido e os botões com buffer guardam o aperto [4]. Quem aperta pulo durante o freeze de uma explosão ainda ganha o boost (dev) [8].
- **A direção do dash é lida depois do freeze (inferência pela ordem do código).** `DashBegin()` zera a velocidade e chama `Freeze(.05f)`. A corrotina `DashCoroutine` faz `yield return null` e só então lê `lastAim`. `lastAim` é atualizado no `Update` do player antes dos componentes rodarem [1], e durante o freeze a cena não roda [4]. Na prática, o jogador tem ~3 f para corrigir a diagonal depois de apertar dash. É forgiveness disfarçada de "impacto".
- **A equipe reduziu um freeze para melhorar o fluxo.** O hitstun da refill gem caiu de 0,10 para 0,05 s [8]. Freeze demais quebra o ritmo.
- **Com o jogo em câmera lenta, o freeze some** (`TimeRate > 0,25`), para não empilhar lentidão [1].
- **Celeste Classic (30 fps):** `freeze = 2` frames (≈ 0,067 s) no dash. **O relógio do jogo conta os frames antes do `if (freeze > 0) return`**, então o freeze entra no tempo (código) [3]. No Celeste principal, o tratamento do IGT durante o freeze é **não confirmado** (o código de `Level`/`SaveData` não foi publicado).

### 2.6b Linha do tempo do dash, frame a frame (reconstruída do código)

Reconstruída pela ordem de chamadas em `Player.cs` [1] e `Engine.cs` [4] (inferência onde indicado). Mostra como as camadas de feedback se distribuem no tempo.

| Tempo | O que acontece | Canal |
|---|---|---|
| f0 (aperto) | `StartDash()`: gasta a carga e consome o buffer do dash. `DashBegin()`: **freeze de 0,05 s**, zera a velocidade, **rumble Strong/Medium**, **onda de distorção** no centro, cooldown de 0,2 s | tato + tela |
| f1–f3 | **Congelado**: a cena não atualiza, mas o input é lido [4]. O jogador ainda pode ajustar a direção (inferência) | — |
| ~f4 | `DashCoroutine` retoma: **lê a direção (`lastAim`)**, define a velocidade (preservando uma velocidade anterior maior na mesma direção), **shake direcional de 0,2 s**, **som do dash** (lado + cor da carga), **slash branco**, **1ª sombra** | som + tela + visual |
| ~f4–f13 | partículas do dash a cada 0,02 s; **2ª sombra** 0,08 s depois da 1ª | visual |
| ~f13 (0,15 s de dash) | **3ª sombra**; a velocidade cai para a de fim de dash e o controle volta (`StNormal`) | visual + controle |
| depois | cabelo já azul (ou vermelho, se ainda sobrar carga); o shake termina ~0,2 s depois do início do movimento | estado |

- **Leitura perceptual:** o som e o shake chegam **junto com o movimento**, não com o aperto. O aperto é marcado pelo rumble e pela distorção. Assim o "golpe" do som coincide com o deslocamento visível, e o rumble confirma o input na hora (inferência pela ordem das chamadas).
- Somando 1 f do aperto, 3 f de freeze e ~9 f de dash, dá ≈ 13 f. É próximo do "dash de 15 frames incluindo 3 de freeze" citado pela celeste.ink (trecho de busca; a diferença pode vir de como se contam o início e o fim). Também bate com a "pausa de quatro frames" que o GMTK mediu [7] (o frame do aperto mais os 3 congelados).

### 2.7 Screen shake e câmera

**Shake (código) [1]:**
- Dash: `level.DirectionalShake(DashDir, 0,2 s)`. O tremor vai **na direção do dash** e dura 0,2 s (12 f).
- Super mola: `DirectionalShake(−Y, 0,1 s)`. Mola lateral: `DirectionalShake(±X, 0,1 s)`. Pouso depois da queda do Mirror Temple: `DirectionalShake(+Y, 0,5 s)`.
- Morte: `level.Shake()` genérico (duração padrão **não confirmada**).
- Amplitude: **não confirmada** no jogo principal (`Level.cs` não é público). O GMTK chama de "screen shake microscópico" [7]. No **Classic**, o dash liga `shake = 6` frames (0,2 s a 30 fps) com deslocamento aleatório de **−2 a +2 px** por frame, e a morte liga `shake = 10` (≈ 0,33 s). Tudo desligável por `DisableScreenShake` (código) [3].
- Opções: "opção para desativar screen shake" (v1.2.0.0) e depois um nível "50%, que agora é o padrão" (v1.4.0.0) (dev) [8].

**Câmera (código) [1]:**
```csharp
// Player.Update — "lerp por distância usando delta-time" [1]
var multiplier = StateMachine.State == StTempleFall ? 8 : 1f;
level.Camera.Position = from + (target - from)
    * (1f - (float)Math.Pow(0.01f / multiplier, Engine.DeltaTime));
```
- **Suavização exponencial independente de framerate:** a cada segundo sobra 1% da distância. A 60 fps isso é **≈ 7,4% por frame**, com meia-vida **≈ 0,15 s (9 f)**. Na queda do templo (multiplier 8) sobe para ≈ 10,5% por frame.
- **Alvo (`CameraTarget`):** jogador centralizado (`X − 160`, `Y − 90`) + `level.CameraOffset` (ajustável por sala/trigger) + ajustes por estado:
  - pena (voo): **look-ahead de 0,2 × velocidade**;
  - booster vermelho: **+48 px** no sentido do movimento;
  - lançamento do cume: −64 px em Y;
  - âncoras (`CameraAnchor`) com lerp parcial para compor cenas.
- **Câmera por sala:** o alvo é limitado aos `Bounds` da sala. Como as salas são pequenas e a câmera é afastada, "às vezes a sala inteira fica enquadrada por um ponto de vista estático", o que ajuda a precisão porque só a Madeline se mexe (análise) [7].
- **Sem dead zone nem platform snapping no `Player.cs`:** a câmera segue X e Y continuamente e compensa isso com salas curtas e clamp. **Não existe "look up" de câmera** (a animação `LookUp` é só visual) [1].
- **Não revela o poço:** "snap acima de killboxes" limita Y para a câmera não descer abaixo da linha de morte (`box.Top − 180`) [1]. Quem cai sai do quadro.
- **Modos de trava:** em perseguições a câmera só avança (`at.X = max(at.X, camera.X)`). No chefe final, idem no eixo Y [1].
- **Transição entre salas:** a duração exata do pan é **não confirmada** (`Level.cs` não é público). Guias de speedrun registram que **entrar numa tela nova recarrega o dash** e que a posição dos objetos depende de ter entrado na tela ou renascido nela (análise) [11].

### 2.8 Áudio

- **Foley por superfície:** "quase toda superfície andável/agarrável do jogo tem seu próprio conjunto de efeitos sonoros" (Kevin Regamey, Power Up Audio) (dev) [12]. No código, cada `Play(...)` recebe o índice da superfície (`SurfaceIndex.Param`) da plataforma sob o pé ou na mão (código) [1].
- **Pulo, aterrissagem e wall jump:** eventos separados (`char_mad_jump`, `char_mad_land`). Wall jump tem versões esquerda/direita e toca também o som da superfície empurrada (código) [1].
- **Dash:** 4 variantes: `dash_red_left`, `dash_red_right`, `dash_pink_left`, `dash_pink_right`. Ou seja, lado + cor da carga usada. O som também informa o recurso (código) [1].
- **Passo vs aterrissagem:** logo depois de subir uma borda (`playFootstepOnLand = 0,5 s`), o pouso toca um passo em vez de "land". Isso evita um pouso "pesado" num movimento leve (código) [1].
- **Randomização/pitch:** está no projeto FMOD, fora do `Player.cs`. **Não confirmado** nas fontes lidas.
- **Música reativa ao movimento:** **não confirmado** para o Celeste.

### 2.9 Rumble (vibração)

| Evento | Força | Duração | Fonte |
|---|---|---|---|
| Aterrissagem | Light | Short | [1] |
| **Dash** | **Strong** | Medium | [1] |
| Agarrar parede | Medium | Short | [1] |
| Escalando (subindo) | Climb | Short, a cada 0,2 s | [1] |
| Parado na parede, no ar | Climb | Short, a cada 0,8 s | [1] |
| Climb jump no ar | Light | Medium | [1] |
| Cansado (stamina < 20) | Light | Short, todo frame | [1] |
| Mola / super mola / mola lateral | Light / Medium / Medium | Medium | [1] |
| Explosão (bumper) | Strong | Medium | [1] |
| Morte | Light | Medium | [1] |

- Os valores numéricos de `RumbleStrength`/`RumbleLength` estão em `Input.cs`: **não confirmado**.
- Opção de força do rumble "100%, 50% ou OFF" (v1.2.0.0) (dev) [8].
- Detalhe de percepção: **o pulo não vibra**. O rumble fica guardado para contato e impacto.

### 2.9b Matriz de feedback por evento (resumo do Celeste, para usar como gabarito no SPEC)

| Evento | Squash | Partículas | Freeze | Shake | Rumble | Som | Outros |
|---|---|---|---|---|---|---|---|
| Passo | — | — | — | — | — | passo por superfície (frames 0/6) | — |
| Pulo | (0,6; 1,4) | 4 | — | — | — | `jump` | — |
| Aterrissagem | até (1,6; 0,4) pela Vy | 8 (se Vy ≥ 80) | — | — | Light/Short | `land` por superfície | `RunStumble` em queda grande |
| Wall slide | — | 1/0,01 s (início do slide) | — | — | — | loop de wall slide | — |
| Wall jump | (0,6; 1,4) | 4 diagonal | — | — | — | L/R + superfície | — |
| Agarrar parede | — | — | — | — | Medium/Short | `grab` por superfície | suor |
| Escalar | — | — | — | — | pulso a cada 0,2 s | `handhold` (frame 5) | stamina: piscar vermelho |
| **Dash** | — | contínuas + slash | **0,05 s** | **direcional 0,2 s** | **Strong/Medium** | 4 variantes (lado × carga) | 3 sombras, distorção, cabelo muda |
| Mola / super mola | (0,6; 1,4) / (0,5; 1,5) | — | — | — / −Y 0,1 s | Light / Medium | (entidade) | — |
| Explosão / bumper | — | — | **0,1 s** (graça) | (entidade) | Strong/Medium | (entidade) | — |
| Morte | — | (corpo) | — | `Shake()` | Light/Medium | (corpo) | contador + corpo animado |
| Respawn | (1,5; 0,5) no fim | efeito de morte "voando" até o spawn | — | — | — | `revive` | tween de 0,6 s |

Fonte de todas as células: `Player.cs` [1]. "(entidade)" / "(corpo)" = efeito implementado em outra classe não pública.

### 2.10 Input e responsividade percebida ("Celeste & Forgiveness")

Maddy Thorson lista 10 técnicas, todas "centradas em alargar janelas de tempo ou posição, para que tudo seja um pouco ajustado a favor do jogador. Acho que isso é um grande motivo pelo qual Celeste parece gentil apesar de muito difícil — ele quer que você vença" (dev) [5]:

| # | Técnica | O que o jogador *sente* | Valor no código [1] |
|---|---|---|---|
| 1 | Coyote time | "pulei na beirada e funcionou" | `JumpGraceTime = 0,1 s` (6 f) |
| 2 | Jump buffer | "apertei um frame antes e pulou no frame exato do pouso" | buffer do Jump/Dash existe; duração **não confirmada** |
| 3 | Gravidade pela metade no ápice (segurando pulo) | mais tempo para mirar o pouso; "parece e é agradável" [5] | `mult = 0,5` se \|Vy\| < 40 e pulo segurado |
| 4 | Correção de quina no pulo | a cabeça não "bate" na quina | até 4 px |
| 5 | Correção de quina no dash | o dash "sobe" a borda | até 4 px |
| 6 | Pop em semi-sólidos | dash lateral sobe em plataforma atravessável | até 6 px |
| 7 | Momento herdado de plataformas | "a plataforma me jogou" | `LiftBoost` |
| 8 | Wall jump a 2 px da parede | wall jump "generoso" | artigo: 2 px [5]; código: `WallJumpCheckDist = 3` (checagem deslocada 3 px, o que aceita ~2 px de folga) |
| 9 | Super wall jump a ~5 px | "mais que meio tile!" [5] | Maddy diz "acho que são 5 px"; no `Player.cs` publicado a checagem usa a mesma constante de 3 px. Divergência não resolvida |
| 10 | Reembolso de stamina | climb jump vira wall jump se virar logo em seguida | `ClimbJumpBoostTime = 0,2 s` |

- **Por que o jogador sente isso como "justiça":** Noel Berry explica que "é ruim se você aperta pulo um frame antes de tocar o chão e nada acontece… parece que o jogo errou, que perdeu seu input — que ele foi *comido*". Maddy completa: "É trabalhar na intenção do jogador em vez de uma simulação precisa de apertar botões no momento certo" (dev) [7]. Nijman (Vlambeer) diz que o coyote time "faz parecer que o jogo conhece suas intenções" (dev, via [21]).
- **Teto de habilidade preservado:** "para jogadores casuais só faz o jogo parecer melhor; jogadores pro descem aos frames e abusam" (Noel) (dev) [7]. O changelog confirma o cuidado com esse público: "agora você pode fazer hyper jump durante o coyote time" (v1.2.2.4) [8].
- **Controle retomado cedo:** o dash tira o controle só nos primeiros 0,15 s. Depois o jogador pode frear para não voar nos espinhos (análise) [7].
- **Buffer também em comandos de sistema:** 0,1 s de buffer no Pause (v1.4.0.0) e no Quick Restart (v1.3.3.7) (dev) [8].

### 2.11 Morte, respawn e fluxo

- **`Die()` (código) [1]:** incrementa as mortes da sessão, zera a velocidade, trava a máquina de estados, `level.Shake()`, rumble Light/Medium e cria um `PlayerDeadBody`. A animação do corpo (quique e explosão em bolhas) está em `PlayerDeadBody.cs`, não público. **Duração total não confirmada.**
- **Respawn (`IntroRespawnBegin`) (código) [1]:** som `char_mad_revive`. O efeito de morte é desenhado a partir da posição da morte, limitada 40 px para dentro da sala, e viaja até o ponto de spawn num **tween de 0,6 s (36 f)**. No fim, o estado volta a Normal com squash (1,5; 0,5). A continuidade visual da morte até o renascimento mostra *onde* o jogador vai voltar.
- **Pular a animação:** apertar confirmar durante a morte "acelera, economizando até um segundo inteiro a cada morte" (análise, guia de speedrun) [11].
- **Checkpoint por tela:** cada tela é um checkpoint e "Madeline renasce no início da tela quase instantaneamente" (análise) [13].
- **Timer honesto:** "o timer não fica mais oculto durante a morte" (v1.2.0.0). "O timer de capítulo não pausa mais durante mortes, cutscenes e menus" (v1.2.1.0, *breaking change*) (dev) [8]. "A velocidade do Assist Mode não afeta mais cutscenes nem mortes" (v1.2.0.0) [8]: a morte tem duração fixa.
- **Quick Restart** do capítulo (v1.2.0.0), depois com botão dedicado configurável (v1.4.0.0) (dev) [8].
- **Celeste Classic:** 8 partículas radiais, shake de 10 f e **recarga da sala em 15 frames a 30 fps = 0,5 s** (código) [3].

### 2.12 Assist Mode e acessibilidade

- **Assist Mode:** velocidade do jogo de 50% até 100% em passos de 10%, stamina infinita, air dashes (2 ou infinitos), **Dash Assist** (congela e mostra uma seta de direção), invencibilidade, pular capítulos (análise) [10]. Mensagem do jogo: "Assist Mode permite modificar as regras do jogo para suas necessidades específicas…" [10].
- **Variants:** velocidade de **50% a 160%**, em múltiplos de 10%, 360° dashing, entre outros (análise) [9].
- **Opções de feedback:** screenshake Off/50%/On (50% é o padrão desde a v1.4.0.0); rumble 100%/50%/Off; **Photosensitive Mode** (tira flashes de menu, entre outras coisas); **Grab Mode** alternável (toggle); modo de Crouch Dash (dev) [8].
- **Percepção:** o jogo avisa que a dificuldade faz parte da experiência, mas "reconhece que cada jogador é diferente" (análise) [10].

### 2.12b Evolução: Celeste Classic (PICO-8) → Celeste

Comparar o protótipo de 2015 com o jogo final mostra o que a equipe manteve e o que refinou.

| Aspecto | Celeste Classic (30 fps) [3] | Celeste (60 fps) [1][8] | Leitura |
|---|---|---|---|
| Freeze no dash | 2 f ≈ 0,067 s | 0,05 s = 3 f | ficou mais curto em tempo real |
| Shake no dash | 6 f (0,2 s), aleatório ±2 px | direcional, 0,2 s; opção 50% padrão | de aleatório para direcional |
| Cor por carga | vermelho / azul / branco-verde piscando | vermelho / azul / rosa + flash branco 0,12 s | mesmo princípio, mais polido |
| Recarga no chão | **som** (`psfx(54)`) | **flash visual** (sem som no `Player.cs`) | trocou canal |
| Dash sem carga | **som de falha** (`psfx(9)`) | nenhum feedback no `Player.cs` | — |
| Coyote / buffer | `grace = 6 f` (0,2 s) / `jbuffer = 4 f` (0,133 s) | coyote 0,1 s; buffer **não confirmado** | janelas continuam existindo |
| Morte → sala recarregada | shake de 10 f + 8 partículas + 15 f (0,5 s) | shake + corpo animado + tween de 0,6 s; pulável | o final ganhou narrativa visual e opção de pular |
| Relógio | conta durante o freeze | não confirmado; não pausa na morte (v1.2.1.0) | — |

### 2.13 O que levar do Celeste para o Cyborg

- A tabela de squash (§2.2) e a recuperação linear de 1,75/s servem de ponto de partida. Aplicar **só no transform do sprite filho**.
- Dash e impulso do gancho com camadas curtas: freeze de ~3 f, shake direcional de 0,2 s, rastro colorido pelo recurso restante, partículas e som com variante de lado.
- **Recurso legível no corpo:** o equivalente cyberpunk do cabelo (LED, visor, cabo ou capa) mostra a carga de dash/gancho. Flash branco de ~0,12 s ao recarregar e **som de falha** ao tentar sem carga (Classic).
- Aviso de recurso acabando (superaquecimento/stamina) com piscar vermelho a cada 3 f, partícula (vapor ou faíscas) e rumble leve rítmico.
- Câmera com suavização exponencial `1 − r^dt` (ver §8) e trava por sala/segmento.
- Morte com duração fixa, pulável, timer sempre visível e quick restart com buffer.

---

## 3. Hollow Knight (Team Cherry, 2017)

> O código não é público. Os valores abaixo vêm de wikis e patch notes. Onde o dado vem de reconstrução não oficial, está marcado.

**Feedback de impacto e hitstop**
- **Pausa ao levar dano:** jogadores descrevem "um congelamento… TODA VEZ que sou atingido", e a comunidade confirma que "está no jogo desde o começo" (análise) [30]. O patch 1.5.72 (25/06/2021) corrigiu "camera shake não funcionando durante hit pauses" (dev) [29], o que confirma que o jogo usa hit pauses.
- Parâmetros do `FreezeMoment` (rampa de timeScale → espera → rampa de volta): uma reconstrução em Unity publicada no CSDN mostraria, por exemplo, tipo 0 = (0,01 s; 0,35 s; 0,1 s; alvo 0). **Não confirmado:** a página falhou (HTTP 521) e só vi o trecho do buscador [34].
- **Recuo do golpe:** acertar um inimigo com o ferrão empurra o Cavaleiro para trás. O amuleto *Steady Body* "impede seu portador de recuar ao golpear um inimigo" (texto do jogo) [31]. O recuo é feedback de peso e também regra de espaçamento.
- **Depois de levar dano:** 1,3 s de invulnerabilidade e 0,2 s de "recoil" sem input. Com *Stalwart Shell*: 1,75 s e 0,08 s (análise, wiki) [31]. Tirar o controle por pouco tempo e de forma consistente é o "custo" legível do dano.
- **Pogo (nail-bounce):** golpe para baixo em inimigo ou espinho quica o Cavaleiro e **recarrega o dash** (análise) [31]. O acerto vira mobilidade, e isso reforça o feedback positivo.
- **Dash:** cooldown de 0,6 s (0,4 s com Dashmaster) [31].

**Screen shake e rumble (lição de acessibilidade)**
- O shake é usado "em praticamente toda interação" e foi criticado ("atroz", "tomei dois hits porque a tela tremia demais") (análise) [30].
- Resposta da Team Cherry: rumble habilitado na 1.5.68 (07/06/2021); **`ControllerRumbleMultiplier` e `CameraShakeMultiplier` no Config.ini** na 1.5.72 (25/06/2021); "alguns shakes ignoravam o valor da config" corrigido num patch seguinte de 2021 (dev) [29]. Na 1.5.12459 (fev/2026, junto da Switch 2 Edition), a versão de Switch ganhou opções de menu **Camera Shake On/Reduced/Off** (mudança não documentada, registrada pela wiki) [29]. A migração para o Unity Input System veio num beta de dez/2025 [29].

**Câmera**
- **Zonas de câmera que escondem segredos:** "a câmera não 'entra' naquela parede até o jogador atravessar, porque é uma área secreta". A implementação sugerida em Cinemachine é uma segunda vcam sem confiner mais um blend (análise) [32]. Look-ahead e valores de damping: **não confirmados**.

**Morte e respawn**
- **Hazard respawn:** ao tocar espinhos ou ácido, o Cavaleiro perde vida e volta a um ponto seguro próximo, sem voltar ao banco (análise) [33]. É o "respawn local", que mantém o fluxo em trechos de plataforma.
- Morte total: volta ao banco e deixa uma sombra para recuperar (fora do nosso escopo).

**Lições:** hit pause e recuo dão peso ao combate, mas o shake precisa de **orçamento e de opção**. O hazard respawn local é um bom modelo para "morte de plataforma" dentro de uma fase roguelike.

---

## 4. Hollow Knight: Silksong (Team Cherry, 04/09/2025)

- **Mais rápida e acrobática:** "Hornet se move muito mais rápido, pula mais alto, consegue se agarrar e subir em bordas, é no geral mais acrobática" (Ari Gibson, Edge #354) (dev) [35]. Os cenários e inimigos foram redesenhados em função disso [35]. O jogo usa **Unity** e saiu em 4/9/2025 [39].
- **Dash que vira sprint:** "um dash que a impulsiona com um surto repentino de velocidade que pode ser sustentado num sprint" (análise) [36]. No ar, o dash "faz um arco para baixo" e, perto da borda, a Hornet cai "por mais que eu esmague o pulo" (análise) [36]. Esse é um exemplo de expectativa quebrada.
- **O sprint persiste sem segurar a direção.** Jogadores reclamam que soltar a direção não cancela o sprint, "ao contrário de mil outros jogos". Outros defendem que isso permite técnicas avançadas (análise) [38]. É uma tensão clássica entre convenção e expressividade: **documentar e ensinar**.
- **Pogo diagonal por padrão (Hunter Crest):** golpes diagonais para baixo, mais lentos, com efeito de "plataforma de lançamento": o jogador "reflete no inimigo e é lançado ao ar". Outros crests dão o pogo reto clássico (análise) [37]. Há reclamações de levar dano de inimigos em movimento durante o pogo [37].
- **Clawline (gancho/arpão):** a Hornet "arremessa a agulha e se puxa até ela com um fio de seda". Um reviewer demorou "a aceitar que a agulha não precisa atingir uma superfície ou inimigo para a manobra funcionar" (análise) [36]. O wiki descreve como arpão com poucos frames de invencibilidade [40]. **Lição para o gancho do Cyborg:** a regra de "onde o gancho pega" precisa ficar óbvia visualmente.
- **Outros verbos:** *Cling Grip* (escalar paredes), *Silk Soar* (salto vertical), *Drifter's Cloak* (planar e subir em correntes de ar) [39]. Os bancos continuam sendo o respawn após perder toda a vida [39].
- **Recepção:** críticos destacam "a sensação de fluidez e exatidão nos controles da Hornet" [39].
- **Números de hitstop, câmera e partículas:** **não confirmados** (não há código nem análises técnicas públicas).

---

## 5. Sonic — clássico (Mega Drive) e moderno

### 5.1 Câmera clássica (Sonic Physics Guide) [25]

- Tela de **320×224**.
- **Janela horizontal:** bordas em x = **144** e **160**. É uma janela de só 16 px (5% da largura), então a câmera segue quase travada no eixo X. O guia sugere 152/168 para centralizar melhor [25]. No Sonic CD a borda é sempre 160 (sem janela) até a câmera estendida entrar em ação.
- **Foco vertical:** 96 (padrão), 104 (olhando para cima), 88 (para baixo). No ar, o jogador anda livre entre foco −32 e foco +32 (janela de 64 px). No chão, a câmera fica travada no Y do jogador.
- **Limite de velocidade da câmera:** **16 px/frame** (24 no S3&K). Acima disso a câmera "fica para trás", e isso *vende* velocidade. No chão, com velocidade < 8, o limite vertical cai para 6 px/frame (2 se estiver olhando para cima ou para baixo).
- **Olhar para cima/baixo:** no Sonic 1 rola na hora; do Sonic 2 em diante, só depois de **120 frames** (2 s) segurando.
- **Spindash lag:** guarda as últimas **32** posições do jogador. No lançamento, a câmera "atrasa" por 32 − rev frames (rev de 0 a 8), somando posições em pares. O personagem dispara e a câmera o alcança, o que dá drama ao lançamento. A mesma técnica aparece no Drop Dash do Mania "para tornar o movimento mais dramático" [27]. Há um bug conhecido: a câmera pode andar para trás; a correção sugerida é preencher a tabela com a posição atual [25].
- **Câmera estendida (Sonic CD):** com velocidade ≥ 6 px/frame, o foco horizontal desloca **64 px** para trás do personagem (ou seja, a câmera mostra mais à frente), a **2 px/frame**. Volta quando a velocidade cai [25]. É o look-ahead ligado à velocidade.
- Itay Keren resume: o Sonic usa uma janela estreita para manter o personagem centrado em alta velocidade e uma janela alta para limitar o movimento vertical (análise) [14].

### 5.2 Animação e som comunicando velocidade [26][27]

- **Duração de cada frame proporcional à velocidade:** corrida/caminhada = `floor(max(0, 8 − |gsp|))`; bola/rolar = `floor(max(0, 4 − |gsp|))`. A duração só é atualizada quando o frame troca (código de referência do guia) [26].
- **Troca de ciclo por limiar:** caminhada abaixo de 6; corrida com "pés girando" a partir de 6; no Sonic CD, corrida em "8" a partir de 10 [26]. Animação de freada só se o jogador virar com |gsp| ≥ 4 [26].
- **Spindash:** cada apertar soma 2 "revs" (máximo 8); a velocidade de saída é `8 + floor(rev)/2` [27]. Cada aperto se sente como carregar.
- **Música:** com os *Speed Shoes* do S3&K, "o tempo da música é multiplicado por 1,25" [27]. Em outros jogos a música troca para uma versão mais rápida ou um jingle (análise) [28].
- O GMTK observa que o Sonic tem "uma curva de aceleração ridiculamente longa" porque o jogo é sobre construir e manter momento [7]. Isso contrasta com a precisão do Celeste.

### 5.3 Sonic moderno (Boost)

- No Boost (Unleashed em diante), o personagem é envolvido por um campo de energia, **as bordas da tela ficam borradas** e **a música soa levemente diferente**. Nos jogos mais recentes o blur pode ser desativado nas opções (análise) [28].
- Parallax, zoom dinâmico e valores de blur nas versões 2D modernas: **não confirmados**.

**Lições para o Cyborg:** (1) look-ahead proporcional à velocidade, com rampa limitada (2 px/frame); (2) atraso de câmera controlado e curto no lançamento de gancho/dash, sem permitir câmera "para trás"; (3) animação de corrida com duração por frame em função da velocidade; (4) estado de "alta velocidade" com assinatura audiovisual (rastro, blur leve nas bordas, camada musical).

---

## 6. SpeedRunners (DoubleDutch Games, 2016)

- **Câmera compartilhada que segue o líder:** "a tela se move com quem estiver na liderança". Quem fica para trás e sai da tela é eliminado. Depois de uma eliminação, "a tela começa a ficar menor" [41]. Um timer também encolhe a tela se ninguém for eliminado [41].
- **Zoom dinâmico quase invisível:** um desenvolvedor (selo [developer] no Steam) escreveu: "A câmera dá zoom in e out bastante durante o jogo normal, você mal percebe". E sobre encolher em vez de dar zoom: "É muito óbvio o que está acontecendo quando encolhe, enquanto o zoom levaria um tempo para as pessoas entenderem antes de surtarem" (dev) [42]. Jogadores acrescentaram que o zoom "alteraria a percepção e bagunçaria o timing" [42]. **Lição:** zoom sutil pode existir, mas regras de perigo precisam de sinal explícito (borda que fecha).
- **Gancho:** lança a **45°** na direção em que o personagem olha, **só prende em tetos brancos** e segura enquanto o botão estiver pressionado [43]. "Balançar é mais rápido que correr" e "o timing (lançar, balançar, soltar) é o aspecto mais importante" [43]. A cor branca é a *affordance* do que é "enganchável".
- **Boost:** barra de boost gastável (gatilho), recarregada por itens e "charge gates"; *speed tunnels* dão boost dentro da área; deslizar em ladeiras dá "um enorme boost" [43]. O **rastro aparece quando o jogador está em sprint**, é personalizável e vira assinatura de identidade [41].
- **Filosofia:** "em muitos mapas você pode simplesmente segurar o analógico para a direita e correr por bastante tempo". O jogo é acessível e tem profundidade, e o "reverse grapple" surpreendeu o próprio designer (dev) [44].
- **Números (duração de câmera, raio do gancho, velocidades):** **não confirmados**.

**Lições para o Cyborg:** superfícies engancháveis com linguagem visual inequívoca; gancho com "janela de soltura" que recompensa timing; rastro como sinal de "estou no máximo"; zoom só se for sutil e sem mudar a leitura do perigo.

---

## 7. Super Meat Boy (Team Meat, 2010)

- **Filosofia de frustração (dev):** "Remova vidas, reduza o tempo de respawn, mantenha as fases curtas e o objetivo sempre à vista" [45]. "A frustração é criada por uma penalidade… qual é a penalidade no Meat Boy se não há vidas? É quanto tempo leva para começar a jogar de novo" (McMillen, via trecho do artigo *Raw Meat*; a página ficou bloqueada) [46]. Tirar as vidas "permite basear a dificuldade no design de fase, e não na penalidade de perder vidas" (McMillen, citado pelo GMTK) [23].
- **Replay de todas as tentativas:** ao terminar a fase, "uma exibição épica de todas as suas mortes passadas". Para os devs, é um lembrete de que o jogador "está melhorando por suas próprias ações" (dev) [45].
- **Permanência:** o sangue fica nas paredes e marca onde você morreu. Na prática funciona como mapa de calor de erro e mostra "pontos de pulo seguros" (análise) [48].
- **Respawn:** "quase instantâneo". No **Super Meat Boy 3D**, um wiki de fãs mede "menos de meio segundo" [49]. No SMB original: **não confirmado**.
- **Controle:** o GMTK diz que o SMB acelera cerca de 4× mais devagar que a Madeline, "pesado para sair" [7]. O SMB interrompe o pulo ao soltar o botão, o que viabiliza tetos perigosos no level design (análise) [21]. Keren cita o lerp de câmera como adequado a personagens "lentos ou rápidos como em Super Meat Boy" [14].
- **Contraponto:** a Critical-Gaming argumenta que o respawn "reduzido a nada" (palavras atribuídas aos devs) tira o tempo de reflexão: "precisamos de tempo para refletir" (análise) [47]. **Lição:** respawn rápido, mas com uma batida mínima e legível (ver §9).
- Números de hitstop, câmera e partículas: **não confirmados**.

---

## 8. Transversal — câmera para plataforma de precisão e velocidade

### 8.1 Fundamentos

- **Três desafios de câmera** (Keren): **atenção** (direcionar o foco), **interação** (o jogador manter autoridade sobre o personagem) e **conforto** (evitar conflito visual-vestibular, que causa náusea) [14].
- **Vocabulário (Keren) [14]:**
  - *position-locking*: travado no centro;
  - *camera-window* / dead zone: só move quando o jogador toca a borda; no Rastan Saga a altura da janela é igual à do pulo padrão;
  - *platform-snapping*: SMW e Rayman só ajustam Y **ao aterrissar**;
  - *lerp-smoothing* e *physics-smoothing*;
  - *dual-forward-focus*: SMW e Cave Story;
  - *static forward focus*: Defender, ~25% da largura à frente;
  - *projected focus*: extrapola pela velocidade;
  - *target focus*: Snapshot faz a média entre jogador e mira;
  - *cue focus*: atratores;
  - *region anchors*: câmera por região;
  - *zoom-to-fit*;
  - *speedup pull/push zones*: o **Super Mario Bros.** acelera a câmera a partir de um ponto ~25% fora do centro.
- **Shake (Eiserloh) [15]:** usar um nível de **trauma** em [0,1] (dano soma 0,2 ou 0,5), que decai linearmente, com shake = trauma² ou trauma³ (trauma 0,3/0,6/0,9 → 3%/22%/73% de shake). Em 2D, **translação + rotação**. **Ruído suave (Perlin) é melhor que aleatório**: funciona com pausa e câmera lenta e tem frequência ajustável. O offset é somado sobre a câmera base, "preservando a câmera base".
- **Suavização (Eiserloh) [15]:** média assintótica `x += (alvo − x) × k` (0,01 = lento, 0,1 = rápido, 0,5 = "incrivelmente rápido", a 60 fps). Horizontal, vertical, subida e descida podem ter pesos diferentes. Com timescale, multiplicar o peso. **A forma do Celeste `1 − r^dt` resolve isso matematicamente** [1].
- **Câmera parada para precisão:** "se é sobre pular… mantenha a câmera parada para você acertar cada pulo" [22].

### 8.2 Comparativo

| Jogo | Modelo | Horizontal | Vertical | Especiais |
|---|---|---|---|---|
| Celeste [1][7] | Sala + lerp exponencial | segue (7,4%/f) | segue (7,4%/f) | clamp na sala, killbox snap, look-ahead só em estados de voo, travas de avanço |
| Sonic clássico [25] | Janela + velocidade limitada | janela de 16 px | janela de ±32 px no ar; travada no chão | limite de 16 px/f, spindash lag, câmera estendida no CD |
| Hollow Knight [32] | Contínua + zonas | segue | segue | zonas que não revelam segredos |
| SpeedRunners [41][42] | Compartilhada no líder | segue o líder | segue | zoom sutil, tela que encolhe (regra) |
| Platformer Toolkit [24] | Cinemachine + proxy | segue | opção "ignorar pulos" | proxy com Y do último pouso; volta a seguir se cair > 3 unidades |
| SMB [14] | Lerp | segue | segue | — |

**Implementação de "ignorar pulos" (platform snapping) no Toolkit do GMTK (código, MIT) [24]:**
```csharp
// jumpTester.Update(): proxy que a vcam segue
transform.position = new Vector3(characterTransform.position.x, characterY);
// characterJuice: ao aterrissar -> characterY = y do jogador
// se o jogador cair > 3 unidades abaixo do proxy -> cameraFalling = true (segue Y de novo)
```

### 8.3 Recomendações para o Cyborg (speedrun + precisão + gancho)

Valores rotulados como **sugestão** são pontos de partida, a validar em playtest.

1. **Modelo por segmento/sala dentro de cada fase roguelike.** Cada sala gerada define `Bounds`, com clamp igual ao do Celeste. Salas estreitas ficam quase estáticas (precisão); corredores longos usam a câmera de velocidade. Transições: pan curto sem roubar tempo do timer, ou câmera contínua com confiner. Duração do pan: **sugestão** ≤ 0,3 s, com input bufferizado.
2. **Suavização exponencial por eixo, independente de framerate:** `cam += (alvo − cam) × (1 − r^dt)`. Referência do Celeste: r = 0,01 (meia-vida de 0,15 s) [1]. **Sugestão:** horizontal com meia-vida de 0,08–0,15 s; vertical de 0,15–0,25 s; descida mais rápida que subida, para não perder de vista onde vai pousar (assimetria de Eiserloh [15]).
3. **Dead zone horizontal pequena + look-ahead por velocidade.** Referências: janela de 16 px em 320 no Sonic (5%) [25]; foco de 25% à frente no Defender [14]; deslocamento de 64 px (20% da largura) a 2 px/f no Sonic CD [25]; look-ahead de 0,2 × velocidade na pena do Celeste [1]. **Sugestão:** `lookAhead = clamp(vx × 0,2 s, ±20% da largura)` com rampa máxima de ~2 px/f de jogo. Desligar ou reduzir em salas de precisão.
4. **Vertical com platform snapping + exceções.** Y segue o último chão, como o proxy do Toolkit [24] e o SMW [14]. Volta a seguir quando: (a) cai abaixo do limiar (Toolkit: 3 unidades; **sugestão:** ~1/4 da altura da tela); (b) sobe além da janela, em escalada ou gancho para cima; (c) está pendurado no gancho.
5. **Gancho = target focus.** Enquanto preso, puxar o foco para o ponto médio entre jogador e âncora, com peso por distância (Snapshot faz a média jogador/mira [14]). Ao soltar, voltar ao look-ahead de velocidade.
6. **Nunca revelar a morte de antemão nem esconder o destino:** killbox snap (Celeste) [1] e zonas que não entram em áreas secretas (HK) [32].
7. **Lag dramático controlado:** no impulso do gancho ou dash, 2–4 f de atraso proposital (conceito do spindash lag [25]), **sem** câmera para trás (corrigir como o Flame Shield [25]).
8. **Shake como camada aditiva sobre a câmera final**, em pixels inteiros, direcional, curto e desligável (§10). O Celeste Classic usava ±2 px por 6 f [3].
9. **Pixel perfect:** arredondar a posição final da câmera para a grade de pixels de jogo. Alternativa para movimento suave: renderizar em resolução de jogo e aplicar o resto sub-pixel como offset de tela, como Hyper Light Drifter [14].
10. **Zoom:** evitar, ou só em degraus inteiros. Mudar o zoom muda a razão de pixel e, segundo jogadores do SpeedRunners, a percepção de timing [42].

---

## 9. Transversal — morte, respawn e fluxo em tentativa-e-erro cronometrada

### 9.1 O que as referências fazem

| Jogo | Penalidade | Respawn | Timer | Registro de tentativas |
|---|---|---|---|---|
| Celeste | início da tela [13] | tween de 0,6 s + animação pulável (≤ 1 s economizado) [1][11] | não pausa na morte; visível [8] | contador de mortes sem punição [13] |
| Celeste Classic | início da sala | 0,5 s (15 f a 30 fps) [3] | conta até durante o freeze [3] | contador de mortes [3] |
| Super Meat Boy | início da fase curta [45] | "quase instantâneo"; < 0,5 s no SMB 3D [49] | — | replay de todas as mortes + sangue permanente [45][48] |
| Hollow Knight | hazard → ponto seguro; morte → banco [33] | não confirmado | — | sombra (corpse run) |
| Sonic Forces / Crash 4 | checkpoint | — | — | retries contam contra o rank; gema por < 3 retries [23] |

### 9.2 Princípios

1. **Tempo é a moeda.** McMillen: a penalidade é "quanto tempo leva para voltar a jogar" [46]. Num speedrun por fase, cada morte já custa no timer. **Não somar outra punição** (vidas, perda de progresso roguelike por morte simples), a não ser que seja uma regra explícita da run.
2. **O respawn é previsível e curto.** Referências de 0,5 s (Classic, SMB 3D) a ~1 s+ (Celeste com animação completa). **Sugestão:** ≤ 0,5 s da morte ao controle em modo speedrun; animação completa pulável com qualquer botão (como o Celeste [11]).
3. **Uma batida de leitura.** Contra o excesso de velocidade [47]: 2–4 f de freeze e flash no ponto da morte, mais marcador persistente. O jogador entende *o que* o matou sem esperar.
4. **O timer nunca some e não "mente".** O Celeste deixou de esconder o timer na morte e de pausá-lo em mortes e cutscenes [8]. Decidir e **documentar no SPEC** se freezes e hitstops contam no IGT (§10.2).
5. **Reinício instantâneo da fase/segmento** com botão dedicado e buffer de 0,1 s (Celeste [8]).
6. **Transformar fracasso em informação:** marcadores de morte persistentes na run (sangue do SMB [48]), ghost do melhor tempo da sala ou fase e, ao final, replay das tentativas (SMB [45]).
7. **Estrutura roguelike:** em vez de punir, **recompensar consistência**: bônus ou rank por fase sem mortes ou com poucos retries, como Sonic Forces e Crash 4 [23].
8. **Continuidade espacial no respawn:** o efeito que viaja do local da morte ao spawn (tween de 0,6 s do Celeste [1]) orienta o olhar. A câmera não deve "procurar" o jogador depois do respawn.
9. **Hazard respawn local** (HK [33]) para trechos longos: voltar ao último chão seguro custa tempo, mas não o segmento inteiro.

---

## 10. Catálogo priorizado de técnicas (Unity 6 · 2D pixel art · speedrun · Cyborg com gancho)

Custo: **P** (até ~1 dia), **M** (alguns dias), **G** (semana+). Impacto: baixo/médio/alto. "Parâmetros iniciais" são **sugestões** ancoradas nas referências citadas.

| # | Técnica | O que resolve | Referência | Custo | Impacto | Riscos | Parâmetros iniciais sugeridos |
|---|---|---|---|---|---|---|---|
| 1 | Feedback no 1º frame (squash, poeira e som no mesmo frame do input) | sensação de lag mesmo com física correta | Celeste `Jump()` [1] | P | alto | física em FixedUpdate atrasa o visual (ver §10.2) | disparar efeitos no `Update` em que o input é consumido |
| 2 | Buffer de input (pulo, dash, gancho) e coyote time | "input comido", injustiça | Celeste [5][7], Toolkit [24] | P | alto | buffer longo demais gera ações indesejadas | valores com o doc de física; Celeste 0,1 s de coyote [1] |
| 3 | Squash & stretch no sprite filho | peso, impacto, altura da queda | Celeste §2.2 [1], Toolkit [24] | P | alto | "mixels" em pixel art se renderizar em alta resolução | pulo (0,6; 1,4); pouso `lerp` até (1,6; 0,4) pela Vy; volta linear 1,75/s |
| 4 | Poeira contextual (pulo 4, pouso forte 8, wall slide contínuo, derrapagem) | contato com o mundo | Celeste §2.4 [1], GMTK [22] | P | alto | poluição visual; muitas partículas por frame | contagens do Celeste; pouso só com Vy ≥ 50% da terminal |
| 5 | Recurso legível no corpo (LED, visor ou cabo do Cyborg) | saber se há dash ou gancho sem HUD | cabelo do Celeste §2.5 [1] | P | alto | daltonismo: usar cor + forma/brilho | 3 estados de cor; flash branco de 0,12 s na recarga; lerp de ~6×dt ao esvaziar |
| 6 | Som de "sem recurso" e de recarga | feedback de erro e de ganho | Celeste Classic `psfx(9)`/`psfx(54)` [3] | P | médio | irritar se repetido; limitar | 1 som curto, com cooldown de ~0,1 s |
| 7 | Afterimage/rastro colorido pelo recurso restante | vender velocidade e direção; informar recurso | Celeste `CreateTrail` [1], SpeedRunners [41] | M | alto | custo de sprites; pool obrigatório | 3 imagens por dash (t = 0; 0,08; fim); fade de ~0,15–0,3 s (sugestão; Celeste não confirmado) |
| 8 | Freeze curto no dash e no impulso do gancho, lendo a direção **depois** do freeze | impacto + janela de correção de mira | Celeste §2.6 [1][4] | P | alto | **timer de speedrun** e empilhamento com câmera lenta | 0,05 s (3 f); desligar se timescale < 0,25 (como o Celeste) |
| 9 | Freeze como graça em impactos externos (molas, explosões) | "cheguei atrasado e ainda funcionou" | changelog Celeste [8] | P | médio | idem 8 | 0,05–0,1 s com input bufferizado |
| 10 | Screen shake direcional, curto, aditivo, em px inteiros | impacto sem perder referência | Celeste [1][3], Eiserloh [15] | P | médio | náusea, leitura ruim (HK [30]) | dash: 0,2 s, 1–2 px de jogo na direção; morte: 0,3 s; opção 0/50/100% com padrão 50% [8] |
| 11 | Rumble por evento, com hierarquia | tato; impacto sem poluir a tela | Celeste §2.9 [1] | P | médio | motor preso ligado; só gamepads suportados [52] | tabela do Celeste; pulo sem rumble; opção 0/50/100% [8] |
| 12 | Metrônomo de esforço (rumble e piscar em recurso crítico) | antecipar falha de stamina/calor | Celeste tired/climb [1] | P | médio | fadiga sensorial | piscar vermelho a cada 0,05 s; rumble leve a cada 0,2 s |
| 13 | Passos sincronizados por Animation Event + superfície | peso e material | Celeste frames 0/6 [1], Regamey [12] | P | médio | dessincronizar se a animação variar de velocidade | eventos nos frames de contato; clip por superfície |
| 14 | Variação de pitch/clip (pulo, passo, pouso) | evitar repetição | GMTK [22] | P | médio | pitch demais soa cartunesco | sugestão ±3–6% + 3–4 variações |
| 15 | Animações por faixa de velocidade (RunSlow/RunFast/JumpFast) e FPS da animação proporcional | ler velocidade pela pose | Celeste [1], Sonic [26] | M | médio | explosão de estados no Animator | limiar em 50% da velocidade máxima; `speed = f(abs(vx))` |
| 16 | Câmera exponencial por eixo, independente de fps | fim do "jelly" e do jitter | Celeste [1], Eiserloh [15] | P | alto | jitter com pixel perfect mal feito | meia-vida H 0,08–0,15 s, V 0,15–0,25 s |
| 17 | Look-ahead por velocidade + dead zone H pequena | ver o que vem pela frente em alta velocidade | Sonic CD [25], Keren [14] | M | alto | enjoo em inversões rápidas; suavizar | ≤ 20% da largura; rampa ~2 px/f; dead zone ~5% H |
| 18 | Platform snapping vertical com exceções | câmera parada no pulo (precisão) | Toolkit [24], SMW/Rayman [14] | M | alto | perder de vista uma queda longa | seguir Y ao cair > 1/4 da tela ou subir além da janela |
| 19 | Salas com bounds, killbox snap e zonas | enquadramento de precisão; nada de spoiler | Celeste [1], HK [32] | M | médio | geração procedural precisa emitir bounds | bounds por sala do gerador roguelike |
| 20 | Affordance e telegrafia do gancho (superfície enganchável + indicador de alvo) | "onde o gancho pega?" | SpeedRunners tetos brancos [43], Silksong [36] | M | alto | poluição visual | cor/brilho reservado; retícula no ponto válido mais próximo |
| 21 | Feedback de "grab" e "release" do gancho | tensão, timing de soltura | SpeedRunners [43] | M | alto | freeze de grab conta no timer | grab: som metálico + rumble Medium/Short + corda tensionando 2–3 f; release: rastro + anel de velocidade |
| 22 | Efeitos de alta velocidade (anéis, speed lines, blur leve nas bordas, camada musical) | "estou no máximo" | Celeste SpeedRing [1], Sonic Boost [28], S3&K ×1,25 [27] | M | médio | blur prejudica a leitura em pixel art | anel a cada 0,15 s acima do limiar, por até 0,5 s |
| 23 | Morte rápida, pulável, com marcador persistente | fluxo | SMB [45], Celeste [1][11] | M | alto | pouca reflexão [47] | ≤ 0,5 s até o controle; 2–4 f de freeze e flash; marcador no local |
| 24 | Quick restart de sala/fase com buffer | fluxo em speedrun | Celeste [8] | P | alto | reset acidental: exigir segurar | segurar ~0,3 s (sugestão) ou botão dedicado; buffer de 0,1 s |
| 25 | Ghost do melhor tempo + replay de tentativas | aprendizado e motivação | SMB [45] | G | alto | gravação determinística; memória | gravar inputs ou poses a 60 Hz por sala |
| 26 | Opções de acessibilidade (shake, rumble, flash, velocidade, hitstop) | conforto e inclusão | Celeste [8][10], HK [29] | M | alto | rodar em assist deve marcar a run | sliders 0/50/100; modo fotossensível; assist com selo |
| 27 | Zoom dinâmico sutil | enquadrar picos de velocidade | SpeedRunners [42] | M | baixo | quebra o pixel perfect; muda a percepção | evitar; se usar, só degraus inteiros de PPU |

### 10.1 As 10 de maior impacto e menor custo (ordem de implementação)

1 (1º frame) → 2 (buffer/coyote, com o doc de física) → 3 (squash) → 16 (câmera exponencial) → 5 (recurso no corpo) → 4 (poeira) → 8 (freeze no dash, com decisão de timer) → 24 (quick restart) → 10 (shake com opção) → 11 (rumble com opção).

### 10.2 Notas específicas de Unity 6

**Estado atual do repositório (só leitura):** Unity **6000.1.7f1**, Input System **1.14.2**, URP **17.1.0**, **sem Cinemachine**. Câmera própria (`Assets/scripts/CameraFollow.cs`, `SmoothDamp` com `smoothTime = 0,2` nos dois eixos e clamp por `CameraBoundary`). Timer em `Assets/scripts/Timer.cs` usando `Time.deltaTime`. `Fixed Timestep = 0,02` (50 Hz). Rigidbody2D do `Cyborg.prefab` com interpolação ligada.

**Tempo, freeze e timer de speedrun**
- `Time.timeScale = 0` para `FixedUpdate` e `WaitForSeconds` [53]. Com isso **pausa também o `Timer.cs` atual** (usa `Time.deltaTime`), a física, o Animator (se não estiver em `UnscaledTime`), as partículas (se não estiverem em `useUnscaledTime`) e o Cinemachine (se `Ignore Time Scale` estiver desligado [50]). O `Update` e o Input System continuam rodando, então **dá para bufferizar input durante o freeze**, como o Monocle [4].
- **Decisão obrigatória no SPEC: o freeze conta no IGT?**
  - (a) **Conta** (Celeste Classic [3]): IGT = tempo real sem pausa, menus e loading. Simples de explicar, mas qualquer opção de desligar hitstop vira vantagem competitiva.
  - (b) **Não conta** (IGT = soma de ticks de simulação × `fixedDeltaTime`): permite desligar o freeze por acessibilidade sem afetar ranking, e é determinístico.
  - **Recomendação:** (b), com freezes de duração fixa. `Assist`/velocidade reduzida marca a run. O `Timer.cs` atual já se comporta como (b) com `timeScale = 0`, mas **não** com câmera lenta (`timeScale` entre 0 e 1 faz o timer andar mais devagar). Contar ticks resolve os dois casos.
- **Freeze global vs local:** Pichlmair distingue *hit stop* (local, só alguns objetos pausam) de *freeze frame* (global) [21]. Para um jogo single-player de movimento, o freeze **global** de 2–3 f no dash é o padrão do Celeste. Para o *grab* do gancho, considerar um freeze **local** (pular N ticks só da simulação do jogador, com o mundo seguindo). Nesse caso a regra do timer precisa estar explícita.
- **Buffers e janelas durante o freeze:** usar contadores em **ticks de simulação**, que não andam no freeze, para que um aperto feito logo antes do freeze não expire. No Monocle o buffer usa `Engine.DeltaTime`, que continua andando no freeze [4]; com buffer maior que o freeze isso não dá problema, mas é frágil.
- **Latência percebida:** física a 50 Hz com render a 60+ Hz cria cadência irregular. A interpolação do Rigidbody2D suaviza, mas mostra até 1 tick "atrasado". Recomendação: aplicar **efeitos visuais e sonoros no `Update` do input** (item 1) e avaliar `fixedDeltaTime = 1/60` ou 1/120 junto com o doc de física. O Input System processa eventos em "Dynamic Update" ou "Fixed Update" [55]. Ler o "pressed" no Update e **transportar para o próximo tick por buffer** evita perder aperto entre ticks.

**Câmera: Cinemachine 3 vs custom**
- **Cinemachine 3** tem: `CinemachinePositionComposer` (Dead Zone, Hard Limits, Damping, **Lookahead Time/Smoothing/Ignore Y**, Screen Position) [50]; `CinemachineConfiner2D` (bounds por sala); **Impulse** (`CinemachineImpulseSource` com formas Recoil/Bump/Explosion/Rumble ou curva custom, duração em s, `GenerateImpulse()`; `CinemachineImpulseListener` com Gain, Channel Mask e Reaction Settings) [50]; `Ignore Time Scale` no Brain [50]; extensão **Pixel Perfect** (ajusta o ortho size; as transições entre vcams ficam temporariamente fora do pixel perfect; com Upscale RT há menos resoluções possíveis) [50].
  - **Limite:** o platform snapping exige um **proxy** como no Toolkit do GMTK [24]. O look-ahead do CM é baseado em velocidade genérica, não na lógica do Sonic.
- **Custom** (evoluir `CameraFollow.cs`): controle total de look-ahead, snapping, target focus do gancho, shake aditivo e arredondamento em pixel. **Recomendação:** custom, porque a câmera já existe, as regras são específicas do gênero e ter menos dependências ajuda o determinismo do ghost. Trocar `SmoothDamp` por suavização exponencial por eixo (item 16) e adicionar as camadas: `alvo → dead zone/look-ahead/snapping → clamp da sala → suavização → shake → arredondamento de pixel`.

**Pixel Perfect Camera (URP 2D)**
- Propriedades: Assets PPU, Reference Resolution (ex.: 320×180), Crop Frame, **Grid Snapping** = None / **Pixel Snapping** (alinha sprites à grade) / **Upscale Render Texture** (renderiza em resolução de referência e amplia) [51].
- **Squash & stretch em pixel art (inferência):** o Celeste escala sprites numa tela de 320×180 [5], então a deformação é rasterizada na resolução do jogo e continua "pixelada". No Unity, **Upscale Render Texture** reproduz esse efeito. Com Pixel Snapping em alta resolução, a escala não inteira gera *mixels*. Testar os dois modos antes de decidir.

**Partículas e rastros**
- **ParticleSystem (Shuriken)** é o recomendado para poeira e faíscas em pixel art: sprites de 1–3 px, filtro Point, contagens baixas (4–8 por evento como no Celeste), `Simulation Space = World`, `useUnscaledTime` desligado para congelar junto no freeze (ou ligado, por escolha de design).
- **VFX Graph:** exige compute shaders, **não suporta OpenGL ES** e "não saiu do preview em mobile" [54]. Para um jogo 2D pixel art isso é desnecessário e arriscado (suporte ao 2D Renderer **não confirmado** nas fontes lidas).
- **Afterimage:** pool de `SpriteRenderer` copiando `sprite`, `flipX` e posição arredondada, com tint pela cor do recurso e fade de alpha. É o equivalente ao `TrailManager` do Celeste [1]. `TrailRenderer` fica para rastro contínuo de sprint (estilo SpeedRunners [41]). O cabo do gancho vai em `LineRenderer` ou sprites em segmentos.

**Rumble (Input System)**
- `Gamepad.current?.SetMotorSpeeds(low, high)` (0–1; motor de baixa frequência à esquerda, alta à direita); `PauseHaptics`/`ResumeHaptics`/`ResetHaptics` por device ou globais em `InputSystem.*` [52]. **Parar sempre** com corrotina em tempo não escalado e chamar `ResetHaptics` em pausa, morte, troca de cena e perda de foco.
- Suporte: PS4/Xbox/Switch nos consoles; DS4 em Mac/Windows/UWP; Xbox em Windows; **WebGL sem rumble**. No DS4, chamadas seguidas podem falhar [52].
- Mapear a hierarquia do Celeste (§2.9) em presets (ex.: Light/Medium/Strong × Short/Medium). Os valores exatos do Celeste não são públicos, então **calibrar em playtest**.

**Áudio**
- Passos via **Animation Events** nos frames de contato (Celeste 0/6 [1]). Parâmetro de superfície por tile ou material. Variações e pitch leve (item 14). Mixer com snapshot de "freeze" opcional (ex.: abafar 50 ms) para acompanhar o impacto sem pausar o áudio, já que `AudioSource` não depende de `timeScale`.

---

## 11. Anti-padrões (o que faz o movimento parecer ruim)

1. **"Input comido":** apertar um frame antes e nada acontecer. "Parece que o jogo errou" [7]. Causas típicas no Unity: ler `wasPressedThisFrame` dentro do `FixedUpdate`, falta de buffer, buffer que expira durante o freeze.
2. **Feedback atrasado em relação ao input:** efeito disparado só quando a física "confirma" o estado (1–2 ticks depois). Ver item 1 do catálogo.
3. **Animação que prende o controle** (ataques, pousos longos, "recover" de queda). O Celeste devolve o controle em 0,15 s no dash [7]. Travas deliberadas devem ser curtas e consistentes (HK: 0,2 s de recoil de dano [31]).
4. **Câmera que persegue cada pulo no eixo Y:** quebra a referência de pouso. "Mantenha a câmera parada" [22]; use platform snapping [14][24].
5. **Câmera nauseante:** acelerações bruscas, lerp dependente de framerate, zoom pulsando, shake aleatório de alta frequência. Keren trata conforto como um dos três desafios [14]; Eiserloh recomenda Perlin [15].
6. **Câmera atrasada em alta velocidade** a ponto de o personagem bater na borda da tela (limite do lerp simples [14]), ou câmera andando **para trás** (bug do spindash lag [25]).
7. **Shake em tudo:** perde potência e atrapalha a leitura. Críticas ao HK [30] e "sal" [15]. Sem opção de desligar, é problema de acessibilidade [8][29].
8. **Juice que apaga o contexto:** efeitos que cobrem o personagem e o perigo [19]. O Celeste mostra que sutil funciona [7].
9. **Freeze longo ou frequente:** quebra o ritmo. O Celeste reduziu o hitstun da gem de 0,10 para 0,05 s [8]. Num jogo de speedrun, o freeze ainda mexe no timer se a regra não estiver definida (§10.2).
10. **Estado de recurso invisível:** o jogador aperta dash sem carga e não sabe por quê. Resolver com cor no corpo e som de falha [1][3].
11. **Regras de gancho ambíguas:** "onde pega?" (a confusão do Clawline [36]). Usar affordance clara como os tetos brancos [43].
12. **Quebrar convenções sem ensinar:** sprint que não cancela ao soltar a direção, pogo diagonal por padrão. Divide opiniões [37][38].
13. **Squash que mexe no colisor**, ou escala não inteira gerando mixels em pixel art (§10.2).
14. **Morte cara:** animação longa, loading, voltar longe, timer escondido. É o oposto de [45][8].
15. **Zoom que altera a leitura do perigo** sem sinal explícito [42].
16. **Rumble preso** ou sem parada em pausa ou troca de cena [52].

---

## 12. Fontes que falharam ou dados não confirmados

- **"Remaking Celeste's Player"** (Noel Berry/Maddy): **não encontrado**. Buscas não retornaram esse texto.
- **Arquivos não públicos do Celeste** (`Level.cs`, `Input.cs`, `PlayerDeadBody.cs`, `TrailManager.cs`, `Displacement.cs`): amplitude e duração padrão do shake, duração do buffer de pulo/dash, força do rumble em números, duração da animação de morte, fade das sombras e duração da transição de sala ficam **não confirmados**. O repositório público tem só `Player.cs`, `Readme.md` e o port PICO-8 [2][3].
- **Sonic Retro (SPG:Camera)**: bloqueado por anti-bot (Anubis), e o Wayback falhou por conexão. Usei o **espelho GFDL** `trentbrew/sonic-physics-guide` [25].
- **Transcrições do YouTube:** bloqueio de bot. A do GMTK Celeste veio do archive.org [7]; "Secrets of Game Feel and Juice" e "Are Lives Outdated" vieram do dataset Whisper `taesiri/GMTK-Transcripts` [22][23]. **"The Art of Screenshake"**, **"Juice it or lose it"**, **"Building a Better Jump"** (vídeo) e **"Juicing Your Cameras"** (vídeo) não tinham transcrição. Usei slides do archive.org [15][16] e resumos [17][18]. A duração do "sleep" do Nijman aparece como ~0,2 s num blog de estudante [17]: **não confirmado**.
- **"How to make a good 2D camera" (GMTK):** não localizado. Coberto por Keren [14].
- **Steve Swink, limiares de 100 ms / ciclo de correção:** li só o capítulo 1 (definição e polish) [20]. Os números de latência do cap. 2 ficam **não confirmados**.
- **Hollow Knight `FreezeMoment`:** CSDN com HTTP 521 [34]. Valores **não confirmados**. Guia Steam de shake/rumble com HTTP 429.
- **Raw Meat (McMillen)** no gamedev.net, IndieDB e ModDB: HTTP 403 (Cloudflare). Citações vieram do trecho de busca [46] e do postmortem [45].
- **Celeste Assist Mode (celeste.ink):** HTTP 403. Valores do trecho de busca e de outra fonte [10].
- **Silksong:** PC Gamer com paywall; GameSpot com 403. Não há dados técnicos públicos (hitstop, câmera).
- **SpeedRunners:** TV Tropes com 403. Sem números de câmera, gancho ou boost.
- **Super Meat Boy (original):** tempo de respawn em segundos **não confirmado** (só o SMB 3D, via wiki de fãs [49]). *Indie Game: The Movie* não foi consultado.
- **Celeste — IGT durante o freeze** no jogo principal: **não confirmado** (no Classic, conta [3]).

---

## 13. Referências

[1] NoelFB/Celeste, `Source/Player/Player.cs` (MIT). https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs (raw: https://raw.githubusercontent.com/NoelFB/Celeste/master/Source/Player/Player.cs)
[2] NoelFB/Celeste, `Source/Player/Readme.md`. https://github.com/NoelFB/Celeste/blob/master/Source/Player/Readme.md
[3] NoelFB/Celeste, `Source/PICO-8/Classic.cs` (port C# do Celeste Classic). https://github.com/NoelFB/Celeste/blob/master/Source/PICO-8/Classic.cs
[4] Monocle Engine (Engine.cs, Input/VirtualButton.cs, Util/Calc.cs). https://github.com/JamesMcMahon/monocle-engine
[5] Maddy Thorson, "Celeste & Forgiveness". https://www.maddymakesgames.com/articles/celeste_and_forgiveness/index.html
[6] Maddy Thorson, "Celeste and TowerFall Physics". https://www.maddymakesgames.com/articles/celeste_and_towerfall_physics/index.html
[7] Game Maker's Toolkit, "Why Does Celeste Feel So Good to Play?" (2019). https://www.youtube.com/watch?v=yorTG9at90g; legenda em https://archive.org/details/youtube-yorTG9at90g
[8] Celeste — Changelog oficial. https://www.celestegame.com/changelog.html
[9] Celeste Wiki (Fandom), "Variant Mode". https://celestegame.fandom.com/wiki/Variant_Mode
[10] Assist Mode: Game Developer, "Check out Celeste's remarkably granular Assist options". https://www.gamedeveloper.com/design/check-out-i-celeste-s-i-remarkably-granular-assist-options; celeste.ink (via trecho de busca): https://celeste.ink/wiki/Assist_Mode
[11] Avid Achievers, "Celeste Speedrun Guide". https://avidachievers.com/speedruns/celeste-speedrun-guide/
[12] Nintendo Life, entrevista com Kevin Regamey (Power Up Audio). https://www.nintendolife.com/features/the-secrets-of-sounds-and-the-joy-of-door-noises-an-interview-with-celestes-audio-designer
[13] The Play Journal, "Celeste Review". https://theplayjournal.com/celeste-review/
[14] Itay Keren, "Scroll Back: The Theory and Practice of Cameras in Side-Scrollers" (GDC 2015). https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers; slides: https://archive.org/details/GDC2015Keren
[15] Squirrel Eiserloh, "Math for Game Programmers: Juicing Your Cameras With Math" (GDC 2016), slides. https://archive.org/details/GDC2016Eiserloh
[16] Kyle Pittman, "Math for Game Programmers: Building a Better Jump" (GDC 2016), slides. https://archive.org/details/GDC2016Pittman
[17] Jan Willem Nijman, "The Art of Screenshake" (INDIGO 2013). https://www.youtube.com/watch?v=AJdEqssNZ-U; resumos: https://victorweidar.wordpress.com/2016/10/06/the-art-of-screenshake/ e https://www.bluetengu.com/2014/12/12/art-of-screenshake-experiments/
[18] Martin Jonasson & Petri Purho, "Juice it or lose it" (2012). https://www.youtube.com/watch?v=Fy0aCDmgnxg; https://www.gdcvault.com/play/1016487/juice-it-or-lose
[19] Game Developer, "Indies, resist the urge to 'juice it or lose it'" (Folmer Kelly). https://www.gamedeveloper.com/design/video-indies-resist-the-urge-to-juice-it-or-lose-it-
[20] Steve Swink, *Game Feel*, cap. 1 "Defining Game Feel" (PDF). http://mycours.es/gamedesign2014/files/2014/10/Game-Feel-Steve-Swink-chapter-1.pdf
[21] Martin Pichlmair & Mads Johansen, "Designing Game Feel. A Survey" (2020). https://arxiv.org/abs/2011.09201
[22] Game Maker's Toolkit, "Secrets of Game Feel and Juice" (2015). https://www.youtube.com/watch?v=216_5nu4aVQ; transcrição: https://huggingface.co/datasets/taesiri/GMTK-Transcripts
[23] Game Maker's Toolkit, "Are Lives Outdated Game Design?" (2020). Transcrição no mesmo dataset [22]; referência: https://crashynews.wordpress.com/2020/10/15/game-makers-toolkit-are-lives-outdated-game-design-crash-bandicoot-4-its-about-time/
[24] Game Maker's Toolkit, Platformer Toolkit, scripts Unity (MIT: `characterJuice.cs`, `characterHurt.cs`, `characterJump.cs`, `jumpTester.cs`) e devlog "Behind the Code". https://gmtk.itch.io/platformer-toolkit; https://gmtk.itch.io/platformer-toolkit/devlog/395523/behind-the-code
[25] Sonic Physics Guide, "SPG:Camera" (Sonic Retro, GFDL), via espelho. https://info.sonicretro.org/SPG:Camera; https://github.com/trentbrew/sonic-physics-guide (docs/20-camera.md)
[26] Sonic Physics Guide, "SPG:Animations" (espelho docs/21-animations.md). https://info.sonicretro.org/SPG:Animations
[27] Sonic Physics Guide, "Special Abilities" e "Super Speeds" (espelho docs/16-special-abilities.md, docs/11-forces-subtopics/super-speeds.md). https://info.sonicretro.org/Sonic_Physics_Guide
[28] Sonic Wiki Zone (Fandom), "Boost" e "Power Sneakers (power-up)". https://sonic.fandom.com/wiki/Boost; https://sonic.fandom.com/wiki/Power_Sneakers_(power-up)
[29] Hollow Knight Wiki, "Updates (Hollow Knight)" (patch notes). https://hollowknight.wiki/w/Updates_(Hollow_Knight)
[30] Steam Community, "Screenshake and Screen freeze :: Hollow Knight". https://steamcommunity.com/app/367520/discussions/0/3109152828490913585/
[31] Hollow Knight Wiki (Fandom): "Stalwart Shell", "Steady Body", "Mothwing Cloak". https://hollowknight.fandom.com/wiki/Stalwart_Shell; https://hollowknight.fandom.com/wiki/Steady_Body; https://hollowknight.fandom.com/wiki/Mothwing_Cloak
[32] Unity Discussions, "How to implement a camera secret system like the Hollow Knight one?". https://discussions.unity.com/t/how-to-implement-a-camera-secret-system-like-the-hollow-knight-one/833378
[33] Hollow Knight Speedrunning Wiki, "Hazard warp". https://hollow-knight-speedrunning.fandom.com/wiki/Hazard_warp
[34] CSDN, "[Unity Demo] 从零开始制作空洞骑士 第十三集" (reconstrução não oficial; página inacessível, HTTP 521). https://blog.csdn.net/dangoxiba/article/details/142669576
[35] SVG, citando Edge #354 (Ari Gibson). https://www.svg.com/307138/this-seemingly-small-change-will-make-a-huge-difference-in-hollow-knight-silksong/
[36] Play Critically, "Hollow Knight: Silksong Review" (28/09/2025). https://playcritically.com/2025/09/28/hollow-knight-silksong-review/
[37] Game Rant, "Hollow Knight: Silksong's Unique 'Pogoing' is Not for Everyone". https://gamerant.com/hollow-knight-silksong-pogo-platforming-traversal-good-why/
[38] Steam Community, "What's really annoying in movement :: Hollow Knight: Silksong". https://steamcommunity.com/app/1030300/discussions/0/596287304340168021/
[39] Wikipedia, "Hollow Knight: Silksong". https://en.wikipedia.org/wiki/Hollow_Knight:_Silksong
[40] Hollow Knight Wiki (Fandom), "Clawline". https://hollowknight.fandom.com/wiki/Clawline
[41] Wikipedia, "SpeedRunners". https://en.wikipedia.org/wiki/SpeedRunners
[42] Steam Community, "Screen narrowing VS zooming in :: SpeedRunners" (resposta de desenvolvedor). https://steamcommunity.com/app/207140/discussions/2/666826069005560656/
[43] SpeedRunners Wiki (Fandom), "Controls" e "Tips". https://speedrunners.fandom.com/wiki/Controls; https://speedrunners.fandom.com/wiki/Tips
[44] Thumbsticks, "Interview: Bringing the SpeedRunners party to Nintendo Switch" (Casper van Est). https://www.thumbsticks.com/interview-bringing-the-speedrunners-party-to-nintendo-switch/
[45] Game Developer, "Postmortem: Team Meat's Super Meat Boy". https://www.gamedeveloper.com/audio/postmortem-team-meat-s-i-super-meat-boy-i-
[46] GameDev.net, "Raw Meat: Game Design Tips from Team Meat's Edmund McMillen" (bloqueado; via trecho de busca). https://www.gamedev.net/tutorials/game-design/game-design-and-theory/raw-meat-game-design-tips-from-team-meats-edmund-mcmillen-r2868/
[47] Critical-Gaming Network, "Super Meat Boy pt.3" (2011). https://critical-gaming.squarespace.com/blog/2011/1/6/super-meat-boy-pt3.html
[48] Push Square, "Super Meat Boy Review (PS4)". https://www.pushsquare.com/reviews/ps4/super_meat_boy
[49] supermeatboy.wiki (fã), "Super Meat Boy 3D Beginner Guide". https://supermeatboy.wiki/guide/beginner/
[50] Unity, Cinemachine 3.1: Position Composer, Cinemachine Brain, Impulse Source, Impulse Listener, Pixel Perfect. https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html; .../CinemachineBrain.html; .../CinemachineImpulseSource.html; .../CinemachineImpulseListener.html; .../CinemachinePixelPerfect.html
[51] Unity 6, "Pixel Perfect Camera component reference (URP)". https://docs.unity3d.com/6000.0/Documentation/Manual/urp/2d-pixelperfect-ref.html
[52] Unity Input System 1.11, "Gamepad" (Rumble). https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Gamepad.html
[53] Unity 6 Scripting API, `Time.timeScale`. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-timeScale.html
[54] Unity, Visual Effect Graph, "System Requirements". https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/System-Requirements.html
[55] Unity Input System 1.11, "Settings" (Update Mode). https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Settings.html
