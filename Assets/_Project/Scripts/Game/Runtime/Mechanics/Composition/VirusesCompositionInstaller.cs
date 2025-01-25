using UnityEngine;
using Zenject;
using _Project.Mechanics.Combat;
using _Project.Mechanics.Viruses.Macro;
using _Project.Mechanics.Viruses.Micro;
using _Project.Mechanics.Waves;

namespace _Project.Mechanics.Composition
{
    public class VirusesCompositionInstaller : MonoInstaller
    {
        [Header("Micro viruses")]
        [SerializeField] private MicroVirusController _orangeVirusPrefab;
        [SerializeField] private MicroVirusController _cucumberVirusPrefab;
        [SerializeField] private MicroVirusController _spinerVirusPrefab;
        [SerializeField] private VirusBullet _virusBulletPrefab;

        [Header("Macro viruses")]
        [SerializeField] private MacroVirusConfig _macroVirusConfig;
        [SerializeField] private MacroVirusController _macroVirusPrefab;
        [SerializeField] private MacroVirusController _macroGlistPrefab;

        [Header("Waves")]
        [SerializeField] private WaveScalingConfig _waveScalingConfig;

        // Shared with PlayerHealth too (see PlayerCompositionInstaller) - bound once here since Zenject rejects
        // binding the same type twice in one container.
        [Header("Shared combat presentation")]
        [SerializeField] private DamageDeathPresentationConfig _damageDeathPresentation;

        public override void InstallBindings()
        {
            Container.Bind<DamageDeathPresentationConfig>().FromInstance(_damageDeathPresentation).AsSingle();

            MicroVirusesInstaller.Install(Container, _orangeVirusPrefab, _cucumberVirusPrefab, _spinerVirusPrefab, _virusBulletPrefab);
            VirusesInstaller.Install(Container, _macroVirusConfig, _waveScalingConfig, _macroVirusPrefab, _macroGlistPrefab);
        }
    }
}
