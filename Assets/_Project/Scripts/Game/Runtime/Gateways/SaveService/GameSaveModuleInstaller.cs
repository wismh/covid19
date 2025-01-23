using System;
using _Project.Entities.Permanent;
using Project.Core.SaveServiceModule;
using Project.Core.SaveServiceModule.Migrations;
using Project.Core.SaveServiceModule.Serialization;
using Zenject;

namespace _Project.Gateways.SaveService
{
    /// <summary>Wires the generic Save module for <see cref="GameSaveState"/>. The generic
    /// <c>SaveServiceModuleInstaller</c> only binds <see cref="ISerializer"/> (project-agnostic); this installer
    /// adds the <c>GameSaveState</c>-specific <see cref="SaveMigrationRunner"/> and
    /// <c>SaveService&lt;GameSaveState&gt;</c> on top.</summary>
    public sealed class GameSaveModuleInstaller : Installer<GameSaveModuleInstaller>
    {
        /// <summary>Bump when GameSaveState's shape changes in a way existing save files can't silently absorb
        /// (a field rename/removal, not an additive field with a default), and add an
        /// <see cref="ISaveMigration"/> for the version step.</summary>
        public const int CurrentSaveVersion = 1;

        public override void InstallBindings()
        {
            SaveServiceModuleInstaller.Install(Container);

            Container.Bind<SaveMigrationRunner>()
                .FromMethod(CreateMigrationRunner)
                .AsSingle();

            Container.Bind<ISaveService<GameSaveState>>()
                .FromMethod(CreateSaveService)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<GameSaveService>().AsSingle();
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

            // filePath left null: SaveService<TState> defaults to Application.persistentDataPath/save.json.
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
