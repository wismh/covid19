using System;
using Project.Core.SaveServiceModule;
using Project.Core.SaveServiceModule.Migrations;
using Project.Core.SaveServiceModule.Serialization;
using Zenject;

namespace _Project.Save
{
    /// <summary>
    /// Wires _Lumenwake's generic Save module for <see cref="GameSaveState"/>, following the exact
    /// static-<c>Install(DiContainer)</c> <c>Installer&lt;T&gt;</c> pattern already used by
    /// <c>AssetLoaderInstaller</c>/<c>AudioSystemInstaller</c>/<c>SceneLoaderServiceModuleInstaller</c> (all
    /// under Assets/_Lumenwake), called from <see cref="_Project.Bootstrap.ProjectContextInstaller"/>. This is
    /// the first `_Project`-side module installer beyond ProjectContextInstaller itself.
    ///
    /// --- Binding shape, read directly from SaveServiceModule's source (not guessed) ---
    /// <c>Project.Core.SaveServiceModule.SaveServiceModuleInstaller</c> (the generic, project-agnostic
    /// installer shipped in _Lumenwake) only binds <see cref="ISerializer"/> - it does NOT bind
    /// <c>ISaveService&lt;TState&gt;</c> itself, because that binding is necessarily per-state-type (the file
    /// path, target schema version, and default-state factory are all specific to one save model). So this
    /// installer calls the generic installer first for the shared serializer, then adds the
    /// <c>GameSaveState</c>-specific pieces on top: a <see cref="SaveMigrationRunner"/> (currently empty -
    /// GameSaveState has no prior schema version to migrate from, see <see cref="CurrentSaveVersion"/>'s doc
    /// comment) and the concrete <c>SaveService&lt;GameSaveState&gt;</c>, both via <c>FromMethod</c> so they can
    /// resolve their own dependencies (<see cref="ISerializer"/>, <see cref="SaveMigrationRunner"/>) out of the
    /// container explicitly rather than relying on Zenject's implicit collection-constructor-injection for
    /// <c>SaveMigrationRunner</c>'s <c>IEnumerable&lt;ISaveMigration&gt;</c> parameter (no <c>ISaveMigration</c>
    /// is bound anywhere yet, and this sidesteps needing to verify whether Zenject's constructor injection
    /// resolves an untyped/unbound <c>IEnumerable&lt;T&gt;</c> parameter to an empty collection or throws - see
    /// this phase's final report for that specific uncertainty).
    /// </summary>
    public sealed class GameSaveModuleInstaller : Installer<GameSaveModuleInstaller>
    {
        /// <summary>
        /// Save schema version, written to the JSON "Version" key by SaveJsonVersion/read back by
        /// SaveMigrationRunner on load. GameSaveState is a brand-new save model (GDD.md section 11: the old
        /// GMTK game never had a save system) - there is no version 0 or prior schema to migrate from, so this
        /// starts at 1 and <see cref="SaveMigrationRunner"/> below is constructed with zero migrations. The next
        /// time GameSaveState's shape changes in a way existing save files can't just silently absorb (a field
        /// rename/removal, not an additive field with a sensible default), bump this to 2 and add an
        /// <see cref="ISaveMigration"/> implementation for the 1-to-2 step.
        /// </summary>
        public const int CurrentSaveVersion = 1;

        public override void InstallBindings()
        {
            // Shared serializer (Newtonsoft.Json wrapper) - project-agnostic, shipped as-is in _Lumenwake.
            SaveServiceModuleInstaller.Install(Container);

            Container.Bind<SaveMigrationRunner>()
                .FromMethod(CreateMigrationRunner)
                .AsSingle();

            Container.Bind<ISaveService<GameSaveState>>()
                .FromMethod(CreateSaveService)
                .AsSingle();

            // IGameSaveService is the only type gameplay/UI code should depend on - see its own doc comment for
            // why the raw ISaveService<GameSaveState>/GameSaveState are not exposed further than this installer
            // and GameSaveService itself. BindInterfacesAndSelfTo also picks up IInitializable/IDisposable (see
            // GameSaveService's class doc comment for what those do), matching AudioSystemInstaller's own
            // BindInterfacesAndSelfTo<AudioSystem>() pattern one file up in _Lumenwake.
            Container.BindInterfacesAndSelfTo<GameSaveService>().AsSingle();

            // See SaveTrigger's own doc comment for exactly what this does and does not cover.
            Container.BindInterfacesAndSelfTo<SaveTrigger>().AsSingle();
        }

        private static SaveMigrationRunner CreateMigrationRunner(InjectContext _)
        {
            return new SaveMigrationRunner(Array.Empty<ISaveMigration>(), CurrentSaveVersion);
        }

        private static ISaveService<GameSaveState> CreateSaveService(InjectContext ctx)
        {
            var serializer = ctx.Container.Resolve<ISerializer>();
            var migrationRunner = ctx.Container.Resolve<SaveMigrationRunner>();

            // filePath intentionally left null: SaveService<TState> itself defaults to
            // Path.Combine(Application.persistentDataPath, "save.json") when none is supplied (see
            // SaveService.cs's constructor) - reusing that default keeps this installer from duplicating path
            // logic the module already owns, and "save.json" is an accurate, unambiguous name for a project
            // with exactly one save file.
            return new SaveService<GameSaveState>(
                serializer,
                migrationRunner,
                CurrentSaveVersion,
                setVersion: (state, version) => state.Version = version,
                filePath: null,
                createDefault: () => new GameSaveState());
        }
    }
}
