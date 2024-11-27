using System;
using System.Collections;
using UnityEngine;

namespace Project.Core.ApplicationLifecycle
{
    /// <summary>
    /// Application pause / focus / per-second tick — shared across Lumenwake titles.
    /// </summary>
    public class LifeController : MonoBehaviour
    {
        public event Action OnQuit;
        public event Action OnPause;
        public event Action OnResume;
        public event Action OnEverySecond;

        private void OnApplicationPause(bool pauseStatus)
        {
            OnPause?.Invoke();
        }

        private void OnApplicationQuit()
        {
            OnQuit?.Invoke();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                OnResume?.Invoke();
        }

        private void Start()
        {
            StartCoroutine(EverySecondCoroutine());
        }

        private IEnumerator EverySecondCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                OnEverySecond?.Invoke();
            }
        }
    }
}
