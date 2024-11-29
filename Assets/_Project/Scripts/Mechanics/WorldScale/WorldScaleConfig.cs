using UnityEngine;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Every tunable duration/scale/orthographic-size number found in GMTK.GameStates.TransitionIntoMacroWorld
    /// and GMTK.GameStates.TransitionIntoMicroWorld (Assets/GMTK/Scripts/GameStates/TransitionIntoMacroWorld.cs,
    /// TransitionIntoMicroWorld.cs), plus one value pulled from GMTK.CellController (see the cell-physics header
    /// below) that those transitions call into but that Phase 3 never ported.
    ///
    /// Scene-serialized-override check (per REFACTORING_PLAN.md Phase 3's precedent of reading `_distanceThreshold`
    /// from the scene rather than trusting C# field-initializer defaults): NEITHER TransitionIntoMacroWorld NOR
    /// TransitionIntoMicroWorld is a MonoBehaviour - both are plain C# classes instantiated with `new
    /// TransitionIntoMicroWorld().Start(this)` from GMTK.GameStates.GamePlayState.State's setter (GameplayState.cs
    /// :43-44), never attached to a GameObject. Confirmed by grepping Assets/GMTK/Scenes/SampleScene.unity for
    /// both class names - zero matches. So there is no scene-serialized component instance to check for overrides;
    /// every number below is a C# literal read directly from the two class bodies (or, for `_cellMacroMaxVelocity`,
    /// from Assets/GMTK/Prefabs/Cell.prefab's serialized CellController component), not a class-default that a
    /// scene inspector value might have silently overridden.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/WorldScale/" + nameof(WorldScaleConfig),
        fileName = nameof(WorldScaleConfig) + "_Default")]
    public sealed class WorldScaleConfig : ScriptableObject
    {
        [Header("Transition timing")]
        // Old: both TransitionIntoMacroWorld and TransitionIntoMicroWorld declare their own private
        // `const float k_transitionDirection = 1f` and pass it as the duration to all four DOTween.To(...) calls
        // in the file (scale, camera orthographic size, camera rotation, position) - despite the name, it is a
        // duration in seconds, not a direction; both classes' constants hold the identical value 1f, so this is a
        // single shared number here rather than two.
        [SerializeField] private float _transitionDurationSeconds = 1f;

        // Old: neither class ever calls `.SetEase(...)` on any of its DOTween.To(...) tweens, so every tween
        // inherits DOTween's project-wide default ease, which is set (not DOTween's own hardcoded fallback) by
        // Assets/Resources/DOTweenSettings.asset:31 `defaultEaseType: 6` = DG.Tweening.Ease.OutQuad (verified via
        // .NET reflection over DOTween.dll's Ease enum - ordinal 6 is OutQuad, not InOutQuad as the name alone
        // might suggest). This is a global engine setting, not a per-transition tunable, so it is intentionally
        // NOT duplicated as a field here - WorldScaleTransition likewise never calls .SetEase(...), so it
        // inherits the same project default exactly like the old code did (see WorldScaleTransition's class doc
        // comment).

        [Header("Player scale (old: DOTween'd root `_player.transform.localScale` AND an instantly-set nested " +
                "visual `_player.GetChild(0).GetChild(0).localScale`, both to the same target vector)")]
        [SerializeField] private Vector3 _macroPlayerScale = new(1f, 1f, 1f);
        [SerializeField] private Vector3 _microPlayerScale = new(0.3f, 0.3f, 1f);

        [Header("Camera (old: Camera.main.orthographicSize DOTween'd in both files; camera rotation.z DOTween'd " +
                "to the literal 0 for macro, but to the entered cell's own CURRENT rotation.z for micro - that " +
                "target is a runtime value read at call time, not a configurable number, so it has no field here)")]
        [SerializeField] private float _macroCameraOrthographicSize = 5f;
        [SerializeField] private float _microCameraOrthographicSize = 1.9f;

        [Header("Macro exit placement (old: TransitionIntoMacroWorld - `var outsidePosition = _player.position + " +
                "_player.right * 5`, flipped to `- _player.right * 5` if a Physics2D.Linecast from the player to " +
                "that point hits the \"Barier\" layer)")]
        [SerializeField] private float _macroExitOffsetDistance = 5f;

        [Header("Player audio (old: TransitionIntoMicroWorld ONLY - `_player.GetComponent<AudioSource>().volume " +
                "= 0.4f` at the start, restored to `= 1` in the position tween's OnComplete. " +
                "TransitionIntoMacroWorld never touches the player's AudioSource.volume at all - this asymmetry " +
                "is preserved, not a copy/paste omission on my part)")]
        [SerializeField] private float _microPlayerAudioVolume = 0.4f;
        [SerializeField] private float _restoredPlayerAudioVolume = 1f;

        [Header("Cell physics (old: GMTK.CellController.SetVelocity(WorldState.MACRO_WORLD), called BY both " +
                "transitions via `cellController.SetVelocity(...)` - `Random.Range(-_maxVelocity.x, _maxVelocity.x)` " +
                "etc, using a serialized `_maxVelocity` field that lived on the CellController component PER CELL " +
                "PREFAB INSTANCE, not a global. Phase 3 never ported CellController.SetVelocity or `_maxVelocity` " +
                "onto CellHealth/CellInfectionState/CellVisuals (none of them touch Rigidbody2D at all), so there " +
                "is no existing _Project config field to read this from. Value below is read directly from " +
                "Assets/GMTK/Prefabs/Cell.prefab's serialized CellController component (`_maxVelocity: {x: 0.6, " +
                "y: 0.6}`) and used here as the single representative default; if a future cell-registry phase " +
                "finds per-cell-type velocity variance matters, this belongs to a per-cell config instead)")]
        [SerializeField] private Vector2 _cellMacroMaxVelocity = new(0.6f, 0.6f);

        public float TransitionDurationSeconds => _transitionDurationSeconds;

        public Vector3 MacroPlayerScale => _macroPlayerScale;
        public Vector3 MicroPlayerScale => _microPlayerScale;

        public float MacroCameraOrthographicSize => _macroCameraOrthographicSize;
        public float MicroCameraOrthographicSize => _microCameraOrthographicSize;

        public float MacroExitOffsetDistance => _macroExitOffsetDistance;

        public float MicroPlayerAudioVolume => _microPlayerAudioVolume;
        public float RestoredPlayerAudioVolume => _restoredPlayerAudioVolume;

        public Vector2 CellMacroMaxVelocity => _cellMacroMaxVelocity;
    }
}
