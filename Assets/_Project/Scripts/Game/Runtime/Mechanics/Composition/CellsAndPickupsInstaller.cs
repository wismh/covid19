using UnityEngine;
using Zenject;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Pickups;

namespace _Project.Mechanics.Composition
{
    public class CellsAndPickupsInstaller : MonoInstaller
    {
        [Header("Cells")]
        [SerializeField] private CellConfig _cellConfig;

        [Header("Pickups")]
        [SerializeField] private PickupsConfig _pickupsConfig;
        [SerializeField] private MedicalPickup _medicalPrefab;

        public override void InstallBindings()
        {
            CellsInstaller.Install(Container, _cellConfig);
            PickupsInstaller.Install(Container, _pickupsConfig, _medicalPrefab);
        }
    }
}
