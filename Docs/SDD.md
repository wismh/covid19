# SDD — COVID-19: The Beginning (технічний дизайн-документ)

> Знімок стану на 2026-09-19, після завершення фаз 0–7 рефакторингу
> (`Docs/REFACTORING_PLAN.md`) і 13 раундів живого тестування через Unity CLI. Описує систему
> такою, якою вона фактично зібрана й працює зараз — включно з місцями, де нова архітектура ще не
> витіснила стару. Це технічний документ для розробників; ігровий дизайн і точні баланс-числа —
> у [GDD.md](GDD.md). Історія причин кожного архітектурного рішення — у
> [REFACTORING_PLAN.md](REFACTORING_PLAN.md); оцінка якості коду ДО рефакторингу — у
> [TECHNICAL_AUDIT.md](TECHNICAL_AUDIT.md) (застаріла щодо поточного стану, залишена як історичний
> референс).

## 1. Область застосування

Проєкт одночасно містить **дві архітектури в одному Play Mode**:

- **`Assets/GMTK/Scripts/**`**, простір імен `GMTK.*` — оригінальний джем-код (Unity 2022.3,
  Built-in RP, `ServiceLocator`, корутини). Частина цих класів досі `m_Enabled: 1` в сцені й
  фактично керує реальною поведінкою гри там, де новий код ще не отримав відповідної фічі.
- **`Assets/_Project/Scripts/**`**, простір імен `_Project.*` — цільова архітектура (Unity
  6000.3.10f1, URP 2D, Zenject, UniTask). Це код, який слід читати й розширювати для будь-якої
  нової роботи.
- **`Assets/_Lumenwake/Scripts/Runtime/**`** — код-агностичний до гри вендорений шаблон
  (`AssetLoaderModule`, `AudioSystem`, `SaveServiceModule`, `SceneLoaderServiceModule`,
  `StateMachineModule`, `UIModule`, `ComponentRegistry`, `ParticleFxModule`, `SteamModule`,
  `ApplicationLifecycleModule`) — скопійований з `C:\Projects\Lumenwake\template` без змін.
  Не редагувати заради цього проєкту; будь-яка потрібна зміна тут — сигнал оновити сам шаблон.

Розділ 7 нижче зводить точний перелік того, де саме стара система досі є джерелом правди.

## 2. Технологічний стек

| | |
|---|---|
| Рушій | Unity **6000.3.10f1** |
| Render Pipeline | URP 17.3.0, 2D Renderer |
| DI-контейнер | Zenject (лише для платформних сервісів — розділ 4.1) |
| Асинхронність | UniTask (`Cysharp.Threading.Tasks`) — витіснив корутини в новому коді |
| Твінінг | DOTween (той самий інстанс, що й у старому коді) |
| Збереження | `_Lumenwake.SaveServiceModule` (Newtonsoft.Json-серіалізація) поверх `ISaveService<TState>` |
| Аудіо | `_Lumenwake.AudioSystem` (`IAudioSystem`, кросфейд музики вбудований) |
| UI-фреймворк | `_Lumenwake.UIModule` (`BaseScreen`/`BaseScreenManager`, шарова система екранів) |
| Асети | TextMesh Pro (з `com.unity.ugui` 2.0.0 в Unity 6, окремий пакет TMP більше не потрібен) |
| Платформи збірки | WebGL (itch.io), Windows x64 |
| Сцени в білді | `BootstrapScene` (0) → `Menu` (1) → `SampleScene` (2) |

## 3. Конвенція неймспейсів і папок

```
Assets/
├── _Lumenwake/   — вендорений шаблон, не чіпати заради цієї гри
└── _Project/
    └── Scripts/
        ├── Bootstrap/      — ProjectContextInstaller, Bootstrap.cs (DI-контейнер, старт застосунку)
        ├── Save/           — GameSaveState, GameSaveService, SaveTrigger
        ├── Mechanics/       — ігрові правила, одна папка на систему
        │   ├── AppFlow/          — GlobalGameStateMachine, MenuState, GameplayState
        │   ├── Player/           — PlayerConfig, PlayerStatsService, PlayerMovement/Turret/Health
        │   ├── Cells/            — CellConfig, CellHealth/InfectionState/Visuals, CellSpawner/Registry
        │   ├── Viruses/Macro/    — MacroVirusConfig/Controller/Spawner/Registry, 3 IMacroVirusBehavior
        │   ├── Viruses/InCell/   — InCellVirusConfig/Controller/Spawner/Registry, 3 IInCellVirusBehavior
        │   ├── Waves/            — WaveScalingConfig (єдиний спільний конфіг ескалації складності)
        │   ├── WorldScale/       — WorldScaleController/Transition/Config/TransitionContext
        │   ├── Audio/            — GameAudioService/Catalog, GameSfxCue enum
        │   ├── Shared/           — EntityRegistry<T>, SpawnPlacementUtility
        │   └── Composition/      — MainSceneComposition (композиційний корінь геймплею)
        ├── UI/              — Hud/, Upgrades/, GameOver/, MainMenu/ — по одній папці на екран
        └── _Generated/      — Address_g.cs, PrefabNames_g.cs, SceneInBuild_g.cs (кодогенерація шаблону)
```

Неймспейс дзеркалить *логічну* папку без сегментів `Features`/`Runtime`/`Editor`
(`_Project.Mechanics.Player`, не `_Project.Mechanics.Runtime.Player.Features`), навіть якщо
фізичний шлях — `Scripts/Runtime/...`. Виключення: `_Generated/*_g.cs` мають
`namespace Project.Generated` (без підкреслення) — так хардкодить сам генератор шаблону.

## 4. Composition Root і DI

Проєкт свідомо **не** проганяє все через Zenject. Межа проведена так (зафіксовано в
`REFACTORING_PLAN.md` розділ 4):

- **Наскрізні/платформні сервіси** (Save, Audio, SceneLoader, Assets, app-flow стейт-машина) —
  через Zenject-контейнер.
- **Геймплейні системи** (`Mechanics/*`, окрім Save/Audio-обгорток) — прості C#-POCO, які
  композиційний корінь створює вручну через `new X(...)` і з'єднує їхні C#-події напряму.

Причина: у проєкті немає жодного Zenject `SceneContext` (перевірено — нуль збігів по всіх
`.unity`-файлах і референс-проєктах `template`/`td-over-unity`), тож жоден сценний `MonoInstaller`
ніколи не запускається автоматично. Замість цього кожен `MonoBehaviour`, якому потрібен доступ до
контейнера, сам себе інжектує через `Zenject.ProjectContext.Instance.Container.Inject(this)`
(лениво ініціалізований статичний акцесор) у власному `Awake()`.

### 4.1 `ProjectContextInstaller` — що реально є в Zenject-контейнері

`ScriptableObjectInstaller`, `Assets/_Project/Scripts/Bootstrap/ProjectContextInstaller.cs`,
екземпляр `Configurations/ProjectContextInstaller_Default.asset`, встановлюється з
`Assets/_Project/Resources/ProjectContext.prefab` (Zenject автозавантажує будь-який `.prefab` з
папки `Resources/`, незалежно від активної сцени).

Реально забайндженно (і більше нічого):

| Тип | Джерело |
|---|---|
| `ISceneLoaderService` | `SceneLoaderServiceModuleInstaller.Install` (`_Lumenwake`) |
| Asset-loading сервіси | `AssetLoaderInstaller.Install` (`_Lumenwake`) |
| `IAudioSystem` | `AudioSystemInstaller.Install` (`_Lumenwake`) |
| `ISaveService<GameSaveState>` / `IGameSaveService` | `GameSaveModuleInstaller.Install` (розділ 4.4) |
| `MenuState`, `GameplayState`, `GlobalGameStateMachine` | напряму в цьому інсталері, `AsSingle()` |
| `LifeController` | `FromComponentInHierarchy().AsSingle()` (`_Lumenwake.ApplicationLifecycleModule`) |

Жоден `PlayerConfig`/`CellConfig`/`MacroVirusConfig`/`WorldScaleConfig`/`WaveScalingConfig`/
`GameAudioCatalog`/`UpgradeDefinition` — усе це `[SerializeField]`-посилання на
ScriptableObject-асети, призначені напряму на `MainSceneComposition`, а не Zenject-біндинги.

`Assets/_Project/Scripts/UI/UiInstaller.cs` — **мертвий код**: без `SceneContext` жоден сценний
`MonoInstaller` не виконується, тож нічого не викликає `UiInstaller.InstallBindings()`. Залишений
у проєкті з коментарем, а не видалений (правило "вимкнути/лишити, не видаляти, поки не
підтверджено").

### 4.2 App-flow: `Bootstrap` → `GlobalGameStateMachine` → сцени

```
Bootstrap.Start()
  Application.runInBackground = true   // критично для CLI/автоматизованого тестування
  SceneManager.LoadScene(SceneInBuild.Menu)   // прямий виклик, ще без стейт-машини

GlobalGameStateMachine : StateMachineBehaviour<StateBase>   (_Lumenwake.StateMachineModule)
  states = [ MenuState, GameplayState ]

MenuState.Enter()      → ISceneLoaderService.LoadSceneAsync(SceneInBuild.Menu, unloadRedundant: true)
GameplayState.Enter()  → ISceneLoaderService.LoadSceneAsync(SceneInBuild.SampleScene, unloadRedundant: true)
```

`Bootstrap.cs` сам ще не переведений на стейт-машину (лишається прямим `SceneManager.LoadScene` —
свідомо, оскільки старий `MenuManager`/`HUD`-код, який стейт-машина мала б замінити, ще активний).
`MainMenuScreen.Play()`/`GameOverScreen.Restart()` вже викликають `GlobalGameStateMachine.Enter<T>()`
коректно, але жодна кнопка в сцені `Menu.unity` на них не вказує (розділ 7).

### 4.3 `MainSceneComposition` — композиційний корінь геймплею

`Assets/_Project/Scripts/Mechanics/Composition/MainSceneComposition.cs` — єдиний `MonoBehaviour`
у `SampleScene.unity`, що "знає про все". Прямий аналог `td-over-unity`'s `MainSceneView.BuildArena()`.
Послідовність у `Awake()` (усе саме в `Awake()`, не `Start()` — Unity гарантує, що всі `Awake()` у
сцені відпрацюють раніше за будь-який `Start()`, а кілька спавнерів залежать від цього):

1. `Zenject.ProjectContext.Instance.Container.Inject(this)` — самоін'єкція (розділ 4).
2. `ResolvePlayerComponentReferences()` — обхід відомого Unity-бага (розділ 8.3).
3. `BuildScreenManager()` — вручну збирає `BaseScreenManager` зі списку `[HudScreen, UpgradeScreen, GameOverScreen]`, примусово ховає Upgrade/GameOver-групи (сирий стан сцени лишає обидві активними).
4. `BuildMechanicsPocos()` — `new PlayerStatsService(...)`, `new CellRegistry()`, `new MacroVirusRegistry()`, `new WorldScaleController(new WorldScaleTransition(...))`, `new GameAudioService(...)`.
5. `ConstructSpawners()` — `CellSpawner.Construct(...)`, `MacroVirusSpawner.Construct(...)`, `PlayerMovement/Turret/Health.Construct(...)`.
6. `ConstructScreens()` — ручний виклик `Construct(...)` на кожному `BaseScreen` (не покладаючись на Zenject `FromInstance`-повторну ін'єкцію — прибирає невизначеність порядку).
7. `WireCrossSystemEvents()` — фактична "склейка": `CellRegistry.OnStatisticsChanged → HudScreen.SetStatistics`, `OnAllCellsDied → GameOverScreen`, `OnCellDied → MacroVirusSpawner.SpawnReplacementVirusesAsync`, `WorldScaleController.OnEntered/ExitedMicroWorld → MacroVirusSpawner.SetPaused`, `PlayerHealth.OnDied → HandleGameOver`, XP-підписки на `OnKilled` кожного вбитого вірусу.
8. `_screenManager.OpenScreen<HudScreen>()` — єдиний екран, що відкривається автоматично на старті.
9. `PollNearestCellAsync` — UniTask-цикл кожні 0.2с, що оновлює "найближчу клітину" для `CellProximityPrompt` і полів `WorldScaleTransitionContext`.

`Start()` (окремо від `Awake()`, оскільки читає `GMTK.Services.ServiceLocator` — реєстрація
старих сервісів гарантована лише між усіма `Awake()` і будь-яким `Start()`, не всередині одного
`Awake()`) — місток до старого коду, розділ 7.

## 5. Ключові патерни проєкту

- **POCO "факт" + MonoBehaviour/DOTween-колаборатор "дія".** Повторюється в кожній системі:
  `WorldScaleController` (стан + події) керує ін'єктованим `IWorldScaleTransition`
  (`WorldScaleTransition`, весь DOTween/Camera/Rigidbody2D-код); `CellHealth`/`CellInfectionState`
  (стан) поруч із `CellVisuals` (візуальний ефект); `PlayerStatsService` (числа) окремо від
  `PlayerHealth`/`PlayerMovement`/`PlayerTurret` (MonoBehaviour, що їх застосовують).
- **`EntityRegistry<T> where T : Component`** (`Mechanics/Shared/EntityRegistry.cs`) — заміна
  п'яти порожніх підкласів старого `ObjectManager`, що існували лише заради типу-тегу для
  `ServiceLocator`. Дає `Register`/`Unregister`/`FindNearest(point[, out sqrDistance])` +
  `OnRegistered`/`OnUnregistered`. `CellRegistry`/`MacroVirusRegistry`/`InCellVirusRegistry`
  композують його всередині; `CellRegistry` додає поверх реальну доменну логіку (живі/хворі/
  загиблі лічильники, `OnAllCellsDied` vs `OnCellDied`).
- **Strategy для ШІ ворогів**, збережено з оригінального коду як найсильніший його шматок:
  `IMacroVirusBehavior` (`MoveToCellBehavior`/`AttackCellBehavior`/`PenetrationIntoCellBehavior`)
  і `IInCellVirusBehavior` (`OrangeVirusBehavior`/`CucumberVirusBehavior`/`SpinerVirusBehavior`).
- **Конфіг як ScriptableObject**, іменування `<ClassName>_<Variant>` (`PlayerConfig_Default`,
  `InCellVirusConfig_Orange`, `UpgradeDefinition_Damage`) — жодних магічних чисел інлайн; `menuName`
  завжди дзеркалить `Configurations/<Feature>/`.
- **Прямі `[SerializeField]`-посилання й сиблінг-`GetComponent<T>()`** замість
  `transform.Find("string")`/`GetChild(index)` — єдиний виняток, коли sibling-lookup свідомо
  повторюється кожен `Awake()` (наприклад `ResolvePlayerComponentReferences`), задокументований
  як обхід конкретного Unity-бага, а не звичка.
- **`event Action<T>`, не `UnityEvent`**, з дисципліною відписки: `BaseScreen`-екрани підписуються
  в `OnOpen()`/відписуються в `OnClose()` (не `Start()`/`OnEnable()` — Zenject-`[Inject] Construct`
  може ще не відпрацювати до `OnEnable()`); POCO-сервіси (`GameAudioService.Dispose()`,
  `MainSceneComposition.OnDestroy()`) відписуються симетрично через кешовані делегати-поля, коли
  підписка — лямбда, яку інакше не можна було б відписати.
- **UniTask замість корутин** усюди в новому коді, з явним `CancellationTokenSource` на кожному
  `MonoBehaviour`, що його потребує (`_lifetimeCts`, скасовується в `OnDestroy`).
- **Полінг там, де подія недоцільна, і це задокументовано.** Приклад:
  `MainSceneComposition.PollNearestCellAsync` (кожні 0.2с, не щокадру) — "найближча клітина"
  змінюється значно повільніше за рух гравця, подієва модель не окупилась би складністю.

## 6. Модулі: відповідальності та ключові типи

### 6.1 Player (`Mechanics/Player/`)

| Клас | Тип | Відповідальність |
|---|---|---|
| `PlayerConfig` | ScriptableObject | усі базові числа (швидкість, здоров'я, перезарядка, крива XP) |
| `PlayerStatsService` | POCO | XP/рівень, per-stat рівні апгрейдів, `Effective*`-властивості (чисті функції від конфігу + рівня апгрейду) |
| `UpgradeDefinition` | ScriptableObject × 6 | `UpgradeStat` (enum) → `EffectPerLevel`, `DisplayName` |
| `PlayerMovement` | MonoBehaviour, `[Inject] Construct` | інерційний рух/поворот, `SetSpeedScale(float)` для мікро-світу |
| `PlayerTurret` | MonoBehaviour | наведення на курсор, `Fire()`/перезарядка, `SetProjectileSpawner(IProjectileSpawner)` |
| `PlayerHealth` | MonoBehaviour, реалізує `GMTK.IDamageable` | HP/броня, `Damage()`, послідовність смерті, підписка на `PlayerStatsService.OnUpgraded`/`OnBonusHealthGranted` |
| `IProjectileSpawner` / `GmtkBulletManagerProjectileSpawner` | інтерфейс + міст | **навмисний, поки не постійний місток до старого `GMTK.BulletManager`/`GMTK.Bullet`** — див. розділ 7 |

`PlayerHealth` реалізує старий `GMTK.IDamageable` без форку інтерфейсу — свідоме рішення
(`REFACTORING_PLAN.md` розділ 5), щоб старий `GMTK.Bullet.OnTriggerEnter2D`'s
`TryGetComponent<IDamageable>()` міг знайти новий компонент напряму.

### 6.2 Cells (`Mechanics/Cells/`)

`CellHealth` (здоров'я, отруєння через UniTask-тик, `OnDied`) + `CellInfectionState` (лише
лічильник вірусів, `OnInfectionStarted`/`OnInfectionCleared`/`OnVirusCountChanged`) +
`CellVisuals` (спрайти/шейдер/entrails) — три сиблінг-компоненти на одному `Cell.prefab`,
координуються прямим `[SerializeField]`-посиланням, без окремого класу-оркестратора.
`CellSpawner` (`Start()`, не `Awake()` — чекає, доки `Construct()` точно відпрацює) спавнить 20
екземплярів через `SpawnPlacementUtility.FindPosition` і реєструє кожен у `CellRegistry`
(розділ 4.3, розділ 5).

### 6.3 Macro Viruses (`Mechanics/Viruses/Macro/`)

`MacroVirusController` (`[Inject] Construct`, власна фізика дрейфу — копія `GMTK.DriftMovableObject`)
делегує поведінку одному з трьох `IMacroVirusBehavior` залежно від стану контакту з клітиною.
`MacroVirusSpawner` рахує хвилі (`_waveSize *= WaveScalingConfig.WaveSizeGrowthMultiplier` після
кожного повного знищення хвилі), обирає важчий префаб після `GlistUnlockTimeSeconds`.
`MacroVirusRegistry` — тонка обгортка над `EntityRegistry<MacroVirusController>`, без власної
логіки (на відміну від `CellRegistry`).

### 6.4 In-Cell Viruses (`Mechanics/Viruses/InCell/`)

`InCellVirusController` + один з трьох `IInCellVirusBehavior`
(`OrangeVirusBehavior`/`CucumberVirusBehavior`/`SpinerVirusBehavior`), один клас
`InCellVirusConfig` × 3 асети-варіанти (`_variant` — enum-дискримінатор). `InCellVirusSpawner`
сидить на кожному `Cell.prefab`; кількість вірусів встановлюється **лінькво, один раз**, коли
гравець реально входить у клітину (`WorldScaleTransition.EnterMicroWorldAsync` викликає
`SetVirusesCount`), а не реактивно на кожну зміну лічильника зараження — свідомий вибір,
задокументований у `InCellVirusSpawner`'s doc comment (реактивна версія була відкинута раундом
тестування: давала повнорозмірних вірусів, що блукали макро-світом).

### 6.5 World Scale (`Mechanics/WorldScale/`)

Розбиття на "факт" і "дію" (розділ 5): `WorldScaleController` (чистий стан +
`OnEntering*`/`OnEntered*`/`OnExiting*`/`OnScaleChanged`, `IsTransitioning`-guard проти
паралельних переходів) керує ін'єктованим `IWorldScaleTransition` (`WorldScaleTransition` —
увесь DOTween: масштаб/камера/позиція/шар/колайдер/гучність, зібрані в один `Sequence` через
`UniTaskCompletionSource` + `OnKill`, оскільки в проєкті немає async-модуля DOTween).
`WorldScaleTransitionContext` — POCO з усіма per-call посиланнями (гравець, камера, поточна
клітина та її компоненти), яку `MainSceneComposition` тримає й оновлює щотік
(`PollNearestCellAsync`), а не перебудовує щоразу.

**Подієва семантика, відмінна від старого коду:** `CurrentScale` і всі `OnEntered*`/`OnExited*`
події спрацьовують лише **після** завершення переходу (стара `GamePlayState.State`-сеттер міняла
стан синхронно, до початку твіну). `OnEntering*`/`OnExiting*` додані окремо саме для слухачів,
яким потрібен старий "негайно" таймінг (звук "свисту" — `GameAudioService`).

### 6.6 UI (`UI/**`, `_Lumenwake.UIModule`)

`BaseScreen`/`BaseScreenManager` — шарова система екранів (`ScreenLayer.Default`/`Overlay`),
відкриття через `IScreenManager.OpenScreen<T>()`. Правило проєкту: підписка на події сервісів у
`OnOpen()`, відписка в `OnClose()` — ніколи в `Start()`/`OnEnable()`, бо `[Inject] Construct` може
ще не відпрацювати до `OnEnable()`.

| Екран | Шар | Відкривається |
|---|---|---|
| `HudScreen` | Default | автоматично, раз, на старті гри (`MainSceneComposition.Awake()`) |
| `UpgradeScreen` | Overlay | `HudScreen` підписана на `PlayerStatsService.OnLevelUp` |
| `GameOverScreen` | Overlay | `HudScreen` підписана на `PlayerHealth.OnDied`; або напряму `MainSceneComposition.HandleAllCellsDied` |
| `MainMenuScreen` | — (сцена `Menu`) | нічого — не підключений до реальних кнопок (розділ 7) |

`HudScreen` навмисно тримає обидві тригер-підписки (Upgrade і GameOver), бо закритий екран не
може підписатись сам на подію, яка мала б його відкрити.

### 6.7 Save (`Save/**`)

Єдиний платформний сервіс, повністю Zenject-біндженний (на відміну від решти `Mechanics/*`).
`GameSaveState` (POCO, 5 полів — розділ 6.7.1) → `ISaveService<GameSaveState>`
(`_Lumenwake.SaveServiceModule`, вбудований 300мс debounce на `RequestSave()`, Newtonsoft.Json,
`Application.persistentDataPath/save.json`) → `GameSaveService : IGameSaveService, IInitializable,
IDisposable` (єдиний тип, від якого залежить геймплей/UI-код; `Initialize()` синхронно вантажить
збереження до першого кадру). `SaveTrigger : IInitializable, IDisposable` — **другий, незалежний**
шлях до `SaveImmediate()` через `LifeController.OnQuit` (`_Lumenwake.ApplicationLifecycleModule`),
бо Zenject викликає `IDisposable.Dispose()` лише якщо `ProjectContext`'s власний `OnDestroy()`
реально відпрацював — не гарантовано на WebGL.

**6.7.1 Схема `GameSaveState`** (версія схеми 1, без міграцій):

```csharp
int   Version;                 // SaveJsonVersion/SaveMigrationRunner
float MasterVolume = 1f;
float MusicVolume  = 1f;
float SfxVolume    = 1f;
float BestSurvivalTimeSeconds;
int   BestPlayerLevel = 1;
```

### 6.8 Audio (`Mechanics/Audio/`)

`GameAudioCatalog` (ScriptableObject) — `GameSfxCue` (enum, 15 значень) → `AudioClip`/гучність/
діапазон висоти, плюс два `SoundData` для меню- й геймплейної музики. `GameAudioService` (POCO,
не Zenject) — обгортка над `IAudioSystem`, підписується на `WorldScaleController.OnEntering*`
для свисту переходу. **Не відтворює** заміну аудіо-мікшер-групи (стара `InCellMixer` vs
`DefaultMixer`) — задокументований, свідомо відкладений розрив (розділ 8.2).

## 7. Місток до старого коду (`GMTK.*`) — де стара система досі є джерелом правди

Це не список багів — це поточна, свідома конфігурація системи. Кожен рядок нижче — місце, де
`_Project`-код або взагалі не існує, або існує, але не є тим, що реально виконується в грі просто
зараз.

| Функція | Хто фактично керує | Деталі |
|---|---|---|
| Урон кулі гравця | **`GMTK.Bullet`** | `PlayerTurret.Fire()` → `GmtkBulletManagerProjectileSpawner` → `GMTK.BulletManager` → `GMTK.Bullet` рахує урон зі старого `GMTK.Services.PlayerStats.GetLevelUpgrade(PLAYER_DAMAGE)`, а не з `PlayerStatsService.EffectiveDamage` |
| Масштаб/швидкість кулі в мікро-світі | **`GMTK.Bullet.Update()`** | читає `GamePlayState.State`, синхронізоване новим `GamePlayState.SyncWorldState(...)` (адитивний метод, викликається з `WorldScaleController.OnScaleChanged`) |
| Музика (меню + геймплей, кросфейд) | **`GMTK.AudioMixerController`/`AudioManagerFactory`** | `DontDestroyOnLoad`-синглтон; `GameAudioService.PlayGameplayMusic()` існує, але виклик прибрано з композиції — одночасний запуск обох спричиняв накладання двох треків |
| Головне меню (кнопки Play/Exit, вступна катсцена) | **`GMTK.MenuManager`** | `MainMenuScreen` існує поруч на тому ж GameObject, але жодна кнопка на неї не вказує |
| Стрілка-локатор | **`GMTK.Arrow`/`ArrowController`** | читає старі `VirusManager`/`VirusCellManager` (`ObjectManager`-пули); `MainSceneComposition.Start()` дзеркалить нові `MacroVirusRegistry`/`CellRegistry`-події в ці старі пули, щоб стрілка мала на що вказувати |
| Пігулки-Medical / вітаміни | **`GMTK.Medical`/`Vitamin`/`MedicalSpawner`** | взагалі не портовані; `MainSceneComposition.Start()` підписує старий `PlayerStats.OnExperienceChange` на виклик нового `_playerStats.AddExperience()` |
| Камера, що слідкує за гравцем | **`GMTK.CameraFollow`** | `MainSceneComposition.Start()` встановлює `cameraFollow.Target` (стара `PlayerFactory`, яка раніше це робила, вимкнена) |
| "Заглушений" мікшер усередині клітини | **ніхто** | стара `TransitionIntoMicroWorld.SetIncell()`/`TransitionIntoMacroWorld.SetMaster()` існують, але викликаються лише зі старого `GamePlayState.State`-СЕТЕРА, який новий код більше не викликає (лише синхронізує через адитивний `SyncWorldState`) — фіча зникла для обох систем одночасно |

Патерн для будь-якої майбутньої роботи над цими рядками: спочатку прочитати відповідний старий
`GMTK.*`-клас, перенести код у `_Project`, і лише тоді видаляти (не вимикати) старий компонент —
метод, що послідовно застосовувався в усіх 13 раундах тестування.

## 8. Технічний борг і відомі інженерні пастки

### 8.1 Успадковано з оригінального коду (`TECHNICAL_AUDIT.md`, ще актуально)

- `AudioList.Play(0..3)` (в старих класах, що ще активні) — числові індекси кліпів без
  named-констант; замінено на `GameSfxCue` лише в новому коді.
- `ResourceManager.cs` — вестигіальна обгортка над `Resources.Load<T>`, жодного виклику в
  геймплейному коді.
- Індекс сцени в білді логічно значущий для старого `AudioMixerController`
  (`buildIndex == 0/1`) — перестановка сцен у Build Settings мовчки зламає стару аудіологіку.

### 8.2 Виявлено під час рефакторингу/тестування — специфічно для цього проєкту

- **`m_Enabled: 0` не зупиняє `Awake()`, `OnDestroy()`, вже запущені корутини чи DOTween-колбеки.**
  Найчастіше джерело регресій за всі 13 раундів (два гравці, дублікати клітин, `IDamageable`,
  що резолвиться в мертвий старий компонент — розділ 7 таблиця вище пояснює наслідок). Практичний
  висновок: коли старий компонент реально конфліктує з новим (не просто дублює вже неробочу
  роботу), **видаляти, а не вимикати**.
- **`TryGetComponent<IDamageable>()` повертає ПЕРШИЙ компонент за порядком у списку** — старий,
  вимкнений, але не видалений компонент, доданий РАНІШЕ нового, і далі резолвиться першим. Три
  такі конфлікти (`GMTK.PlayerDamageable`, `GMTK.VirusController` на обох макро-префабах,
  `GMTK.MicroViruses.InCellVirus` на всіх трьох внутрішньоклітинних префабах) довелось видалити
  повністю, а не просто вимкнути.
- **"Stripped" PrefabInstance-посилання на кастомні `MonoBehaviour`-типи десеріалізуються як
  `null` під рантайм `SceneManager.LoadScene`**, хоча ті самі посилання на вбудовані типи
  (`BoxCollider2D`, `Transform`) на тій самій GameObject резолвяться коректно за будь-якого шляху
  завантаження. Відтворюється стабільно; повністю не root-caused (схоже на внутрішню особливість
  Unity для PrefabInstance-оверрайдів кастомних скриптів саме під рантайм-завантаженням, на
  відміну від прямого відкриття сцени в Editor). Обхід:
  `MainSceneComposition.ResolvePlayerComponentReferences()` — перерезолвлює через
  `GetComponent<T>()` з надійно резолвленого сиблінга (`_playerCollider`), лише для полів, що
  лишились `null`.
- **`DOTween`-твіни продовжують тікати незалежно від `GameObject.activeSelf`** — пауза спавнера
  через `SetActive(false)` не заморожує вже запущену анімацію проникнення вірусу в клітину.
  Обхід: явний `DOTween.Pause(virus.transform)`/`DOTween.Play(...)` по кожному зареєстрованому
  вірусу, окремо від `GameObject`-тогла.
- **Колізія GUID між двома `.meta`-файлами** (`SampleScene.unity`/`URP2DSceneTemplate.unity`)
  ламала саме рантайм-завантаження сцени (`SceneManager.LoadScene`), хоча `EditorSceneManager`/
  CLI-команда відкриття за шляхом працювали нормально — `AssetDatabase.Refresh()`/`ImportAsset`
  не виправляють цей розсинхрон, потрібен ручний перезапис `.meta` + `EditorBuildSettings.asset`.
- **Аудіо-мікшер обмежений однією групою `Master`** — `AudioSystem._mixer`/`_sfxGroup`/
  `_musicGroup` прив'язані до єдиної групи `DefaultMixer.mixer`, без exposed-параметрів
  гучності. `GameSaveState` вже зберігає три окремі значення гучності, але керувати ними окремо
  зараз нема через що — вимагає ручного авторингу мікшера (Music/SFX-групи + exposed parameters)
  в Editor.
- **`Physics2D`-шари** (`InsideCellPlayer`, `BulletMacro`/`BulletMicro`, `Barier`) прив'язані до
  конкретних рядкових імен у кількох місцях — при подальшому редагуванні collision matrix звіряти
  з оригіналом.

### 8.3 Відкриті, задокументовані, але не заблоковані питання

- Один нереплікований звіт: спрайт зараженої клітини під час перебування гравця всередині може
  за певних умов виглядати не так, як очікується — код-шлях перевірено, не знайдено жодного
  способу це викликати; можливо, вже вирішилось побічно іншим фіксом.
- Заміна шрифту (Comic Sans MS → Fredoka) підготовлена (файли, ліцензія), але сам TMP Font Asset
  (SDF-атлас) — порожня заглушка; генерація атласу вимагає реального рендерингу гліфів в Editor.

## 9. Інструментарій розробки

Проєкт живого тестується через **Unity CLI**, під'єднаний до вже запущеного Editor-інстансу
(`unity status`/`unity command eval`, `editor_play`/`editor_stop`, `get_console_logs`,
`capture_game_view`) — не через ручні звіти користувача про баги. Ключові практичні правила:
`AssetDatabase.Refresh()` обов'язковий після кожної зовнішньої правки `.cs`-файлу (Editor не
перекомпільовує сам); `capture_game_view`/`ScreenCapture.CaptureScreenshotAsTexture()` **не**
відображають URP пост-процес у цьому середовищі — перевіряти рендером у `RenderTexture` напряму;
позиційно-залежні перевірки виконувати одним атомарним `eval`-викликом, а не послідовністю
окремих команд (реальний час минає між CLI-раундтрипами, `Application.runInBackground` тримає
Play Mode активним і між викликами). Повний перелік — `CLAUDE.md` і скіл `testing-round`.

## 10. Джерела

- Пряме читання `Assets/_Project/Scripts/**` і тих файлів `Assets/GMTK/Scripts/**`, які досі
  фактично виконуються (розділ 7).
- `Docs/REFACTORING_PLAN.md` — повна історія рішень і журнал 13 раундів живого тестування.
- `Docs/TECHNICAL_AUDIT.md` — стан коду до рефакторингу (історичний референс для розділу 8.1).
- `Docs/GDD.md` — ігровий дизайн і точні баланс-числа (не дублюються тут навмисно).
