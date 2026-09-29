# PRD — Refactor da movimentação do Cyborg

| | |
|---|---|
| **Status** | Rascunho para revisão do time · 29/09/2026 |
| **Produto** | `Projetos Grupo 5`: plataforma 2D cyberpunk em pixel art (Unity 6000.1.7f1, URP 2D, Input System, LDtk), virando roguelike de speedrun |
| **Documento seguinte** | SPEC técnico do controlador (o "como"), escrito a partir deste PRD |
| **Base** | [PLANO_REFACTOR_ROGUELIKE.md](PLANO_REFACTOR_ROGUELIKE.md) (PLANO) · [movimentacao-implementacao.md](docs/pesquisa/movimentacao-implementacao.md) (IMP) · [movimentacao-gamefeel.md](docs/pesquisa/movimentacao-gamefeel.md) (GF) · [auditoria-movimento-atual.md](docs/pesquisa/auditoria-movimento-atual.md) (AUD) |

**Convenções.**
- **u**: unidade Unity (1 tile = 1 u).
- **alt**: altura do colisor do Cyborg, 1,26 u. **larg**: largura dele, 0,51 u (AUD §2.4).
- **tick**: um passo da simulação do jogador. Este PRD recomenda 60 Hz (1 tick = 16,7 ms, ver Q1).
- **P0**: bloqueia a troca do controlador. **P1**: obrigatório antes do playtest de balanceamento (PLANO, Etapa 4.4). **P2**: desejável, pode ficar para depois.
- Referências no formato "IMP §2.7" apontam para a seção do documento de pesquisa correspondente.

---

## 1. Resumo e problema

### 1.1 Resumo
O controlador de movimento do Cyborg (`characterMovement`) vai ser **substituído por completo** por um novo, no modelo do Celeste. O Celeste é a referência principal. Hollow Knight, Super Meat Boy e SpeedRunners entram como referências secundárias. O modelo tem cinco partes:
- velocidade-alvo com aproximação;
- pulo com *hold* de velocidade e meia gravidade no ápice;
- ajudas invisíveis sistemáticas;
- regras explícitas de momento;
- feedback no mesmo frame da ação.

No kit base, o controlador novo entrega **corrida, pulo variável, queda e fast fall**. **Salto de Parede, Pulo Duplo, Dash e Gancho** ficam como habilidades liberadas por upgrade, lidas de `PlayerStats` (PLANO §3.5). O PRD também define:
- a câmera de gameplay;
- a regra de queda e respawn;
- os eventos que o movimento emite;
- a regra de que **o tempo de fase é medido em ticks de simulação, sem contar freeze frames**.

### 1.2 Problema
O movimento de hoje é imprevisível, trava e esconde informação. Num jogo em que **cada fase precisa ser concluída abaixo de um limite de tempo** e em que **o desempenho decide a raridade dos upgrades**, cada um desses defeitos vira tempo perdido, run perdida ou sensação de injustiça.

| O que o jogador sente | Evidência | Por que é grave aqui |
|---|---|---|
| "O jogo comeu meu pulo" ou "fiquei flutuando" | Um pulo bufferizado com toque rápido zera a velocidade vertical a cada passo. O personagem fica parado ou "anda no ar" até apertar de novo. No kit base (`MaxAirJumps = 0`) isso acontece **em todo toque rápido antes de pousar** (AUD P03) | Perde tempo e, sem pulo aéreo, pode perder a run |
| "Pulo de elevador" que bate num teto invisível | A subida é linear a 7,03 u/s com gravidade 0, e a velocidade vai de 7,03 a 0 em um passo (AUD P01, §7.2) | Não dá para prever o pouso |
| "Segurar mais faz pular menos" | Segurar até o fim dá 2,67 u. Soltar entre 0,36 e 0,38 s dá 3,25 u (+22 %) (AUD P02) | A altura máxima vira truque de frame |
| "Fiquei grudado no teto"; "o pulo morreu perto da moeda" | O teto está na layer errada, e moedas, EndGoal e paredes invisíveis contam como teto (AUD P04) | Rotas cortadas por acidente |
| "Pulando no escuro" | A tela mostra 17,8 × 10 u. O atraso da câmera é de 2,1 u na horizontal (sobram 0,65 s de visão à frente) e de 3 u na queda, com os **pés fora da tela** (AUD P05, §7.7) | Um speedrun exige ler o que vem pela frente |
| Controle "sabonete" e "lamacento" | Virar leva 0,66 s no chão e 0,72 s no ar. Soltar o direcional ainda desliza 1,05 u no chão e 1,84 u no ar. O atrito da engine come ~15 u/s² e os números do Inspector não valem (AUD P07, P08) | Passa da borda em plataformas de 1–2 u |
| "O jogo roubou o controle" | O wall jump trava 100 % do input por 0,3 s, só funciona descendo e o deslize acontece até sem a habilidade (AUD P09) | Contradiz o kit base do plano |
| Boneco atrasado em relação ao controle | Fall → Idle tem 0,25 s fixos, o pulo tem 1 frame de animação e há triggers que vazam (AUD P12) | O feedback mente sobre o estado |
| Cair num vão = esperar o timer acabar | Não há kill-zone nem respawn (AUD P21) | Soft-lock dentro de uma run |
| Só teclado | Sem gamepad e sem eixo vertical (AUD P13) | Um plataforma de precisão sem controle perde público |
| **Fases impossíveis no kit base** | Pelo modelo de alcançabilidade, a Quinta exige pulo duplo, e a Quarta exige pulo duplo + wall jump, com o wall jump recarregando o pulo aéreo (AUD §5.3) | **Conflita com a decisão D1** do plano (§10) |

---

## 2. Visão e pilares de feel

**Visão:** *"Cada segundo perdido é culpa minha, e cada segundo ganho é mérito meu."* O Cyborg responde no primeiro frame, perdoa erros de timing pequenos e recompensa quem aprende a rota e a técnica. O kit base já é bom por si só. Um upgrade acrescenta um verbo ou abre uma rota; **nunca conserta uma base ruim**.

| # | Pilar | O que significa na prática | Referência | Por que no roguelike de speedrun |
|---|---|---|---|---|
| F1 | **Responsivo no 1º frame** | A ação e o seu feedback (squash, poeira, som) aparecem no mesmo frame em que o input é aplicado. Chegar à velocidade máxima leva ~0,1 s | Celeste `Jump()` aplica velocidade, squash, poeira e som no mesmo update (GF §1.1, §2.2); SMB "instant-on instant-off" (IMP §6) | Atraso de input vira "o jogo me custou tempo" |
| F2 | **Justo e perdoador** | Coyote, buffer, correção de quina e wall jump a distância alargam as janelas **a favor da intenção** do jogador. Nenhum input é "comido" | "Everything is fudged a tiny bit in the player's favor" (IMP §2.12, GF §2.10); SMB: wall jump com 200 ms de tolerância (IMP §6) | Uma run que termina por input perdido é inaceitável (D3: morte = fim da run) |
| F3 | **Previsível e legível** | O mesmo input sempre gera a mesma trajetória, o pulo cresce de forma monotônica com o botão e o estado dos recursos aparece no corpo | Cabelo da Madeline como indicador de recurso (GF §2.5); determinismo (IMP §12) | Tentativa e erro contra o relógio só funciona se o jogador conseguir aprender a rota |
| F4 | **Momentum recompensa maestria** | O teto de velocidade é macio: a sobrevelocidade decai devagar se o jogador segurar a direção. O pulo dá um impulso horizontal, e o dash e o gancho preservam velocidade. Sempre com teto global | Celeste `RunReduce`, `JumpHBoost`, dash que não reduz X (IMP §2.6, §2.10); Overmax do SpeedRunners (IMP §7) | Nota S → raridades melhores. A expressão de habilidade precisa aparecer no timer |
| F5 | **Rápido para tentar de novo** | Queda → controle em ≤ 0,5 s, sem soft-lock, timer honesto e visível | "A penalidade é quanto tempo leva para voltar a jogar" (SMB, GF §7, §9.2); respawn do Celeste (GF §2.11) | Runs curtas e frequentes; o tempo já é a punição |
| F6 | **Upgrade muda o "como", não estraga o "sentir"** | Stats com teto; nenhum upgrade mexe em gravidade, buffer ou trava de input | Monarch Wings e Mantis Claw adicionam verbos sem mudar a base (IMP §4.3) | 14+ upgrades empilháveis (D5) não podem descaracterizar o controle |

---

## 3. Público e contexto de uso
- **Jogadores:** fãs de plataforma de precisão (Celeste, Super Meat Boy, Hollow Knight) e de roguelikes curtos. É gente que repete tentativas e aprende rotas. Precisa ser jogável por novatos: a Primeira fase serve de tutorial implícito.
- **Dispositivo:** PC com **teclado** (padrão atual) e **gamepad** Xbox/PlayStation (novo). Monitores de 60 a 144 Hz, com VSync ligado ou desligado. O movimento não pode mudar com o framerate.
- **Sessão:** uma run tem 3 fases com limites provisórios de 20/30/25 s (D6), ou seja, ~75 s de gameplay mais as telas de resultado e upgrade. São muitas runs por sessão de 10–30 min. Como a morte encerra a run (D3), o jogador recomeça com frequência.
- **Modo de jogo:** tentativa e erro contra o relógio. O jogador olha o timer, mede a rota e tenta de novo. O que importa: **leitura da tela à frente, controle fino no ar, consistência e reinício rápido**.
- **Público interno:** quem ajusta os números (time de design) mexe em ScriptableObjects no Play Mode, sem tocar em código.

---

## 4. Escopo

### 4.1 Natureza da mudança
- **É uma troca completa de controlador**, não um remendo do `characterMovement`. Os contratos externos são preservados: tag e layer `Player`, GUID e fileID do `Cyborg.prefab`, API usada por outros scripts até o Event Bus chegar (AUD §9.1). Toda a lógica interna é nova.
- **O critério "sensação idêntica ao atual" das tarefas 2.1 e 2.4 do PLANO deixa de valer.** Os testes de "valores de referência" (2.1) passam a congelar o **perfil novo** (§7), e o critério de pronto da 2.4 passa a ser "metas do §7 verificadas por testes EditMode + playtest". O risco do PLANO §8 "o movimento muda de sensação" vira **mudança intencional**.

### 4.2 Dentro do escopo
1. O controlador novo: corrida, pulo, queda, paredes, pulo aéreo, dash e integração do gancho como estado do controlador.
2. Ajudas invisíveis, colisão e teto.
3. Morte do ponto de vista do movimento, queda para fora da fase com respawn, e "voltar ao spawn".
4. Câmera de gameplay: suavização, look-ahead, look-down, platform snapping e shake.
5. Feedback de movimento: estados de animação, squash & stretch, partículas, SFX, rumble e indicador de recurso no corpo.
6. Input: gamepad, eixo vertical, rebinding, nenhum input perdido, e input de menu que não vaza para o jogo.
7. Opções de acessibilidade do feedback.
8. Integração com `PlayerStats`, `StatType`/`AbilityFlags` e os eventos do PLANO §3.4.
9. Ferramentas de tuning: perfil em ScriptableObject, overlay de debug e cena sandbox com "Play direto".
10. Remoção do controlador antigo e dos scripts mortos de movimento (`VerticalJumpController`, `MovementDiagnostic`).
11. As edições de geometria na Quarta e na Quinta necessárias para a D1 (§10).

### 4.3 Fora do escopo (com justificativa)

| Fora | Por quê |
|---|---|
| **Escalada com estamina** (grab do Celeste) | Exige um sistema de recurso, UI/feedback de estamina e geometria pensada para escalar. O Salto de Parede já cobre a travessia vertical em paredes e chaminés. A sheet `Climb` existe (AUD §4.4), mas a mecânica fica como candidata a upgrade Lendário futuro (flag `WallGrab` reservada, §9.2) |
| Agachar, hyper, wavedash e ultra | Não há arte de agachar. Essas técnicas dependem de agachar ou de multiplicadores reaplicáveis, que exigem teto de velocidade e fases feitas para eles (IMP §12). O *super* entra como P2 |
| Rampas, plataformas móveis, one-way, lift boost | As fases não têm nenhum desses elementos (AUD §5.1) |
| Inimigos, combate e dano | Fora do movimento. Só o contrato de morte (`PlayerDied`) e a regra de colisão com inimigos (Q9) ficam aqui |
| Gancho em pêndulo (SpeedRunners) | É outra mecânica. O MVP mantém o "puxar até o ponto" (modelo Clawline do Silksong, IMP §5) e só corrige a integração |
| Ghosts, replays e leaderboard | Ficam para depois. **O determinismo que os permite é requisito agora** (RNF-02) |
| Assist mode completo, música reativa, zoom dinâmico | Baixo impacto e risco de mudar a leitura do timing (GF §6, §10 item 27) |
| Arte nova (dash, morte, skid) | Usar a arte existente mais efeitos por código (AUD §4.4). Frames novos são melhoria, não bloqueio |
| Redesenho de fases além do que a D1 exige | Só as edições da §10 |

---

## 5. Kit de movimento

### 5.1 Base × upgrade

| Mecânica | Onde | Comportamento esperado | Referência (por quê) |
|---|---|---|---|
| **Corrida** | Base | Aproxima uma velocidade-alvo em ~0,1 s. Para em ~0,1 s ao soltar e vira em ~0,2 s. Acima do máximo, se o jogador segurar a direção, a sobrevelocidade decai devagar | Celeste `RunAccel`/`RunReduce` (IMP §2.6): precisão com espaço para momento |
| **Pulo variável** | Base | Mantém a velocidade de subida enquanto o botão está pressionado, por até ~0,2 s. Depois vem um arco de gravidade, com meia gravidade no ápice se o botão continuar pressionado. Soma um impulso horizontal na direção do input | Celeste (IMP §2.7). HK usa o mesmo desenho de hold (IMP §4.3). Resolve P01 e P02 |
| **Queda + fast fall** | Base | A queda aproxima suavemente um teto de velocidade. Segurar ↓ no ar eleva o teto | Celeste `MaxFall`/`FastMaxFall` (IMP §2.8): o jogador decide quando descer rápido |
| **Ajudas invisíveis** | Base | Coyote, buffer que funciona com o botão solto, correção de quina, tolerância de teto e retenção de velocidade na parede | IMP §2.12, GF §2.10 (pilar F2) |
| **Paredes** | Base = só bloqueiam | Sem a habilidade, não há deslize nem pulo de parede | Legibilidade: um deslize sem pulo ensina uma affordance falsa (hoje ele acontece sem a flag, AUD P09) |
| **Salto de Parede** | Upgrade Épico (flag `WallJump`) | Desliza devagar ao segurar contra a parede e pula a até 0,3 u dela, subindo ou descendo. Trava o input por 0,16 s só se havia direção pressionada. O *neutral jump* (sem direção) sobe a chaminé. Tem "wall coyote" | Celeste wall jump e neutral jump (IMP §2.9); tolerância do SMB (IMP §6); Mantis Claw do HK (IMP §4.3) |
| **Pulo Duplo** | Upgrade Épico (`MaxAirJumps` +1) | Substitui a velocidade vertical, mesmo caindo, por um pulo variável de até 3,0 u | Monarch Wings do HK: a altura varia com o botão e o pulo recarrega em contato (IMP §4.3) |
| **Dash** (novo) | Upgrade Épico (`MaxDashes` +1), P1 | 8 direções, ~4 u em 0,15 s, sem gravidade. Freeze de 0,05 s com a mira lida depois dele. Recarrega no chão | Celeste (IMP §2.10, GF §2.6b): verbo "caótico" que contrasta com o controle fino (GF §2.1b) |
| **Gancho** | Upgrade Épico (flag `GrapplingHook`) | Mira automática no alvo válido mais próximo, com indicador e preferência pela direção pressionada. Puxa até o ponto e lança ao soltar. Pular durante o puxão cancela | Clawline do Silksong (IMP §5), affordance clara do SpeedRunners (GF §6) |

**Avaliação do Dash.**
- **A favor:**
  - é o verbo mais "speedrun" do gênero e cria decisões de rota;
  - a regra de preservação de momento alimenta o pilar F4;
  - a leitura da mira depois do freeze é uma ajuda disfarçada (GF §2.6);
  - completa um quarteto de Épicos com texturas diferentes.
- **Contra:**
  - não existe arte de dash (AUD §4.4);
  - exige mais um botão e o eixo vertical;
  - pode encurtar fases a ponto de exigir recalibrar o D6;
  - traz o freeze, que exige a regra de timer do RNF-04.
- **Decisão:** entra como **P1**, Épico, com placeholder visual (sprite esticado + afterimages). O *super* (dash + pulo com coyote) fica em **P2**.

### 5.2 Interações e recargas

| Recurso ↓ / evento → | Tocar o chão | Wall jump | Prender o gancho | Respawn |
|---|---|---|---|---|
| Pulos aéreos | recarrega | **recarrega** (preserva a rota da Quarta, AUD §5.3, §9.1-6) | não (upgrade *Âncora*: sim) | recarrega |
| Dash | recarrega depois de 0,1 s do início do dash | não (upgrade *Recarga na Parede*: sim) | não | recarrega |
| Coyote | arma (0,1 s) | wall coyote de 0,1 s ao desencostar | — | — |
| Cooldown do gancho | — | — | começa no disparo | zera |

**Prioridades**
1. **Pulo.** Perto de uma parede e com a habilidade, o botão faz wall jump. Senão, com coyote ativo, faz pulo do chão. Senão, se houver carga, faz pulo aéreo. Senão, o aperto fica no buffer.
2. **Durante o dash.** Não há gravidade nem controle. O wall jump é permitido. O pulo fica no buffer e sai ao fim do dash (o super entra em P2). O gancho também fica no buffer.
3. **Durante o puxão do gancho.** Pular cancela o gancho e aplica um pulo do chão, sem gastar pulo aéreo. Dash também cancela o gancho.
4. **Morte ou `DisableMovement`.** Cancela tudo e ignora input, gancho incluído (hoje ele não respeita, AUD P14).

---

## 6. Requisitos funcionais

Critérios "± 1 tick" se referem ao tick escolhido no SPEC. As métricas M01…M34 estão no §7.

### 6.1 Corrida

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-01 | A velocidade horizontal aproxima `MaxSpeed × direção`, com taxas separadas para chão e ar. O input horizontal é digital (−1/0/+1), e o stick tem zona morta configurável (padrão 0,3) | P0 | M01–M07 dentro de ± 1 tick em teste EditMode. Stick com deflexão de 0,5 = mesma velocidade do teclado |
| RF-02 | Nenhuma força da engine (atrito, quique, depenetração) altera a velocidade do jogador | P0 | Velocidade máxima medida em Play Mode = perfil ± 2 %. Correndo e caindo contra uma parede: `vx = 0` e a queda é igual à queda livre ± 2 % (nada de "grudar") |
| RF-03 | Sobrevelocidade: acima de `MaxSpeed`, segurando a mesma direção, a velocidade decai à taxa de freio (M08). Soltando ou invertendo, a taxa é a normal | P1 | De 17 para 10 u/s no chão segurando a direção em 0,175 s ± 1 tick; soltando, 17 → 0 em 0,17 s ± 1 tick |
| RF-04 | Retenção de velocidade: se uma parede deixar de bloquear em ≤ 0,06 s (o jogador passou pela quina), a velocidade horizontal anterior volta | P1 | Cena de teste com quina: `vx` de antes da batida é restaurado |

### 6.2 Pulo

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-05 | Pulo variável por hold: a velocidade de subida é mantida enquanto o botão está pressionado, por até ~0,2 s. Soltar só encerra o hold, sem zerar `vy`. Depois vem o arco de gravidade | P0 | M10 = 3,5 ± 0,1 u; M11 = 0,9 ± 0,1 u; altura **monotônica não decrescente** varrendo de 1 a 30 ticks de botão |
| RF-06 | Arco contínuo: nenhuma descontinuidade de `vy` fora de eventos explícitos (início de pulo, chão, teto, dash, gancho, wall jump) | P0 | Em voo livre, `|Δvy|` por tick ≤ gravidade × dt (+1 %) |
| RF-07 | Meia gravidade no ápice (`|vy|` < ~5 u/s) enquanto o botão está pressionado | P0 | M13 e M14 dentro de ± 1 tick. Com o botão pressionado, o tempo com `|vy|` < 5 u/s é ≥ 1,8× o tempo sem segurar |
| RF-08 | Impulso horizontal aditivo de +4 u/s na direção do input em todo pulo do chão (inclusive coyote e buffer) | P0 | Pular a 10 u/s → 14 ± 0,1 u/s. Pular parado com direção → 4 u/s |
| RF-09 | Coyote de 0,1 s depois de sair de uma borda sem pular. É consumido pelo pulo | P0 | A 60 Hz, pular no 6º tick fora da borda funciona e no 7º não. Nunca saem dois pulos do chão do mesmo coyote (a correção do bug 6 continua valendo) |
| RF-10 | Buffer de 0,1 s: um aperto até 0,1 s antes de o jogador poder pular (pouso, coyote, parede) executa o pulo no primeiro tick possível, **mesmo com o botão já solto** (sai o pulo mínimo). Cada aperto gera no máximo uma ação | P0 | Toque de 1 tick a 1–6 ticks do pouso → sempre pula, com M11. A 7 ticks → não pula. Um aperto = um pulo (corrige P03) |
| RF-11 | Nenhum estado deixa o jogador parado ou "andando" no ar fora de wall slide, gancho ou dash | P0 | Fuzz test com 10 000 sequências aleatórias de input em 3 geometrias: 0 ocorrências de ≥ 3 ticks no ar com `vy = 0` fora do ápice |
| RF-12 | Ordem de prioridade do pulo conforme §5.2 | P0 | Tabela de casos coberta por teste EditMode |

### 6.3 Queda

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-13 | A queda aproxima suavemente um teto de 17 u/s. Nada de arrasto por tick | P0 | M16 e M18. Com o tick a 50 Hz e a 60 Hz, a velocidade terminal varia ≤ 2 % (hoje dependeria do dt, AUD P06) |
| RF-14 | Fast fall: segurar ↓ no ar eleva o teto para 24 u/s, com transição de ~0,2 s. Não tem efeito no chão nem no wall slide | P1 | M17. Soltar ↓ volta ao teto de 17 u/s na mesma taxa |
| RF-15 | A gravidade e a queda não dependem de `JumpHeight` | P0 | Com +16 % de `JumpHeight`: gravidade e M16 idênticos, altura +16 % ± 1 % (corrige P22) |

### 6.4 Paredes (habilidade Salto de Parede)

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-16 | Sem a flag, a parede só bloqueia: não há deslize nem wall jump | P0 | Caindo encostado na parede e segurando contra ela = queda livre |
| RF-17 | Wall slide só com a flag, **segurando em direção à parede** e com `vy ≤ 0`. Teto de queda de 2,5 u/s, alcançado em ≤ 0,1 s ao entrar em queda rápida | P0 | M22. Passar raspando sem segurar a direção não desliza |
| RF-18 | Wall jump a até 0,3 u da parede, subindo ou descendo. Impulso de 14 u/s para longe, com `vy` e hold iguais aos do pulo. Trava de 0,16 s **só se havia direção pressionada**. Sem direção sai o *neutral jump*, que volta para a parede | P0 | M23–M25. Cena de teste: sobe a chaminé de 3 u da Quarta alternando paredes, sem pulo aéreo |
| RF-19 | O wall jump recarrega os pulos aéreos | P0 | Com `MaxAirJumps = 1` já gasto, depois do wall jump o pulo aéreo está disponível |
| RF-20 | Wall coyote: até 0,1 s depois de desencostar de um wall slide, ainda é possível dar wall jump | P1 | Pulo no 6º tick após sair da parede = wall jump; no 7º, não |
| RF-21 | A detecção de parede cobre toda a lateral do colisor, exceto 0,1 u em cada ponta | P1 | Wall jump funciona com só a metade superior do corpo encostada numa quina |
| RF-22 | Paredes invisíveis de limite (layer Default) bloqueiam e contam como teto, mas **não** permitem slide nem wall jump. Triggers nunca contam como chão, parede ou teto | P0 | Wall jump numa borda invisível falha. Moeda e EndGoal não cortam o pulo (corrige P04) |

### 6.5 Pulo aéreo (Pulo Duplo)

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-23 | `MaxAirJumps` pulos no ar. Cada um substitui a `vy` atual (mesmo caindo rápido) por um pulo com hold de até 3,0 u (85 % de `JumpHeight`). Recarrega no chão, no wall jump e no respawn | P0 | M26 = 3,0 ± 0,1 u partindo de qualquer `vy`. M27 ≈ 6,5 u |
| RF-24 | Com `MaxAirJumps = 0`, apertar pulo no ar fora do coyote não faz nada além de guardar o aperto no buffer | P0 | Nenhuma mudança de `vy`. Se pousar dentro de 0,1 s, o pulo sai (RF-10) |

### 6.6 Dash (P1)

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-25 | Dash em 8 direções (sem direção = para onde o personagem olha): 27 u/s por 0,15 s, sem gravidade e sem controle. Ao terminar, sai a 17 u/s (a componente para cima é multiplicada por 0,75). **Nunca reduz uma velocidade horizontal maior no mesmo sentido** | P1 | M28. Teste: dash horizontal a 20 u/s mantém 20 u/s |
| RF-26 | Cargas = `MaxDashes`. Recarrega no chão depois de 0,1 s do início do dash. Cooldown de 0,2 s. Buffer de 0,1 s | P1 | Dash no chão só recarrega depois da janela de 0,1 s. Sem carga → `PlayerActionDenied` |
| RF-27 | Freeze de 0,05 s no início do dash. A direção é lida **ao fim** do freeze | P1 | Mudar a diagonal durante o freeze muda a direção. O freeze não conta no timer (RNF-04) |
| RF-28 | Correção de quina no dash horizontal: até 0,25 u na vertical | P1 | Um dash rente a uma borda "sobe" a quina em vez de parar |
| RF-29 | *Super*: pular durante um dash horizontal com coyote ativo dá uma velocidade horizontal alta e fixa (valor no SPEC, respeitando o teto global) | P2 | Técnica reproduzível em teste; o teto global nunca é excedido |

### 6.7 Gancho

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-30 | O gancho é um estado do controlador e respeita morte e `DisableMovement`: não dispara e é cancelado se estiver ativo | P0 | Depois de `Die()`, apertar o botão do gancho não faz nada (corrige P14) |
| RF-31 | O lançamento não tem clamp vertical. Resultado esperado: +5 ± 0,3 u acima do ponto de soltura em lançamento vertical puro, e ≥ 12 u de deslocamento horizontal até voltar a `MaxSpeed` segurando para a frente | P0 | M30. Com o upgrade de força (+40 %), as duas medidas aumentam de forma visível |
| RF-32 | Indicador (retícula) no alvo válido atual antes do disparo. A mira automática prefere alvos na direção pressionada | P1 | Com 2 alvos no raio, segurar a direção de um deles escolhe esse alvo |
| RF-33 | Pular durante o puxão cancela o gancho e aplica um pulo do chão, sem gastar pulo aéreo e preservando `vx` | P1 | Teste de estado |
| RF-34 | Buffer de 0,1 s para o gancho. Apertar durante o cooldown guarda o aperto no buffer | P1 | O disparo sai no primeiro tick depois do cooldown, se o aperto ainda estiver no buffer |

### 6.8 Ajudas invisíveis e colisão

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-35 | Correção de quina ao subir: até 0,25 u de deslocamento lateral, sem perder `vy` | P0 | Cabeça a 0,2 u de uma quina → desliza e segue subindo. A 0,3 u → bate |
| RF-36 | Tolerância de teto: bater a cabeça nos primeiros 0,05 s não cancela o hold | P1 | Teste com teto baixo |
| RF-37 | Chão só com contato real (≤ 0,02 u) e `vy ≤ 0`. Acabar com o "pouso antecipado" de 0,18 u | P0 | `PlayerLanded` no mesmo tick do contato (corrige P25) |
| RF-38 | O jogador não trava em emendas de tiles dos composites | P0 | Varredura automática de todo chão e parede das 3 fases a 10 e 27 u/s: 0 paradas espúrias |
| RF-39 | Nada é atravessado em alta velocidade | P0 | A 27 u/s (0,45 u/tick) o jogador não atravessa a placa de 0,15 u da Quarta nem paredes de 1 u |
| RF-40 | A hitbox fica simétrica ao virar (hoje o offset de −0,088 u não espelha) | P0 | A posição do colisor não muda com o `flip` |

### 6.9 Morte, queda e reinício

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-41 | Morte (bala, inimigo, tempo esgotado): trava input **e** física (gravidade inclusa), cancela gancho e dash, e emite `PlayerDied` uma única vez | P0 | Chamar a morte duas vezes → 1 evento. O corpo não "desce devagar" (corrige P23) |
| RF-42 | **Queda para fora da fase não é morte.** Quando o jogador sai pelos limites de baixo, volta ao **último chão seguro** em ≤ 0,5 s, com o timer rodando e a câmera em corte | P0 | M31. 0 soft-locks nas 3 fases (corrige P21). Os vãos da Primeira passam a ter kill-zone |
| RF-43 | "Voltar ao spawn": segurar o botão por 0,3 s. **Não zera o timer**, para ninguém reiniciar até tirar nota S | P1 | Soltar antes de 0,3 s não faz nada. O timer continua |
| RF-44 | O respawn tem feedback: squash de "pouso" (1,5; 0,5) e som | P1 | Presente em 100 % dos respawns |
| RF-45 | Marcador persistente dos locais de queda ou morte dentro da run | P2 | Visível nas tentativas seguintes da fase |

### 6.10 Câmera

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-46 | Suavização exponencial por eixo, independente de framerate: meia-vida de 0,08–0,15 s no eixo horizontal e de 0,15–0,25 s no vertical. O parallax não fica 1 frame atrasado | P0 | A mesma trajetória a 30, 60 e 144 fps gera posições de câmera iguais (± 0,01 u) nos mesmos instantes |
| RF-47 | Look-ahead: na velocidade máxima estável, ≥ 12 u visíveis à frente. Rampa suave e sem câmera andando para trás em inversões | P0 | M33 |
| RF-48 | Look-down: caindo acima de 50 % da queda máxima, ≥ 5 u visíveis abaixo dos pés. Os pés nunca saem da tela | P0 | Teste com queda de 20 u (corrige P05) |
| RF-49 | Platform snapping: pulos com ápice ≤ 4 u acima do último chão movem a câmera em Y no máximo 0,25 u. A câmera volta a seguir em Y ao cair mais de ~1/4 da tela ou ao subir além da janela | P1 | Pulos repetidos no plano: Y da câmera estável |
| RF-50 | Área visível ≥ 20 × 11,25 u, com clamp pela `CameraBoundary` e sem mostrar nada abaixo da zona de queda | P1 | M32 |
| RF-51 | Shake aditivo, direcional, curto (dash: 0,2 s, 1–2 px) e em pixels inteiros | P1 | Opção 0/50/100 % respeitada (RF-65) |
| RF-52 | Foco do gancho no ponto médio entre jogador e âncora; atraso dramático de 2–4 ticks no lançamento | P2 | — |

### 6.11 Feedback e game feel

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-53 | O feedback (squash, partícula, som) aparece **no mesmo frame renderizado** em que a ação é aplicada: pulo, wall jump, pulo aéreo, dash, pouso | P0 | Captura quadro a quadro: 0 frames de diferença entre a mudança de velocidade e o efeito |
| RF-54 | A animação é guiada pelo estado do controlador, sem triggers que vazam. Estados: Idle, Run, Rise, Apex, Fall, Land, WallSlide, AirJump, Dash. Há transição Fall → Run direta. A antecipação é cosmética. O `Speed` do Animator é a velocidade real. O personagem não fica de costas para a parede | P0 | Pousar correndo mostra Run em ≤ 1 frame. Um pulo de coyote não deixa trigger pendente (corrige P12, P16, P17) |
| RF-55 | Squash & stretch só no sprite filho, nunca no colisor. Pulo (0,6; 1,4). Pouso proporcional à velocidade de queda, até (1,6; 0,4). Retorno linear de ~1,75 por segundo. Valores ajustáveis | P0 | O colisor não muda. Valores iniciais da GF §2.2 |
| RF-56 | Partículas: 4 no pulo, 8 no pouso forte (≥ 50 % da queda máxima), emissão contínua no wall slide, diagonais no wall jump, e no dash | P1 | Contagens do GF §2.4, em pool, sem alocação por evento |
| RF-57 | SFX: passos por Animation Event, pulo, pouso, wall jump, pulo aéreo, dash, gancho (disparo, prender, soltar), recarga e "sem recurso" (este com cooldown de 0,1 s). Variação de pitch de ±3–6 % | P1 | Cada evento do §9.3 tem som |
| RF-58 | Recurso legível no corpo (visor ou LED do Cyborg): cargas de pulo aéreo e de dash. Flash de 0,12 s ao recarregar. Cor **e** brilho ou forma | P1 | O jogador identifica "tenho carga?" sem HUD (pergunta do playtest, §11) |
| RF-59 | Rumble com hierarquia: pulo sem rumble, pouso leve, dash forte, morte leve/médio. Sempre para em pausa, morte, troca de cena e perda de foco | P1 | Nenhum motor fica ligado depois desses eventos |
| RF-60 | Rastro do dash (3 afterimages coloridas pelo recurso que sobrou) e anéis de velocidade acima de 1,4× `MaxSpeed` | P2 | — |

### 6.12 Input

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-61 | Teclado e gamepad, com eixo vertical (fast fall e mira do dash). Mapeamento padrão: mover ←/→ ou A/D ou stick/D-pad; cima/baixo ↑/↓ ou W/S; pulo Espaço ou C ou Sul; dash X ou Shift ou Oeste/RB; gancho G ou Z ou Leste/RT; voltar ao spawn R ou Select (segurar) | P0 | Uma run completa só com gamepad e outra só com teclado |
| RF-62 | Nenhum aperto se perde entre frame e tick. Apertar e soltar no mesmo frame conta como pulo mínimo | P0 | Teste automatizado com 10 000 apertos em cadências aleatórias: 0 perdidos |
| RF-63 | O input de menus e pausas não vaza para o jogo: o botão que confirma um upgrade não gera pulo bufferizado no 1º tick da fase | P0 | Teste de transição de cena |
| RF-64 | Rebinding em jogo (teclado e gamepad), salvo nas preferências, com tecla alternativa de pulo | P1 | Rebinding persiste entre sessões |

### 6.13 Acessibilidade, opções e ferramentas

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RF-65 | Opções: shake 0/50/100 % (padrão 50 %, como o Celeste, GF §2.7), rumble 0/50/100 %, freeze liga/desliga (sem efeito no timer), redução de flashes | P1 | Com freeze desligado, o tempo final de uma run gravada é idêntico (RNF-04) |
| RF-66 | Nenhum indicador depende só de cor | P1 | Revisão com simulador de daltonismo |
| RF-67 | Modo assistido (velocidade do jogo de 70 a 100 %) que marca a run como assistida | P2 | — |
| RF-68 | Overlay de debug do movimento (estado, velocidade, altura e distância do último pulo, timers de coyote e buffer) e cena sandbox com "Play direto" | P1 | Tuning possível sem passar pelo MainMenu (AUD P13, §9.4-10) |

---

## 7. Metas numéricas de feel ("feel budget")

As metas são **resultados medidos**, não constantes. O SPEC escolhe as constantes que as atingem (ponto de partida no §7.2) e as congela em testes EditMode. Os números do Celeste são as constantes do `Player.cs` convertidas com 1 tile = 1 u (IMP §10), e os tempos são as simulações da IMP §2.6–2.10. HK só tem valores confirmados pela wiki (IMP §4.2), e as unidades são as do próprio HK. "(deriv.)" indica uma conta feita a partir de constantes documentadas.

| # | Métrica | Atual (AUD §7) | Celeste convertido | Hollow Knight | **Alvo (u, s)** | **Alvo (Cyborg)** |
|---|---|---|---|---|---|---|
| M01 | Velocidade máxima no chão | 10,5 u/s (10,2 real) | 11,25 u/s | 8,3 u/s (10 com Sprintmaster) | **10 u/s** | 7,9 alt/s |
| M02 | 0 → máx. no chão | 0,36 s (90 %) | 0,09 s | ~instantânea (inf.) | **0,10 s** | 0,5 u de pista (0,4 alt) |
| M03 | Parar no chão (soltar) | 0,24 s · 1,05 u | 0,09 s · ~0,5 u (deriv.) | ~instantânea (inf.) | **0,10 s · 0,5 u** | 0,4 alt · 1 larg |
| M04 | Virar no chão (máx. → −máx.) | 0,66 s | 0,18 s | n/c | **0,20 s** | — |
| M05 | 0 → máx. no ar | 0,53 s | 0,138 s | n/c | **0,15 s** | — |
| M06 | Parar no ar | 0,35 s · 1,84 u | 0,138 s · ~0,78 u (deriv.) | n/c | **0,15 s · 0,8 u** | 0,6 alt |
| M07 | Virar no ar | 0,72 s | 0,277 s | n/c | **0,31 s** | — |
| M08 | Freio de sobrevelocidade segurando a direção | não existe | 50 u/s² (×0,65 no ar) | — | **40 u/s² (26 no ar)** | — |
| M09 | Impulso horizontal do pulo | 0 | +5 u/s | n/c | **+4 u/s** | — |
| M10 | Altura máxima do pulo | 2,67 u (3,25 com o truque P02) | 3,56 u | n/c | **3,5 u ± 0,1** | **2,8 alt** |
| M11 | Altura mínima (toque) | 0,72 u | 0,88 u | n/c | **0,9 u ± 0,1** | 0,7 alt |
| M12 | Faixa do pulo variável | 3,7:1, **não monotônica** | ~4:1 | "segurar = mais alto" | **~3,9:1, monotônica** | — |
| M13 | Tempo até o ápice (máx.) | 0,38 s | ~0,35 s | n/c | **0,35 s ± 0,02** | — |
| M14 | Tempo no ar (pulo máx., mesmo nível) | 0,82 s | ~0,68 s | n/c | **~0,68 s** | — |
| M15 | Distância do pulo máx. correndo | 8,6 u | ~8,1 u | n/c | **≥ 7 u (~7,1)** | 5,6 alt · 14 larg |
| M16 | Velocidade máxima de queda | 15,2 u/s (arrasto por passo) | 20 u/s | n/c | **17 u/s** | 13,5 alt/s |
| M17 | Fast fall | — | 30 u/s | — | **24 u/s** | 19 alt/s |
| M18 | 0 → queda máxima | −14 u/s depois de 1 s | ~0,18 s | n/c | **~0,15 s** | — |
| M19 | Coyote | 0,1 s | 0,1 s | n/c | **0,1 s** (6 ticks) | ~1 u além da borda na velocidade máxima |
| M20 | Buffer de pulo | 0,1 s (quebrado, P03) | ~4 f ≈ 0,067 s (comunidade); Classic 0,133 s | n/c | **0,1 s, funcionando com o botão solto** | — |
| M21 | Correção de quina ao subir | nenhuma | 0,5 u (50 % da largura da Madeline) | n/c | **0,25 u** | 0,5 larg |
| M22 | Wall slide | 2 u/s (mesmo sem a habilidade) | 2,5 u/s, subindo até 20 u/s em 1,2 s | n/c | **2,5 u/s constante** | 2 alt/s |
| M23 | Wall jump (vx; vy) | (8; 14) | (16,25; 13,1) | diagonal, sem número | **(14; igual ao pulo)** | — |
| M24 | Trava de input do wall jump | 0,3 s, 100 %, sempre | 0,16 s, só se havia direção | — | **0,16 s, só com direção** | — |
| M25 | Distância máxima da parede para o wall jump | 0,15 u (1 raio) | 0,375 u (vão ≤ 0,25 u) | — | **≤ 0,3 u** | 0,6 larg |
| M26 | Pulo aéreo (máx.) | +2,46 u | — | varia com o botão | **3,0 u** | 2,4 alt |
| M27 | Alcance vertical com 1 pulo aéreo | 5,1 u | — | — | **~6,5 u** | 5,2 alt |
| M28 | Dash | — | 30 u/s × 0,15 s = 4,5 u; sai a 20 u/s | cooldown 0,6 s (0,4 com Dashmaster) | **27 u/s × 0,15 s ≈ 4 u; sai a 17 u/s; cooldown 0,2 s** | 3,2 alt |
| M29 | Freeze no início do dash | — | 0,05 s | — | **0,05 s, fora do timer** | 3 ticks |
| M30 | Gancho: subida após soltura vertical | +5,1 u (limitada pelo clamp de 20 u/s) | — | — | **+5 u ± 0,3** | 4 alt |
| M31 | Queda para fora da fase → controle | não existe (espera o timer) | tween de 0,6 s; Classic 0,5 s | hazard respawn (sem número) | **≤ 0,5 s** | — |
| M32 | Área visível | 17,8 × 10 u | 40 × 22,5 u | n/c | **≥ 20 × 11,25 u** | 16 × 9 alt |
| M33 | Visão à frente na velocidade máxima | 6,8 u (0,65 s) | — | — | **≥ 12 u (1,2 s)** | — |
| M34 | Latência adicionada (input processado → 1º frame com resposta) | até ~40 ms (20 de física + 20 de interpolação) | 60 Hz travado | — | **≤ 2 frames a 60 fps (meta: 1)** | — |

### 7.1 Raciocínio
1. **Escala compatível com o Celeste.** Nos dois jogos, 1 tile = 1 u. O Cyborg tem 1,26 u de altura, contra 1,375 t da Madeline (0,92×). Por isso as constantes do Celeste servem quase 1:1 em u.
   - O Cyborg é **metade** da largura da Madeline (0,51 contra 1 t). As tolerâncias proporcionais ao corpo (correção de quina, distância do wall jump) caem para a metade ou um pouco mais: 0,25 u e 0,3 u.
2. **Velocidade (M01).** 10 u/s = 7,9 alt/s, praticamente igual ao Celeste em alturas da Madeline (8,2). É também a velocidade atual. A tela visível tem metade dos tiles do Celeste (M32), então correr mais rápido pioraria a leitura.
3. **Aceleração (M02–M07).** ~0,1 s até o máximo, como o Celeste (0,09 s) e perto do "instant-on" do SMB. Ainda sobram ~6 ticks de rampa, o que deixa o personagem "humano" (GF §2.1b). Isso resolve o "sabonete" (0,66 s para virar) e o overshoot em plataformas de 1–2 u: parar ocupa 0,5 u, ou seja, 1 largura.
4. **Pulo de 3,5 u (M10).**
   - As fases têm muitos degraus de 2 u e vários de 3 u (AUD §5.2). 3,5 u dá 0,5 u de margem sobre o degrau alto mais comum, e a AUD §9.1-6 pede pelo menos ~2,7 u de altura útil.
   - É o próprio Celeste convertido (3,56 u) e fica em ~2,8 alt, dentro da faixa "~3× a altura do corpo" que o GMTK descreve (GF §2.1b).
   - **Não** vale subir para 4–6 u só para dispensar a edição das fases (opção A da §10): 6 u = 4,8 alt, o que torna os degraus de 1–3 u triviais e piora a leitura numa tela de 11,25 u.
   - Faixa de 0,9 a 3,5 u com altura monotônica. Isso corrige os P01–P03.
5. **Tempo de ar (M13–M14).** Ápice em 0,35 s e ~0,68 s no ar, como o Celeste. O pulo atual é **mais lento** (0,82 s) e sobe como elevador. O novo é mais curto e mais legível, e isso favorece o speedrun.
6. **Distância (M15).** ~7,1 u com o impulso de 4 u/s. É menor que os 8,6 u de hoje, mas o maior vão obrigatório é de 3–4 u (AUD §5.3): quase 2× de margem. A AUD §9.1-6 pede cautela abaixo de ~8 u. **A reexecução do modelo de alcançabilidade com o perfil novo é critério de pronto** (§10.3).
7. **Queda (M16–M18).** 17 u/s fica entre a queda de hoje (15,2) e a do Celeste (20), porque a nossa tela mostra metade da altura em tiles. O fast fall (24 u/s) é opcional e parte do jogador.
8. **Janelas (M19–M20).** Coyote e buffer de 0,1 s são os valores atuais, que vale reaproveitar (AUD §9.2), e batem com o Celeste. Um buffer maior geraria ações indesejadas (GF §10, item 2), por isso **ele não é stat de upgrade**.
9. **Parede (M22–M25).**
   - A velocidade horizontal do wall jump é `MaxSpeed + impulso` (14 u/s), a mesma regra do Celeste.
   - Atravessar a chaminé de 3 u (2,49 u livres) leva ~0,18 s, praticamente a duração da trava (0,16 s). Por isso o *neutral jump* e o zigue-zague sobem a chaminé sem trava artificial.
10. **Pulo aéreo de 3,0 u (M26–M27).** 85 % do pulo do chão mantém o pulo do chão como o principal. O alcance total de ~6,5 u cobre o obstáculo de ~6 u da Quarta com margem: depois da edição B (§10), a rota original vira **atalho** de Pulo Duplo.
11. **Dash (M28–M29).** O Celeste percorre 4,5 t = 3,3 alt da Madeline; o alvo percorre 4 u = 3,2 alt. A velocidade de saída é 1,7× `MaxSpeed` (no Celeste, 1,78×).
12. **Gancho (M30).** A gravidade quase triplica (de 38 para ~110 u/s²). Se o Δv de 25 u/s fosse mantido, a subida cairia de 5,1 para ~2,8 u (deriv.). Por isso a meta é o **resultado** (+5 u), e o Δv fica em ~33 u/s (deriv.). O upgrade "+40 %" passa a funcionar na vertical, algo que o clamp atual impede (AUD §8.4).
13. **Câmera (M32–M33).** 20 × 11,25 u (ortho 5,625) equivale a 640 × 360 px a 32 PPU, com escala inteira ×3 em 1080p. Mesmo assim é metade da área do Celeste, então look-ahead e look-down são **P0**.

### 7.2 Constantes de partida para o SPEC (não normativas)
Simuladas a 60 Hz no modelo do Celeste. Atingem M10, M11, M13 e M14 dentro das tolerâncias.

| Grupo | Constantes |
|---|---|
| Corrida | `MaxSpeed` 10 u/s · aceleração/freio/virada no chão 100 u/s² · multiplicador no ar 0,65 · freio de sobrevelocidade 40 u/s² |
| Pulo | velocidade de pulo 13,5 u/s · hold 0,2 s · gravidade ~110 u/s² · limiar da meia gravidade 5 u/s · impulso horizontal 4 u/s · velocidade do pulo aéreo ~12 u/s |
| Queda | queda máxima 17 u/s · fast fall 24 u/s |
| Janelas | coyote 0,1 s · buffer 0,1 s · tolerância de teto 0,05 s · retenção de velocidade 0,06 s · wall coyote 0,1 s |
| Tolerâncias espaciais | correção de quina 0,25 u · distância do wall jump 0,3 u |
| Parede | wall jump (14; 13,5) · trava 0,16 s · wall slide 2,5 u/s |
| Dash | 27 u/s · 0,15 s · saída 17 u/s · fator vertical 0,75 · cooldown 0,2 s · recarga 0,1 s · freeze 0,05 s |
| Gancho | raio 9 u · puxão 20 u/s · lançamento ~33 u/s · cooldown 0,5 s |

### 7.3 Métricas derivadas para o level design (LDtk)

| Kit | Subida máxima por pulo (projeto) | Vão máximo recomendado | Pé-direito para o pulo máximo |
|---|---|---|---|
| Base | **3 u** (0,5 u de margem sobre M10) | **5 u** (≥ 2 u de margem sobre M15) | ≥ 4,8 u (3,5 + 1,26); corredores mais baixos cortam o pulo |
| + Pulo Duplo | 6 u | 9 u | — |
| + Salto de Parede | chaminés de 2–4 u de largura, altura livre | — | — |

---

## 8. Requisitos não funcionais

| ID | Requisito | Prio | Critério de aceitação |
|---|---|---|---|
| RNF-01 | **Independência de framerate.** A trajetória depende só dos ticks e do input por tick, nunca do framerate de render nem do VSync | P0 | A mesma gravação de input a 30, 60, 144 e 240 fps gera posições idênticas no tick N |
| RNF-02 | **Determinismo.** Mesmo estado inicial + mesma sequência de input por tick ⇒ mesmo estado, bit a bit, no mesmo build e plataforma. Sem `Random`, sem `Time.deltaTime` e sem leitura de pose interpolada na lógica. É pré-requisito de ghosts e replays e da justiça do timer | P0 | Teste EditMode roda 2× uma gravação de 10 000 ticks e compara o hash do estado |
| RNF-03 | **Latência.** A lógica do jogo adiciona no máximo 1 tick entre o input processado e a aplicação da ação, e o feedback aparece no mesmo frame da aplicação. Total ≤ 2 frames a 60 fps (≤ 33 ms), com meta de 1 frame (M34) | P0 | Log instrumentado com o índice do frame do aperto e do primeiro frame com mudança visual, em 100 amostras |
| RNF-04 | **Tempo de fase em ticks de simulação.** O `LevelTimer` conta ticks × duração do tick. **Freeze frames não contam** (a simulação não avança). Pausa, menus e loading não contam. Respawn de queda conta, porque é punição. Nenhum freeze pode ser implementado alterando `Time.timeScale`, que hoje também desacelera um timer baseado em `Time.deltaTime` (GF §10.2) | P0 | Uma run gravada com o freeze ligado e com ele desligado termina com o **mesmo** tempo. Câmera lenta não altera o tempo medido |
| RNF-05 | **Tuning por dados.** Todo número de feel fica num perfil em ScriptableObject (base do `PlayerBaseStats` do PLANO 2.1), e os upgrades o modificam via `PlayerStats`. Zero número mágico no código e edição em Play Mode com efeito imediato | P0 | Revisão de código. Alterar o SO em Play Mode muda o movimento sem recompilar |
| RNF-06 | **Testabilidade.** A lógica do movimento é C# puro, testável em EditMode sem cena. Há testes de metas (M01–M30), de invariantes (RF-11, RF-38, RF-39) e de determinismo | P0 | Suíte EditMode verde via `TestRunnerBridge` (PLANO 0.4) |
| RNF-07 | **Compatibilidade com `PlayerStats`.** O movimento lê um snapshot de stats e flags e aplica `PlayerStatsChanged` no próximo tick sem interromper a ação em curso. Os valores base são os do perfil novo (os testes da 2.1 congelam o §7) | P0 | Teste: mudar stats no meio de um pulo só afeta o próximo pulo, exceto `MaxSpeed`, que vale imediatamente |
| RNF-08 | **Contratos preservados.** Tag e layer `Player`, GUID e fileID do `Cyborg.prefab`, `Die()` idempotente até a migração para eventos, e as layers 6 e 8 (AUD §9.1, §9.3) | P0 | EndGoal, EnemyBullet, Timer/LevelTimer, CameraFollow e Minimap funcionam sem alteração além da migração prevista |
| RNF-09 | **Performance.** 0 B de GC por tick depois do aquecimento, controlador ≤ 0,1 ms por tick em média no PC de desenvolvimento, e número de consultas de física por tick limitado e documentado no SPEC | P1 | Profiler numa fase completa |
| RNF-10 | **Observabilidade.** Logs no formato `[Movement] - …`, temporários com `// [DEBUG]` (PLANO §7), overlay (RF-68) e relatório de telemetria por fase (§9.3) | P1 | CSV de playtest com os campos da §11 |
| RNF-11 | **Robustez do tick.** Se o SPEC mantiver 50 Hz, todas as metas em segundos continuam valendo, com tolerância de 1 tick | P0 | Suíte de metas rodando no tick escolhido |
| RNF-12 | **Pixel art estável.** Nada de shimmer do sprite em movimento: posição renderizada alinhada à grade de pixels | P2 | Depende da decisão de arte (Q8) |

---

## 9. Integração com o roguelike

### 9.1 `StatType` (revisão da lista do PLANO §3.5)

| StatType | Proposta | Base | Teto | Observação |
|---|---|---|---|---|
| `MaxSpeed` | manter | 10 u/s | +30 % (13 u/s) | "Passos Leves" ×3 ≈ +25 % |
| `Acceleration` | manter, mas **cobre aceleração, freio e virada no chão** | 100 u/s² | +30 % | Com a base em 0,1 s, +15 % dá ~13 ms, imperceptível. **Trocar o upgrade "Arranque"** (§9.4) |
| `AirAcceleration` | **renomear para `AirControl`**: multiplica todas as taxas no ar (aceleração, freio, virada) | ×1 | +50 % | Hoje o "Controle Aéreo" só mexe na aceleração no mesmo sentido (AUD §8.4) |
| `JumpHeight` | manter. **Muda só a altura; gravidade e queda ficam fixas** | 3,5 u | +20 % (4,2 u) | O pulo aéreo acompanha (85 %). Corrige P22 |
| `CoyoteTime` | manter | 0,1 s | 0,2 s | É ajuda, não verbo; por isso o teto baixo |
| `WallSlideSpeed` | manter | 2,5 u/s | mínimo 1 u/s | "Deslize Lento" |
| `MaxAirJumps` | manter | 0 | 2 | Pulo Duplo e Triplo |
| `GrappleRadius` / `GrappleCooldown` | manter | 9 u / 0,5 s | 13,5 u / mínimo 0,2 s | — |
| `GrappleLaunchForce` | **renomear para `GrappleLaunchSpeed`** (Δv em u/s, sem clamp) | ~33 u/s | +40 % | Hoje o "+40 %" não muda nada na vertical (AUD §8.4) |
| `TimeLimitBonus` | manter (não é de movimento) | 0 | — | Exige o timer em ticks (RNF-04) |
| **`MaxDashes`** (novo) | Dash = `MaxDashes ≥ 1`, na mesma lógica do pulo duplo | 0 | 2 | Sem flag separada |
| **`JumpHorizontalBoost`** (novo) | impulso horizontal do pulo | 4 u/s | 6 u/s | Upgrade "Impulso" (pilar F4) |
| **`OverspeedDecay`** (novo) | freio da sobrevelocidade | 40 u/s² | mínimo 20 u/s² | Upgrade "Embalo" (pilar F4) |

**Deliberadamente fora de `StatType`** (invariantes de feel, pilar F6): gravidade, queda máxima, buffer, trava de input, correção de quina, freeze e parâmetros de câmera. Um upgrade que mexe nesses valores quebra a previsibilidade: o jogador reaprende o controle a cada fase. Todo stat tem teto aplicado pelo `PlayerStats`.

### 9.2 `AbilityFlags`

| Flag | Proposta |
|---|---|
| `WallGrab` | **Renomear para `WallJump`** (libera deslizar + pular). O nome `WallGrab` fica reservado para uma futura escalada com estamina |
| `GrapplingHook` | manter |
| `DashRefillOnWallJump` (nova) | o wall jump recarrega o dash (upgrade "Recarga na Parede") |
| `AirJumpRefillOnGrapple` (nova) | prender o gancho recarrega os pulos aéreos (upgrade "Âncora") |

### 9.3 Eventos (convenção do PLANO §3.3: `readonly struct`, nome no passado)
Os eventos são emitidos no tick em que o fato acontece. Os ouvintes de feedback reagem no mesmo frame (RF-53). Não pode haver alocação por evento.

| Evento | Payload | Quando | Ouvintes |
|---|---|---|---|
| `PlayerJumped` | tipo (Chão, Coyote, Aéreo), veio do buffer (bool), posição, velocidade | tick em que o pulo é aplicado | feedback, animação, telemetria |
| `PlayerWallJumped` | lado, neutro (bool), posição | idem | feedback, telemetria |
| `PlayerLanded` | velocidade de impacto, tempo no ar, posição | 1º tick no chão | squash, poeira, rumble, câmera (snap em Y), telemetria |
| `PlayerDashed` | direção, cargas restantes, posição | fim do freeze (direção fixada) | feedback, shake, telemetria |
| `PlayerWallSlideChanged` | começou (bool), lado | ao entrar ou sair do slide | partículas, som em loop, animação |
| `PlayerAbilityRefilled` | recurso (Pulo aéreo, Dash), quantidade | ao recarregar | indicador no corpo, som |
| `PlayerActionDenied` | ação (Dash, Pulo aéreo, Gancho), motivo (sem carga, cooldown, travado) | aperto sem recurso | som de falha, telemetria |
| `PlayerGrappleFired` / `PlayerGrappleAttached` / `PlayerGrappleReleased` | alvo; velocidade de lançamento | cada transição do gancho | feedback, câmera, telemetria |
| `PlayerFellOut` | posição, último chão seguro | saída pelos limites de baixo | respawn, telemetria (o `LevelTimer` **não** para) |
| `PlayerRespawned` | posição, motivo (queda, voltar ao spawn) | controle devolvido | câmera (corte), feedback |
| `PlayerDied` (já no PLANO) | **causa** (Inimigo, Tempo, …), **posição** | morte (RF-41) | RunManager, LevelTimer |
| `MovementStatsReported` | contadores da fase: pulos do buffer, buffers expirados, pulos de coyote, correções de quina, ações negadas, quedas | junto com `LevelGoalReached` ou `PlayerDied` | telemetria (PLANO 4.3) |

O movimento **consome** `PlayerStatsChanged`, `LevelTimeExpired` (que trava o controle), `LevelStarted` e `PlayerSpawned`. Cada evento novo entra no catálogo do PLANO §3.4 no mesmo PR (PLANO §7).

### 9.4 Upgrades de movimento (revisão da tabela do PLANO §3.5)

| Raridade | Upgrade | Efeito | Stack | Requer | Mudança |
|---|---|---|---|---|---|
| Comum | Passos Leves | +8 % `MaxSpeed` | 3 | — | manter |
| Comum | Mola | +8 % `JumpHeight` (a queda não fica mais pesada) | 2 | — | manter |
| Comum | Controle Aéreo | +20 % `AirControl` | 2 | — | agora mexe em todas as taxas no ar |
| Comum | **Embalo** | −30 % `OverspeedDecay` | 2 | — | **substitui "Arranque"**, que seria imperceptível |
| Comum | **Impulso** | +1 u/s `JumpHorizontalBoost` | 2 | — | novo |
| Raro | Coyote Estendido | +0,08 s `CoyoteTime` | 1 | — | manter (teto de 0,2 s) |
| Raro | Deslize Lento | −40 % `WallSlideSpeed` | 1 | Salto de Parede | a dependência muda de nome |
| Raro | Gancho Rápido / Alcance do Gancho | −30 % cooldown / +25 % raio | 2 / 2 | Gancho | manter |
| Raro | **Recarga na Parede** | flag `DashRefillOnWallJump` | 1 | Dash | novo |
| Raro | **Âncora** | flag `AirJumpRefillOnGrapple` | 1 | Gancho | novo |
| Épico | Pulo Duplo | +1 `MaxAirJumps` | 1 | — | manter |
| Épico | **Salto de Parede** | flag `WallJump` | 1 | — | renomeia "Wall Grab" |
| Épico | Gancho | flag `GrapplingHook` | 1 | — | manter |
| Épico | **Dash** | +1 `MaxDashes` | 1 | — | novo (P1) |
| Lendário | Pulo Triplo | +1 `MaxAirJumps` | 1 | Pulo Duplo | manter |
| Lendário | Impulso do Gancho | +40 % `GrappleLaunchSpeed` | 1 | Gancho | agora funciona na vertical |
| Lendário | **Dash Duplo** | +1 `MaxDashes` | 1 | Dash | novo |
| Lendário | Relógio de Bolso | +5 s `TimeLimitBonus` | 1 | — | manter |

Regra de design para upgrades futuros: um upgrade **acrescenta um verbo, uma rota ou uma recarga, ou amplia um número dentro do teto**. Nunca mexe nos invariantes do §9.1. Trade-offs negativos (ex.: "+velocidade, −controle") ficam fora do MVP.

---

## 10. Impacto no level design e na decisão D1

### 10.1 O conflito
A D1 recomenda "toda fase completável com o kit base". Pelo modelo de alcançabilidade (AUD §5.3), com o controlador **atual**:
- a Quinta para em y ≈ 30–32 (o goal está em 51) e exigiria um pulo base de ≈ 4 u;
- a Quarta não sai do spawn e exigiria ≈ 6 u. A única rota encontrada é **wall jump → pulo aéreo recarregado**.

O gargalo é **vertical**, não horizontal. O gancho não é necessário em nenhuma fase.

### 10.2 Opções

| Opção | O que muda | Prós | Contras |
|---|---|---|---|
| **A: kit base mais alto** | Pulo base de ≈ 4 u (Quinta) ou ≈ 6 u (Quarta) | Nenhuma edição de fase | 6 u = 4,8 alt muda o caráter do jogo: degraus de 1–3 u ficam triviais, Pulo Duplo e Mola perdem valor e a tela de 11,25 u fica apertada. 4 u sozinho não resolve a Quarta |
| **B: ajustar a geometria** | Quinta: um degrau intermediário no trecho que exige mais de 3,5 u. Quarta: uma saída do spawn com subidas ≤ 3 u e plataformas intermediárias (≤ 3 u de espaçamento) na sala de plataformas a 9–11 u umas das outras. As rotas atuais ficam como **atalhos** de Pulo Duplo e Salto de Parede | Preserva o feel do §7. Deixa D1 verdadeira por construção e torna visível a regra "habilidade = mais rápido" | Exige edição no LDtk e nas cenas (a Quarta tem histórico de conflitos YAML, PLANO 0.2 e 0.5) e reexecução da alcançabilidade |
| **C: mobilidade garantida nas ofertas** | Forçar Pulo Duplo (e Salto de Parede) nas 1ª e 2ª ofertas | Nenhum trabalho de fase | Tira a escolha: um slot forçado não é escolha. As duas primeiras ofertas ficariam amarradas. Quebra com qualquer reordenação de fases (D4). Contradiz a própria D1 |

### 10.3 Recomendação: **B + pulo base de 3,5 u** (C só como contingência)
1. Adotar o pulo de 3,5 u do §7. Ele já é necessário por outras razões (degraus de 3 u e referência do Celeste) e reduz a quantidade de edição.
2. Aplicar a opção **B** só nos pontos que o modelo de alcançabilidade apontar, **reexecutado com o perfil novo**, seguindo as métricas do §7.3. Manter as rotas antigas como atalhos das habilidades.
3. **Critério de pronto da D1:**
   - (a) o modelo de alcançabilidade dá ✅ nas 3 fases com o kit base;
   - (b) 3 pessoas completam cada fase com o kit base dentro do limite de D6;
   - (c) cada habilidade Épica encurta em **≥ 10 %** o tempo da melhor rota do kit base em pelo menos uma fase. É isso que torna verdade o "habilidades só deixam mais rápido".
4. **Contingência:** se a edição B atrasar, liga-se uma flag no `RunConfig` que garante uma habilidade de mobilidade na 1ª oferta (opção C, provisória), a ser removida quando B terminar.
5. Os limites e tempos-alvo da D6 (20/30/25 s) **precisam ser remedidos** com o controlador novo. O pulo mais curto e a queda mais rápida mudam os tempos.

---

## 11. Métricas de sucesso

### 11.1 Playtest (Etapa 4.4 e playtests de movimento)

| Métrica | Como medir | Meta |
|---|---|---|
| Questionário de feel (Likert 1–7): responsividade, justiça, controle no ar, previsibilidade do pouso, câmera, vontade de tentar de novo, "sei se tenho carga" | Formulário depois de 3 runs | Média ≥ 5,5 e nenhum item < 4,5. **A/B com o controlador atual:** ≥ +1,5 ponto em responsividade e em justiça |
| Mortes ou quedas percebidas como injustas | O jogador marca "injusta" após cada falha (build de playtest) ou o observador anota | ≤ 10 % das falhas |
| Relatos espontâneos de "comeu meu pulo", "fiquei preso" ou "flutuei" | Observador (think-aloud) | 0 |
| Onboarding | Tentativas até completar a Primeira fase dentro do limite | ≥ 80 % dos novatos em ≤ 3 tentativas |
| Aprendizado | Mediana e coeficiente de variação do tempo por fase, da 1ª à 5ª tentativa | Mediana melhora ≥ 15 % e a variação cai |
| Valor das habilidades | Taxa de escolha de cada Épico quando ofertado, e uso por fase depois de adquirido | Nenhum Épico de movimento com escolha < 20 % |

### 11.2 Telemetria automática (CSV do PLANO 4.3, estendido por `MovementStatsReported`)

| Métrica | Definição | Meta |
|---|---|---|
| **Inputs "comidos"** | Apertos de pulo que expiraram do buffer e, ≤ 2 ticks depois, o jogador passou a poder pular | < 1 % dos apertos |
| Uso do buffer e do coyote | % de pulos vindos do buffer e do coyote | Informativo: confirma que as ajudas atuam (esperado > 0) |
| Correções de quina por minuto | Contador | Informativo. Um valor alto indica geometria apertada demais |
| Soft-locks | Tempo esgotado com o jogador sem progresso por > 3 s | 0 |
| Quedas por fase e causas de morte | Contadores e `PlayerDied.causa` | Base para a D6 e para o level design |
| Ações negadas por minuto | `PlayerActionDenied` | Cai ao longo das tentativas (sinal de recurso legível) |
| Distribuição de notas S/A/B/C por fase | `LevelCompleted` | Calibra a D6 e as curvas de raridade |

### 11.3 Técnicas (gate de merge)
100 % dos testes de metas (M01–M30), invariantes e determinismo verdes; 0 B de GC por tick; latência medida ≤ 2 frames; todos os P0 atendidos.

---

## 12. Riscos, dependências, perguntas em aberto e glossário

### 12.1 Riscos e mitigação

| Risco | Impacto | Mitigação |
|---|---|---|
| O controlador novo muda os tempos das fases e a D6 fica obsoleta | Limites errados distorcem a raridade | Remedir a D6 depois da troca (§10.3-5), com telemetria (§11.2) |
| Rotas quebradas ou atalhos não previstos com o pulo novo (inclusive na Primeira) | Fase impossível ou trivial | Reexecutar a alcançabilidade (§10.3) + playtest com o kit base, usando as métricas do §7.3 |
| Corpo cinemático deixa de gerar triggers e contatos (EndGoal, moedas, balas, inimigos) | Fase sem fim, balas sem efeito | Requisito explícito no SPEC. Teste de cena com cada trigger (RNF-08) |
| Perda de valores serializados ou GUID ao trocar o script (AUD §9.3) | Prefab quebrado nas cenas | Estratégia de migração do AUD §9.3 e prefab editado num commit isolado |
| Escopo grande (controlador + câmera + feedback + input + dash) | A etapa não cabe na janela | Corte por prioridade: P0 primeiro, Dash e feedback P1 depois, P2 fora |
| Arte faltando (dash, frame de slide, morte) | Leitura pior | Placeholders e efeitos por código (squash, afterimage, partículas). Arte nova é melhoria |
| Mudar o tick para 60 Hz afeta inimigos e balas | Comportamento diferente | Revisar `EnemyAI`/`EnemyBullet` no SPEC, ou simular só o jogador no tick próprio (Q1) |
| Upgrades empilhados descaracterizam o controle | Controle imprevisível no fim da run | Tetos por stat (§9.1) e invariantes fora de `StatType` |
| Shimmer da pixel art (escala 1,7; 64 × 32 PPU) | Visual "tremendo" | Q8. RNF-12 como P2 |
| Conflito com o PLANO (2.1/2.4 pedem "sensação idêntica") | Retrabalho e testes contraditórios | Atualizar o texto das tarefas (§4.1) antes da Etapa 2 |

### 12.2 Dependências
1. **Etapa 1 do PLANO** (Event Bus e catálogo de eventos), para os eventos do §9.3. Até lá, os contratos atuais continuam valendo (RNF-08).
2. **Tarefa 2.1** (`PlayerStats`, `PlayerBaseStats`), com o texto ajustado para congelar o perfil novo.
3. **Decisões D1 e D3** do time. D1 segue a recomendação da §10; a D3 precisa aceitar a regra "queda ≠ morte" (Q3).
4. **Level design no LDtk e nas cenas** para a opção B. Depende da limpeza da `QuartaFase` (PLANO 0.2) e do Smart Merge (0.5).
5. **`LevelTimer` (PLANO 3.1)** contando ticks (RNF-04).
6. **Áudio:** hoje não existem SFX de movimento (AUD P24). É preciso alguém para produzir ou curar os sons.
7. **"Play direto na fase"** (AUD bug 13) para a iteração de tuning (RF-68).
8. Input System 1.14.2 (já instalado) e gamepads para teste.

### 12.3 Perguntas em aberto (cada uma com recomendação padrão)
As marcadas com **★** são as que mais impactam o SPEC.

| # | Pergunta | Recomendação padrão |
|---|---|---|
| Q1 ★ | Tick de 60 Hz ou manter 50 Hz? Só o jogador ou o projeto inteiro? | 60 Hz. Paridade com as referências e janelas em ticks inteiros (0,1 s = 6 ticks) (IMP §11.3). Se afetar inimigos, simular o jogador num tick próprio |
| Q2 ★ | Controlador cinemático próprio, dinâmico com velocidade sobrescrita, ou `Rigidbody2D.Slide`? | Cinemático próprio, com resolução por eixo (IMP §11.1 opção A). O RF-02 exige números da engine fora do caminho |
| Q3 ★ | Queda para fora da fase: respawn com custo de tempo ou fim da run (D3)? | Respawn no último chão seguro (RF-42). A D3 continua valendo para inimigos e tempo. Cair é falha de plataforma, e o tempo já pune |
| Q4 ★ | O freeze conta no timer? | Não (RNF-04). Freezes com duração fixa em ticks |
| Q5 | O Dash entra no MVP da run? | Sim, como P1 e Épico. Se cortado, os outros três Épicos cobrem o MVP |
| Q6 ★ | Paredes invisíveis de limite: contam como parede ou teto? | Bloqueiam e contam como teto, mas sem wall slide ou jump (RF-22). A longo prazo, trocar por geometria visível |
| Q7 | Câmera própria ou Cinemachine 3? | Evoluir o `CameraFollow` próprio (GF §10.2): regras específicas do gênero e determinismo |
| Q8 | Pixel perfect com referência 640 × 360 (ortho 5,625)? E o Cyborg a PPU 64 × 1,7? | Adotar a área de 20 × 11,25 u agora (RF-50). Pixel snap (RNF-12) só depois de decidir se o sprite será reexportado para bater com os 32 PPU dos tiles |
| Q9 | Inimigos bloqueiam o jogador? Dá para ficar em pé neles? | Continuam sólidos e servem de chão, mas não de parede para wall jump. O dano continua vindo só das balas (comportamento atual, AUD §1.8) |
| Q10 | Altura do pulo aéreo: 85 % ou 100 % do pulo do chão? | 85 % (3,0 u), para manter o pulo do chão como principal e limitar o alcance vertical |
| Q11 | "Voltar ao spawn" zera o timer? | Não (RF-43). Zerar permitiria repetir a fase até a nota S e quebrar a raridade |
| Q12 | O wall jump recarrega o dash no kit? | Não. Vira upgrade ("Recarga na Parede") |
| Q13 | Input horizontal digital ou analógico? | Digital, como Celeste e SMB (IMP §2.6, §6): resposta legível e consistente entre teclado e gamepad |
| Q14 | O impulso horizontal do pulo (bunnyhop) entra no kit base? | Sim, com 4 u/s (pilar F4). A distância do pulo fica em ≥ 7 u |
| Q15 | Onde o refactor entra no PLANO? | Numa etapa nova depois da Etapa 1, fechando antes da 2.4. A 2.4 vira "ligar o controlador novo ao `PlayerStats`" |

### 12.4 Glossário

| Termo | Definição |
|---|---|
| **alt / larg** | Unidades relativas ao Cyborg: 1 alt = 1,26 u (altura do colisor); 1 larg = 0,51 u |
| **Ápice (apex)** | Topo do arco do pulo, onde `vy` passa por zero |
| **Meia gravidade no ápice** | Gravidade ×0,5 quando `|vy|` é pequeno e o botão continua pressionado. Dá mais tempo para mirar o pouso |
| **Pulo variável (hold)** | A velocidade de subida é mantida enquanto o botão está pressionado, até um limite de tempo. Soltar encerra o hold sem zerar a velocidade |
| **Impulso horizontal do pulo** | Velocidade somada na direção do input ao pular (`JumpHBoost` do Celeste) |
| **Coyote time** | Janela para pular depois de sair da borda sem pular |
| **Buffer de input** | Janela em que um aperto feito cedo demais fica guardado e é executado quando a ação fica possível. É consumido uma única vez |
| **Correção de quina** | Deslocamento lateral pequeno e automático quando a cabeça, ou o corpo no dash, bate numa quina, para o movimento continuar |
| **Tolerância de teto** | Bater a cabeça logo no início do pulo não cancela o hold |
| **Retenção de velocidade** | A velocidade horizontal volta se a parede em que o jogador bateu deixar de bloquear logo em seguida |
| **Fast fall** | Queda mais rápida ao segurar ↓ |
| **Wall slide / wall jump** | Deslizar devagar numa parede e pular a partir dela |
| **Neutral jump** | Wall jump sem direção pressionada: não trava o input e o personagem volta para a parede |
| **Wall coyote** | Coyote time aplicado ao wall jump depois de desencostar da parede |
| **Sobrevelocidade / teto macio** | Velocidade acima do máximo que decai devagar enquanto o jogador segura a direção (Overmax do SpeedRunners) |
| **Dash / super** | Arrancada curta em 8 direções. O super é um pulo dado durante o dash horizontal com coyote ativo |
| **Freeze frame / hitstop** | Pausa curta da simulação para dar ênfase. Aqui é fixa em ticks e fica fora do timer |
| **Squash & stretch** | Deformação só do sprite (nunca do colisor) para comunicar impulso e impacto |
| **Look-ahead / look-down** | A câmera se desloca para a direção do movimento ou da queda |
| **Platform snapping** | A câmera só ajusta Y ao pousar em outra altura, sem seguir cada pulo |
| **Tick** | Um passo fixo da simulação do jogador |
| **Determinismo** | Mesmo estado + mesma sequência de input ⇒ mesmo resultado. Base de ghosts, replays e justiça do timer |
| **Kit base** | O que o jogador tem no início de toda run, sem upgrades |
| **Último chão seguro** | A posição registrada para o respawn depois de uma queda. Nunca fica à beira de um vão |

---

## 13. Referências (seções dos documentos de pesquisa)
- **Problema e estado atual:** AUD Resumo executivo, §6 (P01–P25), §7 (métricas), §5.3 (alcançabilidade), §8.4 (StatType × código), §9 (contratos e riscos de GUID).
- **Modelo de movimento:**
  - Celeste: corrida IMP §2.6, pulo §2.7, queda §2.8, paredes §2.9, dash §2.10, técnicas §2.11, ajudas §2.12;
  - constantes convertidas IMP §10;
  - tabela comparativa IMP §9;
  - HK IMP §4, Silksong §5, SMB §6, SpeedRunners §7;
  - Pittman e GMTK IMP §8.
- **Arquitetura e tick:** IMP §11.1 (três caminhos), §11.2 (input × simulação), §11.3 (tick), §11.5–11.6 (máquina de estados e ordem do tick).
- **Técnicas de speedrun:** IMP §12.
- **Feel e feedback:** GF §1 (princípios), §2.2 (squash), §2.4 (partículas), §2.5 (recurso no corpo), §2.6 (freeze), §2.7 e §8 (câmera), §2.9 (rumble), §2.10 (forgiveness), §2.11 e §9 (morte e respawn), §10 (catálogo priorizado), §10.2 (Unity: timer × freeze, Pixel Perfect, rumble), §11 (anti-padrões).
- **Plano do roguelike:** PLANO §2 (D1–D7), §3.3–3.5 (Event Bus, eventos, upgrades), Etapa 2 (tarefas 2.1 e 2.4).
