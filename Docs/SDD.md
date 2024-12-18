# SDD — COVID-19: The Beginning (технічний дизайн-документ)

> Знімок стану на 2026-09-19. Стару кодову базу (`Assets/GMTK/Scripts/**`) остаточно видалено з
> репозиторію — кожна система, що раніше залежала від неї напряму чи через тимчасовий міст
> (стрільба, камера, стрілка-локатор, пігулки, фон, головне меню, музика), тепер має власну
> нативну реалізацію в `Assets/_Project/**`. Цей документ описує єдину кодову базу, що лишилась.
> Технічний документ для розробників; ігровий дизайн і точні баланс-числа — у [GDD.md](GDD.md).
> Історія рішень і повний журнал міграції — у [REFACTORING_PLAN.md](REFACTORING_PLAN.md); оцінка
> якості коду ДО рефакторингу — у [TECHNICAL_AUDIT.md](TECHNICAL_AUDIT.md) (описує вже неіснуючу
> кодову базу, залишена як історичний референс).

## 1. Область застосування

Проєкт складається з двох частин:

- **`Assets/_Project/Scripts/**`**, простір імен `_Project.*` — уся гра (Unity 6000.3.10f1,
  URP 2D, Zenject, UniTask). Єдина кодова база, яку слід читати й розширювати.
- **`Assets/_Lumenwake/Scripts/Runtime/**`** — код-агностичний до гри вендорений шаблон
  (`AssetLoaderModule`, `AudioSystem`, `SaveServiceModule`, `SceneLoaderServiceModule`,
  `StateMachineModule`, `UIModule`, `ComponentRegistry`, `ParticleFxModule`, `SteamModule`,
  `ApplicationLifecycleModule`) — скопійований з `C:\Projects\Lumenwake\template` без змін.
  Не редагувати заради цього проєкту; будь-яка потрібна зміна тут — сигнал оновити сам шаблон.

Розділ 7 нижче — короткий історичний запис про те, чим була заміна `Assets/GMTK` і коли вона
завершилась; корисно для розуміння, чому частина doc-коментарів у коді досі згадує старі типи.

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
        │   ├── Combat/           — Bullet, BulletManager, IDamageable, VirusBullet/VirusBulletSpawner
        │   ├── Camera/           — CameraFollow, CameraExtensions
        │   ├── Navigation/       — Arrow, ArrowController (стрілка-локатор)
        │   ├── Pickups/          — MedicalPickup, VitaminPickup, MedicalSpawner
        │   ├── Environment/      — BackgroundBacteria/Manager, BackgroundParallax (декор)
        │   ├── Audio/            — GameAudioService/Catalog, GameSfxCue enum, AudioList
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

`Bootstrap.cs` сам ще не переведений на стейт-машину (лишається прямим `SceneManager.LoadScene`).
`MainMenuScreen.Play()` і `GameOverScreen.Restart()` викликають `GlobalGameStateMachine.Enter<T>()`
коректно; кнопки в сцені `Menu.unity` тепер справді викликають `MainMenuScreen.Play()`/`Exit()`
(перевірено — `m_MethodName: Play`/`Exit` у `Menu.unity`), а от `GameOverScreen.Restart()` у
`SampleScene.unity` досі ніким не викликається (розділ 8.2).

### 4.3 `MainSceneComposition` — композиційний корінь геймплею

`Assets/_Project/Scripts/Mechanics/Composition/MainSceneComposition.cs` — єдиний `MonoBehaviour`
у `SampleScene.unity`, що "знає про все". Прямий аналог `td-over-unity`'s `MainSceneView.BuildArena()`.
Послідовність у `Awake()` (усе саме в `Awake()`, не `Start()` — Unity гарантує, що всі `Awake()` у
сцені відпрацюють раніше за будь-який `Start()`, а кілька спавнерів залежать від цього):

1. `Zenject.ProjectContext.Instance.Container.Inject(this)` — самоін'єкція (розділ 4).
2. `ResolvePlayerComponentReferences()` — обхід відомого Unity-бага (розділ 8.2), тепер також резолвить `BulletManager`/`PlayerParticlesController`/`ArrowController`.
3. `BuildScreenManager()` — вручну збирає `BaseScreenManager` зі списку `[HudScreen, UpgradeScreen, GameOverScreen]`, примусово ховає Upgrade/GameOver-групи (сирий стан сцени лишає обидві активними).
4. `BuildMechanicsPocos()` — `new PlayerStatsService(...)`, `new CellRegistry()`, `new MacroVirusRegistry()`, `new WorldScaleController(new WorldScaleTransition(...))`, `new GameAudioService(...)`.
5. `ConstructSpawners()` — `CellSpawner`/`MacroVirusSpawner`/`MedicalSpawner.Construct(...)`, `PlayerMovement/Turret/Health.Construct(...)`, `BulletManager.Construct(_playerStats, _worldScaleController)`, `ArrowController.Construct(_macroVirusRegistry, _cellRegistry, _worldScaleController, _playerHealth, _worldCamera)`, `PlayerParticlesController.Construct(_worldScaleController, _playerMovement)`.
6. `ConstructScreens()` — ручний виклик `Construct(...)` на кожному `BaseScreen` (не покладаючись на Zenject `FromInstance`-повторну ін'єкцію — прибирає невизначеність порядку).
7. `WireCrossSystemEvents()` — фактична "склейка": `CellRegistry.OnStatisticsChanged → HudScreen.SetStatistics`, `OnAllCellsDied → GameOverScreen`, `OnCellDied → MacroVirusSpawner.SpawnReplacementVirusesAsync`, `WorldScaleController.OnEntered/ExitedMicroWorld → MacroVirusSpawner.SetPaused`, `PlayerHealth.OnDied → HandleGameOver`, XP-підписки на `OnKilled` кожного вбитого вірусу.
8. `_gameAudioService.PlayGameplayMusic()` і `_screenManager.OpenScreen<HudScreen>()` — музика й HUD стартують одразу, без жодної узгодженості зі старою системою (її більше не існує).
9. `PollNearestCellAsync` — UniTask-цикл кожні 0.2с, що оновлює "найближчу клітину" для `CellProximityPrompt` і полів `WorldScaleTransitionContext`.

`Start()` тепер лише встановлює ціль камери (`CameraFollow.SetTarget`) — до фінального видалення
старого коду тут же жив цілий блок мостів до `GMTK.Services.ServiceLocator` (камера, кулі,
пігулки, стрілка, синхронізація масштабу світу зі старим `GamePlayState`); увесь цей блок прибрано
разом з видаленням `Assets/GMTK` (розділ 7). **Застереження щодо doc-коментарів**: великий
блок XML-doc-коментарів усередині цього файлу (над `Construct`, `Start`, `WireCrossSystemEvents`)
досі детально описує ці вже видалені мости слово в слово — вони не оновлені після фінального
рефакторингу й описують код, якого більше немає в цьому ж файлі. Читати як історичний запис
причин, не як опис поточної поведінки; сама логіка методів (яку варто читати) — коротка й
нативна.

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
| `PlayerHealth` | MonoBehaviour, реалізує `Combat.IDamageable` | HP/броня, `Damage()`, послідовність смерті, підписка на `PlayerStatsService.OnUpgraded`/`OnBonusHealthGranted` |
| `IProjectileSpawner` | інтерфейс | реалізується `Combat.BulletManager` (розділ 6.9) |

`PlayerHealth`, `MacroVirusController`, `InCellVirusController`, `MedicalPickup` — усі
реалізують один спільний `_Project.Mechanics.Combat.IDamageable` (розділ 6.9), а не свій власний
інтерфейс кожен — єдина точка входу для будь-якого джерела урону (`Bullet`, `VirusBullet`,
ближній бій вірусів).

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
для свисту переходу, і викликається з `MainSceneComposition.Awake()` для старту геймплейної
музики (`PlayGameplayMusic()`); `MainMenuScreen` окремо викликає `IAudioSystem.PlayMusic` напряму
для меню-музики — обидва шляхи в підсумку йдуть через `_Lumenwake.AudioSystem`'s вбудований
кросфейд між двома внутрішніми `AudioSource`, тож перехід меню→гра не накладає два треки одне на
одне. `AudioList` (перенесений, не `GameAudioCatalog`-based) — по-індексний плеєр кліпів,
залишений для `PlayerHealth`, яка звертається до нього за старою звичкою "індекс = ефект" (2 =
удар, 3 = смерть) замість переходу на іменований `GameSfxCue`. **Не відтворює** заміну
аудіо-мікшер-групи (окрема "приглушена" група для мікро-світу) — задокументований, свідомо
відкладений розрив (розділ 8.2).

### 6.9 Combat (`Mechanics/Combat/`)

Уся бойова система, включно з єдиним спільним інтерфейсом урону:

| Клас | Відповідальність |
|---|---|
| `IDamageable` | `void Damage(int damage, Vector3 attackDirection)` — реалізують `PlayerHealth`, `MacroVirusController`, `InCellVirusController`, `MedicalPickup` |
| `Bullet` | політ снаряда гравця, масштаб/швидкість 0.3× у мікро-світі (запитує стан через `Func<bool> isMicroWorld`, не читає жоден стан напряму), `OnTriggerEnter2D` шукає `IDamageable` |
| `BulletManager` | `MonoBehaviour, IProjectileSpawner`; `UnityEngine.Pool.ObjectPool<Bullet>` (ємність 50, максимум 100); `Construct(PlayerStatsService, WorldScaleController)` — урон кожного пострілу читається напряму з `PlayerStatsService.EffectiveDamage` |
| `VirusBullet` / `VirusBulletSpawner` | дальня атака Cucumber (розділ 6.4) |

`BulletManager.Fire()` — єдине місце, де `PlayerStatsService.EffectiveDamage` реально впливає на
щось, окрім власного значення в UI; до фінального перенесення коду снаряд рахував урон з окремого,
вже нікому не підключеного лічильника (розділ 8.2 містить деталі цього вже закритого розриву).

### 6.10 Camera (`Mechanics/Camera/`)

`CameraFollow` — конфігурований `MonoBehaviour` (3 режими згладжування: `MoveTowards`/`Lerp`/
`SmoothDamp`, опційні межі руху). `MainSceneComposition.Start()` викликає `SetTarget(...)` на
гравця одразу, і повторно на клітину/гравця через `WorldScaleTransitionContext.SetCameraTarget`
при вході/виході з клітини.

### 6.11 Navigation (`Mechanics/Navigation/`)

`ArrowController` (`[Inject] Construct(MacroVirusRegistry, CellRegistry, WorldScaleController,
PlayerHealth, Camera)`) — щ 0.1с шукає найближчу ціль напряму через
`MacroVirusRegistry.FindNearestVirusTransform`/`CellRegistry.FindNearestInfectedCellTransform`
(макро-вірус має пріоритет при рівній відстані), ховає `Arrow`, коли ціль у кадрі камери, коли
гравець мертвий, або коли `WorldScaleController.CurrentScale == Micro`.

### 6.12 Pickups (`Mechanics/Pickups/`)

`MedicalPickup` (`IDamageable`) — руйнується від будь-якого урону (`_health` за замовчуванням 1,
тобто гине з одного влучання), вивільняє дочірні `VitaminPickup` через `_breakDelay` (1с).
`VitaminPickup` — імпульс-розліт + магнітний політ до гравця (`_targetProvider`/`_playerTransform`),
і **кожен екземпляр самостійно викликає `PlayerStatsService.AddExperience()`** при прильоті —
див. розділ 8.2 щодо наслідків цього для балансу. `MedicalSpawner` — таймер-спавнер
(`Construct(PlayerStatsService, IPlayerTargetProvider)`, конкретні числа — в `GDD.md` розділ 7).

### 6.13 Environment (`Mechanics/Environment/`)

`BackgroundBacteria`/`BackgroundBacteriaManager`/`BackgroundParallax` — чисто декоративні,
жодної логічної залежності від інших систем; перенесені без змін поведінки.

## 7. Історія: заміна `Assets/GMTK`

До 2026-09-19 частина систем свідомо працювала через тимчасові мости до старого коду
(`Assets/GMTK/Scripts/**`) — стрільба йшла через `GmtkBulletManagerProjectileSpawner` у старий
`GMTK.BulletManager`/`GMTK.Bullet`, музику й головне меню тримали `GMTK.AudioMixerController`/
`GMTK.MenuManager`, стрілку-локатор і пігулки не переносили взагалі, а `MainSceneComposition`
вручну дзеркалив нові події в старі `ObjectManager`-пули, щоб ці мости не ламались. Це було
задокументовано в попередній версії цього документа (див. git-історію файлу) як явна, тимчасова
конфігурація — не борг, а свідомий проміжний стан.

2026-09-19 усі ці системи отримали власну нативну реалізацію (розділи 6.9–6.13), `Assets/GMTK`
видалено з репозиторію повністю (жоден `.cs`-файл, жоден асет), і Unity Editor підтвердив 0 помилок
компіляції після видалення. Єдиний слід, що лишився: XML-doc-коментарі всередині кількох файлів
(найбільше — `MainSceneComposition.cs`, розділ 4.3) досі детально описують ці вже неіснуючі мости
як поточну поведінку — вартий уваги пункт гігієни коду, не функціональна проблема.

## 8. Технічний борг і відомі інженерні пастки

### 8.1 Успадковано з оригінального коду, досі актуально після перенесення

- `AudioList.Play(0..3)` — числові індекси кліпів без named-констант, перенесені 1:1 разом з
  класом і досі використовуються `PlayerHealth` (`Play(2)`=удар, `Play(3)`=смерть) — увесь решта
  нового коду вже перейшов на іменований `GameSfxCue` (розділ 6.8), цей один клас — ні.
- `ResourceManager.cs` (якщо ще існує в проєкті після реорганізації активів) — вестигіальна
  обгортка над `Resources.Load<T>`, жодного виклику в геймплейному коді; вартий перевірки на
  видалення разом із рештою гігієни активів.

### 8.2 Виявлено під час рефакторингу/тестування — специфічно для цього проєкту

- **Пігулка тепер дає в 4 рази більше досвіду, ніж в оригінальній грі (ймовірний недогляд, не
  свідоме рішення).** Стара `Medical.VitaminCallback()` рахувала прильоти всіх дочірніх вітамінів
  і викликала `AddExperience()` рівно ОДИН раз, коли долітав останній (`_score == transform.
  childCount`). Новий `VitaminPickup.Collect()` (розділ 6.12) викликає
  `PlayerStatsService.AddExperience()` сам, незалежно, кожного разу коли ДОЛІТАЄ ВІН, без жодної
  координації з рештою вітамінів тієї ж пігулки. `Medical.prefab` (тепер `Assets/_Project/Prefabs/
  Pickups/Medical.prefab`) має 4 дочірні `VitaminPickup` — тобто одна пігулка зараз дає **+4**
  замість оригінальних **+1**. Ніде в журналі рефакторингу це не згадано як навмисна зміна
  балансу — варто підняти з командою перед релізом.
- **`m_Enabled: 0` не зупиняє `Awake()`, `OnDestroy()`, вже запущені корутини чи DOTween-колбеки.**
  Найчастіше джерело регресій за час активної фази міграції зі старим кодом (два гравці, дублікати
  клітин, `IDamageable`, що резолвиться в мертвий старий компонент замість нового). Практичний
  висновок, застосовний до будь-якого майбутнього видалення компонента: коли старий компонент
  реально конфліктує з новим (не просто дублює вже неробочу роботу), **видаляти, а не вимикати**
  — саме так зрештою й вирішили все, що описував розділ 7.
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

- Пряме читання `Assets/_Project/Scripts/**` (єдина кодова база), конфігів/префабів/сцен під
  `Assets/_Project/**`, і `git log`/`git diff` для реконструкції того, що саме змінилось при
  фінальному видаленні `Assets/GMTK` 2026-09-19 (сама стара кодова база звірялась через
  `git show` попереднього коміту, оскільки на диску вже видалена).
- `Docs/REFACTORING_PLAN.md` — повна історія рішень, журнал 13 раундів живого тестування та
  фінальний запис про повне перенесення й видалення `Assets/GMTK`.
- `Docs/TECHNICAL_AUDIT.md` — стан коду до рефакторингу (історичний референс для розділу 8.1,
  описує вже неіснуючу кодову базу).
- `Docs/GDD.md` — ігровий дизайн і точні баланс-числа (не дублюються тут навмисно).
