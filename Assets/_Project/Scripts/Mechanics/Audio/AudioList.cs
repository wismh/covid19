using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Audio
{
    public class AudioList : MonoBehaviour
    {
        [SerializeField] private List<AudioClip> _clips;
        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        public void Play(int i)
        {
            if (_source == null) _source = GetComponent<AudioSource>();
            if (i >= 0 && i < _clips.Count && _source != null)
            {
                _source.clip = _clips[i];
                _source.Play();
            }
        }
    }
}
