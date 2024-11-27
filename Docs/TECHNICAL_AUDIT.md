# Технічний аудит — COVID-19: The Beginning (gmtk24)

> Аналіз поточного технічного стану проєкту: версія рушія, архітектура, організація асетів, якість коду. Слугує базою для [REFACTORING_PLAN.md](REFACTORING_PLAN.md).

## 1. Загальна інформація

| | |
|---|---|
| Unity | **2022.3.37f1**, Built-in Render Pipeline (не URP/HDRP) |
| Product/Company | `gmtk24` / `DefaultCompany` (ніколи не перейменовано) |
| Платформи збірки | WebGL, Windows x64 |
| Обсяг коду | 55 рукописних `.cs` файлів, **~3 588 рядків** (+1 згенерований `Actions.cs`, 487 рядків, Input System) |
| Сцени в білді | 2: `Menu.unity` (0), `SampleScene.unity` (1) — без прогресії рівнів |
| DI-фреймворк | **Відсутній** (не Zenject, не VContainer) |
| Async | Тільки корутини (`IEnumerator`/`StartCoroutine`), жодного `async/await`, `Task` чи UniTask |
| Ключові пакети | `com.unity.inputsystem` 1.7.0, `com.unity.postprocessing` 3.4.0 (legacy stack), `com.unity.textmeshpro`, `com.unity.timeline`, `com.unity.visualscripting` (підключений, але фактично не використовується — жодного графа Visual Scripting в проєкті, лише одна extension-функція) |
| Третьосторонні асети | DOTween (Demigiant) — основний twin для всіх переходів/анімацій; TextMesh Pro (стандартний імпорт) |

## 2. Архітектура

### 2.1 Патерни, що використовуються

- **Service Locator** (`Assets/GMTK/Scripts/Services/Services.cs`) — статичний `ServiceLocator.Instance`, словник `Dictionary<Type, IService>`. Практично кожен MonoBehaviour робить `ServiceLocator.Instance.Get<T>()` в `Awake()`/`Start()` замість `[SerializeField]`-посилань чи DI-контейнера. Використовується **послідовно й свідомо** по всьому проєкту (не хаотичний spaghetti), але:
  - `Get<T>()` — сирий індексатор словника (`dict[type]`), кидає виняток при відсутньому сервісі, замість `TryGetValue`;
  - створює приховані залежності від порядку виконання (`BootstrapState.RegisterServices()` має відпрацювати раніше за будь-який `Get<T>()`) без налаштованого Script Execution Order — тримається "на удачі" порядку об'єктів у сцені.
- **Дерево станів застосунку** (`GameStateManager`, `Dictionary<Type, IState>` FSM): `BootstrapState → LoadSceneState → GamePlayState`. `GamePlayState` виконує подвійну роль — і як стан застосунку, і як контролер режиму масштабу (`WorldState`), змішуючи два рівні абстракції в одному класі.
- **Strategy pattern** для ШІ ворогів — застосований двічі, чисто:
  - `IBehavior` (макро-віруси: `MoveToCell`/`AttackCell`/`PenetrationIntoCell`);
  - `VirusBehavior` (внутрішньоклітинні: `OrangeVirus`/`CucumberVirus`/`SpinerVirus`).
  Це найсильніша архітектурна частина проєкту — легко читається й розширюється.
- **Пулінг об'єктів** — лише кулі гравця через `UnityEngine.Pool.ObjectPool<Transform>` (`BulletManager`, ємність 50), реалізовано коректно. "Пули" клітин/вірусів/пігулок (`ObjectManager`) — насправді прості `List<Transform>`-реєстри без реального перевикористання об'єктів (Instantiate/Destroy як завжди), назва "Pool" вводить в оману щодо фактичної поведінки.
- **Події через прості C# делегати** (`event Action<T>`) — не `UnityEvent`. Розумний вибір для такого масштабу. Відписка в `OnDestroy` є не завжди (наприклад, `PlayerBodyController` підписується на `PlayerStats.OnUpgrade`, але ніколи не відписується — потенційний memory leak при повторному створенні гравця).
- **Інтерфейсів мало** — лише 4 у всьому проєкті: `IState`, `IService` (порожній маркер), `IDamageable`, `IBehavior`. Решта — конкретні класи, що напряму залежать одні від одних через ServiceLocator. Прийнятно для 96-годинного джему, але майже немає точок для unit-тестування чи підміни реалізацій.

### 2.2 Проблеми зв'язності (coupling)

- **Немає жодного `FindObjectOfType`/`GameObject.Find`** (підтверджено повним grep) — команда послідовно використовувала ServiceLocator.
- **Крихкі `transform.Find("string")` / `GetChild(index)`** — реальна й концентрована проблема:
  - `HUD.cs` — 6 викликів `transform.Find(...)` за іменем рядка + вкладений `.Find("YouDied")`;
  - `BarsHUD.cs` — доступ до 3 UI-барів через `GetChild(0).GetChild(1)`, `GetChild(1).GetChild(1)`, `GetChild(2).GetChild(1)` — жодного іменованого посилання;
  - `PlayerTurretController.cs`, `PlayerDamageable.cs`, `CellController.cs`, `TransitionIntoMacro/MicroWorld.cs` — аналогічні крихкі ланцюжки.
  Будь-яке перейменування/перестановка об'єкта в ієрархії Unity-редактора мовчки ламає це під час виконання без помилки компіляції.
- **Магічні числа**, розкидані по коду тюнінгу геймплею замість централізованого конфігу/ScriptableObject:
  - `BarsHUD.cs`: `fillAmount = Health * 0.13f` — недокументований коефіцієнт;
  - `PlayerDamageable.cs`: жорстко закодоване стартове здоров'я `3`, формула броні `Armor / 6.0f`, `Destroy(gameObject, 2f)`;
  - `PlayerStats`: крива рівня `×1.5` inline;
  - Часові гейти складності задубльовані в двох різних файлах з різними магічними порогами (`80`, `50`, `100`) без спільного конфігу кривої складності;
  - `CellSpawner` і `BackgroundBacteriaManager` незалежно реалізують той самий алгоритм rejection-sampling з різними константами (`MAX_CHOICES_VALUE = 5000` vs `500`) — дубльована логіка;
  - `AudioList.Play(0..3)` — числові індекси звукових кліпів без named-констант/enum, викликаються з кількох незв'язаних скриптів.
- **Несумісність неймспейсів**: майже все під `GMTK`/`GMTK.Services`/`GMTK.UI`/`GMTK.GameStates` тощо, але `MenuManager.cs` — у глобальному неймспейсі без `namespace`-блоку.

### 2.3 Розмір найбільших файлів

| Файл | Рядків |
|---|---|
| `CellController.cs` | 202 |
| `InCellVirus.cs` | 136 |
| `VirusController.cs` | 125 |
| `HUD.cs` | 108 |
| `PlayerDamageable.cs` | 105 |

Жоден файл не є справді монолітним — код загалом добре декомпозований на дрібні класи з однією відповідальністю для 96-годинного джему.

## 3. Організація асетів

- `Assets/GMTK/Scripts/` — feature-подібне групування (`Behaviors/Player`, `Behaviors/Cell`, `Behaviors/MacroVirus`, `Behaviors/MicroVirus`, `Behaviors/Medicals`, `GameStates`, `Services`, `UI`, `Menu`, `Background`) — зрозуміло й послідовно.
- Не-скриптові асети (`Animation`, `Textures`, `Audio`, `Prefabs`, `Materials`, `Shader`, `Fonts`) організовані **за типом** на верхньому рівні; лише `Animation/` і `Textures/` далі поділені за фічею/персонажем — типовий і прийнятний Unity-підхід.
- `Assets/GMTK/Prefabs/` — **плаский** каталог з 17 префабів без підпапок за категоріями (гравець/вороги/оточення/UI впереміш), з артефактами копіювання: `Bacteria (1).prefab` … `Bacteria (5).prefab` (автоінкрементні імена Unity, ніколи не перейменовані), `Cucmber.prefab` (одруківка, не узгоджена з `CucumberBullet.prefab` і класом `CucumberVirus.cs`).
- `Assets/GMTK/Textures/` — ~91 МБ, найбільша категорія (прийнятно для рукомальованого 2D, але варто переглянути стиснення/атласи при оптимізації розміру білда).
- `Assets/Resources/` — тільки налаштування DOTween (добра практика, Resources майже не зловживають).
- `ResourceManager.cs` (сервіс-обгортка над `Resources.Load<T>`) — зареєстрований, але жодного виклику `ResourceManager.Load` в геймплейному коді не знайдено — вестигіальна абстракція.

## 4. Фізика й шари

Кастомні фізичні шари (`TagManager.asset`): `Ally`, `Enemy`, `InsideCell`, `PostProcessing`, `Cell`, `BulletMacro`, `BulletMicro`, `InsideCellPlayer`, `Barier`. Кастомних тегів немає (`tags: []`) — ідентифікація об'єктів через `GetComponent`/`TryGetComponent`, узгоджено з патерном `IDamageable`.

Індекс збірки сцен є **логічно значущим** (не лише порядком): `AudioMixerController` перевіряє `buildIndex == 1`/`== 0`, `MenuManager`/`HUD` жорстко викликають `SceneManager.LoadScene(0/1)` — переставити сцени в Build Settings мовчки зламає аудіо-логіку й флоу меню.

## 5. Незавершені/вирізані елементи

- Жодних `TODO`/`FIXME`/`HACK` коментарів у всьому коді.
- 3 закоментовані рядки: вимкнене повернення `Rigidbody2D.isKinematic = false` після переходу масштабу (двічі), і порожній `OnDrawGizmos()` із закоментованим `Gizmos.DrawSphere`.
- Заявлена в readme поведінка "Warrior Virus" (зграйна координація) — не реалізована в коді.
- `PlayerStats._debugLevels` — мертве поле, записується, але ніколи не читається.
- `PlayerDamageable._maxHealth` серіалізований, але ігнорується для стартового значення здоров'я (хардкод `3`) — ймовірна неузгодженість.
- Немає екрана перемоги/фіналу — або свідомий нескінченний режим, або урізана фіча через дедлайн (див. GDD п.11).

## 6. Підсумкова оцінка

Для 96-годинного джему код **на диво чистий і послідовний**: єдиний свідомий (хоч і застарілий за сучасними мірками) архітектурний вибір — Service Locator замість DI, послідовне групування скриптів за фічею, гарний Strategy-патерн для ШІ, відсутність `FindObjectOfType`. Головні технічні борги, які варто закрити при переносі на новий шаблон:

1. Service Locator → повноцінний DI (Zenject) — прибирає приховані залежності порядку виконання.
2. Крихкі `transform.Find`/`GetChild` → `[SerializeField]`-посилання або `ComponentRegistry`.
3. Магічні числа → ScriptableObject-конфіги (криві складності, баланс).
4. Корутини → UniTask (узгодженість з цільовим шаблоном, кращий контроль скасування).
5. `MonoBehaviour`-орієнтований рух/AI без Burst/ECS — не є проблемою для такого масштабу гри, ECS не потрібен.
6. Гігієна асетів: перейменувати `Bacteria (1-5).prefab`, `Cucmber.prefab`; впровадити підпапки в `Prefabs/`.

Детальний план переносу — у [REFACTORING_PLAN.md](REFACTORING_PLAN.md).
