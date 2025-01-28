using UnityEngine;
using _Project.Platform;

namespace _Project.UI
{
    /// <summary>Shows the on-screen touch controls canvas only on touch platforms.</summary>
    public sealed class TouchControlsVisibility : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;

        private void Awake()
        {
            _canvas.enabled = TouchPlatform.IsTouchPlatform;
        }
    }
}
