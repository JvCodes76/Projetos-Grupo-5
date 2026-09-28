# Relatório de bugs — Projetos Grupo 5

Scan feito em 27/09/2026 via Unity MCP (Editor 6000.1.7f1) + leitura de todos os scripts em `Assets/scripts`.
O que foi checado: console, Build Settings, todas as cenas e prefabs (scripts faltando, referências quebradas, referências para assets) e a lógica dos scripts.

Severidade: 🔴 quebra o jogo/dados · 🟠 comportamento errado visível · 🟡 menor / risco futuro

---

## 🔴 Críticos

### 1. Timer e Game Over apontam para o **prefab** do Cyborg, não para o jogador da cena
Nas fases do build (`PrimeiraFase`, `QuartaFase 1`, `QuintaFase`) não existe Cyborg na cena — ele é instanciado pelo `SceneController`. Mesmo assim, no Inspector:

- `Canvas/Timer.playerData` → `Assets/Prefabs/Cyborg.prefab`
- `Canvas/Timer.characterMovement` → `Assets/Prefabs/Cyborg.prefab`
- `GameOverBackground/GameOver_Script.playerData` → `Assets/Prefabs/Cyborg.prefab`

Consequências:
- **`Timer.Start()` salva os dados do prefab no PlayerPrefs** ([Timer.cs:47-51](Assets/scripts/Timer.cs:47)). O prefab tem `coinCount=74` serializado, então a cada fase o save de moedas é sobrescrito com os valores do prefab. (E, no Editor, isso altera o próprio asset do prefab — é por isso que ele está com `coinCount=74` e `currentLevel=2`.)
- **Quando o tempo acaba, `Die()` é chamado no prefab**, não no jogador ([Timer.cs:104-107](Assets/scripts/Timer.cs:104)) — a tela de Game Over aparece, mas o jogador real continua se movendo.
- **Botão "Menu" do Game Over tenta `Destroy` num asset** ([GameOver_Script.cs:26-29](Assets/scripts/GameOver_Script.cs:26)) → erro "Destroying assets is not permitted".

**Correção:** limpar esses campos no Inspector e buscar o jogador em runtime (ex.: assinar `SceneController.OnPlayerSpawned` ou `FindFirstObjectByType` quando o player já existir).

### 2. Tela final sempre mostra `MOEDAS: 0` e `TEMPO TOTAL: 00:00`
- O `PlayerData` do menu (`MainMenu/PlayerDataObject`) **não é `DontDestroyOnLoad`** — morre ao trocar de cena.
- `SceneController.FindPlayerData()` roda em `OnSceneLoaded` *antes* do player ser spawnado ([SceneController.cs:49-54](Assets/scripts/SceneController.cs:49)), então `playerData` fica nulo durante as fases.
- Em `NextLevel()` o tempo só é somado se `playerData != null` ([SceneController.cs:147](Assets/scripts/SceneController.cs:147)) → **o tempo nunca é acumulado** (PlayerPrefs atual: `TotalTimePlayed = 0`).
- A cena `EndGame` não tem nenhum `PlayerData`, então [GameEndScreen.cs:61](Assets/scripts/GameEndScreen.cs:61) cai no ramo de erro e mostra zeros.

**Correção:** ter **um único** `PlayerData` persistente (DDOL, ex.: no mesmo objeto do `SceneController`) e remover o `PlayerData` do prefab do Cyborg — ou ler os valores direto do PlayerPrefs na tela final.

### 3. Três "PlayerData" diferentes competindo
Existem `PlayerData` no MainMenu, no prefab do Cyborg e em `playerData.prefab`. Cada script pega um diferente via `FindObjectOfType`, cada um salva por cima do outro no PlayerPrefs. É a raiz dos bugs 1 e 2. Além disso, `SceneController` usa `GetComponent<PlayerData>() == null` para distinguir "jogador físico" do "objeto de dados" ([SceneController.cs:89](Assets/scripts/SceneController.cs:89), [:136](Assets/scripts/SceneController.cs:136)) — como o Cyborg **tem** `PlayerData`, essa lógica nunca reconhece nem destrói o jogador.

### 4. Fases fora do build / cenas quebradas
Build Settings: `MainMenu(0) · SettingsMenu(1) · PrimeiraFase(2) · QuartaFase 1(3) · QuintaFase(4) · EndGame(5)`; `SextaFase` e `SegundaFase` desativadas.
- **`TerceiraFase` (a única cena com a Loja) não está no build** → a loja é inalcançável no jogo final.
- `TerceiraFase` e `SextaFase` têm **prefab faltando**: `GameController (Missing Prefab guid a9bef88efba54c843b8f74273407fad2)`.
- `SceneController.gameLevelIndexes = [2,3,4,5,6,7]` — os índices 6 e 7 não existem e o 5 é a `EndGame`. Funciona por acaso (após a Quinta ele carrega o índice 5), mas qualquer mudança na ordem do build quebra o fluxo e o "Continuar".

---

## 🟠 Comportamento errado

### 5. `DisableMovement()` não faz nada; jogador se move depois de morrer
`canMove` é setado mas nunca lido ([characterMovement.cs:81](Assets/scripts/characterMovement.cs:81), [:537-544](Assets/scripts/characterMovement.cs:537)) — o próprio compilador avisa (CS0414). `Die()` só zera a velocidade uma vez; o jogador continua andando/pulando com a tela de Game Over aberta, inimigos continuam atirando e o Timer continua contando.

### 6. Pulo "extra" pelo coyote time
Depois de um pulo do chão, `coyoteTimeCounter` não é zerado ([characterMovement.cs:430-456](Assets/scripts/characterMovement.cs:430)) e `onGround` continua `true` por alguns frames (raio 0.258). Apertar pulo de novo nesses ~0.1s dá um segundo pulo **de chão** sem gastar o pulo duplo → dá pra fazer pulo triplo.
**Correção:** `coyoteTimeCounter = 0` dentro de `Jump()`.

### 7. Gancho pode prender o jogador para sempre
Em `State.Grappling` a única saída é chegar a `minDistanceToFinish` (0.5) do alvo ([GrapplingHook.cs:126-139](Assets/scripts/GrapplingHook.cs:126)). O alvo é `bounds.center` do collider; se algo bloquear o caminho (ou o centro estiver dentro de um collider), o jogador fica travado sem gravidade (`characterMovement` zera a gravidade enquanto `IsGrappling`). Não há timeout nem cancelamento. Também dá NRE se `hookTipTransform`/`firePoint` forem nulos ([:96](Assets/scripts/GrapplingHook.cs:96), [:178](Assets/scripts/GrapplingHook.cs:178)).
Além disso, o sprite de `HookTip` está **faltando** (Cyborg.prefab, HookTip.prefab) → a ponta do gancho é invisível.

### 8. Parallax nunca se mexe
`ParallaxBackground.Start()` usa o campo `_parallaxCameraInstance` (sempre `null` no início) em vez da propriedade `ParallaxCameraInstance` ([ParallaxBackground.cs:32](Assets/scripts/ParallaxBackground.cs:32)). Ninguém chama a propriedade, então o evento nunca é assinado. Afeta `QuartaFase 1` e `QuintaFase`.
**Correção:** trocar por `ParallaxCameraInstance.onCameraTranslate += Move;`.

### 9. EnemyBullet pode ativar o Game Over **do prefab**
Se o `GameOverBackground` estiver inativo (e está), `FindGameObjectWithTag` não acha, e o fallback usa `Resources.FindObjectsOfTypeAll<Canvas>()` ([EnemyBullet.cs:45](Assets/scripts/EnemyBullet.cs:45)), que também retorna o `Canvas.prefab` do projeto. A ordem não é garantida → pode ativar o objeto do asset em vez do da cena.
Também: `Destroy(gameObject, 5f)` é chamado **a cada frame** no `Update` ([:80](Assets/scripts/EnemyBullet.cs:80)) — deveria ser uma vez no `Start`.

### 10. Loja: compras não persistem e alguns itens são inúteis
- As quantidades compradas vivem só em `shopItems` (memória) → ao recarregar, dá pra comprar tudo de novo ([ShopManager.cs:44-73](Assets/scripts/ShopManager.cs:44)).
- `canWallJump` e `canGrapplingHook` já começam `true` no `PlayerData` → comprar esses itens não muda nada.
- Upgrades só valem na próxima fase: `characterMovement` lê os stats uma vez no `Start` ([:189-212](Assets/scripts/characterMovement.cs:189)).
- `inventory` nunca é salvo/carregado.

### 11. "Novo Jogo" apaga as configurações
`PlayerData.ResetData()` chama `PlayerPrefs.DeleteAll()` ([PlayerData.cs:81](Assets/scripts/PlayerData.cs:81)), apagando também volume e VSync. O mesmo vale para o F12 do menu.

### 12. Moedas podem ser farmadas
Cada moeda é salva na hora ([characterMovement.cs:494-495](Assets/scripts/characterMovement.cs:494)); morrer e dar "Restart" mantém as moedas e respawna todas.

### 13. Não dá pra testar uma fase dando Play direto nela
As fases não têm câmera nem `SceneController` (dependem dos objetos DDOL vindos do `MainMenu`). Play direto em `PrimeiraFase` = sem câmera, sem jogador, e o `EndGoal` loga "SceneController instance is null!".

---

## 🟡 Menores / higiene

| # | Onde | Problema |
|---|------|----------|
| 14 | `IntroDoctor.unity` | `DialogueManager.dialogueText` e `dialoguePanelImage` vazios → NRE no `Start`. (Cena fora do build.) |
| 15 | `Coin.prefab` (2DRPK) | Sprite faltando. |
| 16 | Todos os `EndGoal` | `victorySound` vazio → sem som de vitória. `soundVolume = 5/100` (volume > 1 distorce). |
| 17 | Músicas das fases | `Loop = false` → a música para no meio da fase. |
| 18 | `CameraFollow` | Câmera DDOL no menu faz `FindWithTag("Player")` + `LogWarning` **todo frame** (é o warning que aparece no console). Limites da câmera de uma fase continuam valendo na próxima se ela não tiver `CameraBoundary`. |
| 19 | Música do menu | `MainMenu`, `SettingsMenu` e `EndGame` têm cada um seu `MenuSounds` sem DDOL → a música reinicia a cada troca menu ↔ config. |
| 20 | VSync | A preferência só é aplicada ao abrir a tela de Configurações; ao abrir o jogo volta ao padrão. `Resolution.refreshRate` é obsoleto. |
| 21 | Debug em build | F12 (apaga o save, `MainMenu.cs:86`) e F5 (`PlayerSaveController.cs:41`) funcionam no jogo final. |
| 22 | Código morto | `SaveManager`/`SaveData`/`PlayerSaveController` (segundo sistema de save, não usado), `MovementDiagnostic` (vazio), `GameController.coinCount` (não usado), `VerticalJumpController`. |
| 23 | Encoding | `EndGoal`, `ShopManager`, `ButtonInfo`, `CameraFollow`, `VolumeSettings`, `VSyncSettings`, `ApplySavedVolume`, `CameraBoundary` estão em **ISO-8859-1**. Funciona no Windows, mas em Mac/Linux o texto "Preço" da loja vira "Pre�o". Converter para UTF-8. |
| 24 | APIs obsoletas | 20+ usos de `FindObjectOfType`/`FindObjectsOfType` (warnings CS0618). |

---

## Ordem sugerida de correção
1. Unificar o `PlayerData` (um só, persistente) e limpar as referências ao prefab no Timer/GameOver → resolve 1, 2, 3 e boa parte do 9.
2. Build Settings + `gameLevelIndexes` + prefab `GameController` faltando (4).
3. Travar o jogador na morte (5), coyote (6), timeout no gancho (7), parallax (8).
4. Resto conforme tempo.
