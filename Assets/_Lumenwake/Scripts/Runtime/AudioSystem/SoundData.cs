using UnityEngine;

namespace Project.Core.AudioSystem
{
    [CreateAssetMenu(
        menuName = "Lumenwake/Audio/" + nameof(SoundData),
        fileName = nameof(SoundData),
        order = 0)]
    public class SoundData : ScriptableObject
    {
        [SerializeField] private AudioClip _clip;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        [SerializeField] private Vector2 _pitchRange = Vector2.one;

        public AudioClip Clip => _clip;

        public float Volume => _volume;

        public Vector2 PitchRange => _pitchRange;
    }
}
