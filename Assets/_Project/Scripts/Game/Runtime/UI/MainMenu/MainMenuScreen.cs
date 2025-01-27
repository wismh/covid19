using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lumenwake.UIModule;
using Project.Core.AudioSystem;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using _Project.Mechanics.AppFlow;

namespace _Project.UI.MainMenu
{
    public class MainMenuScreen : BaseScreen
    {
        [SerializeField] private Image _fadeInPanel;
        [SerializeField] private SoundData _startSound;
        [SerializeField] private Image _humanImage;
        [SerializeField] private Sprite _skeletonWhiteSprite;
        [SerializeField] private Sprite _skeletonBlackSprite;

        private const float SkeletonFlashDuration = 0.2f;

        private GlobalGameStateMachine _stateMachine;
        private IAudioSystem _audioSystem;
        private bool _isPlaying;

        [Inject]
        public void Construct(GlobalGameStateMachine stateMachine, IAudioSystem audioSystem)
        {
            _stateMachine = stateMachine;
            _audioSystem = audioSystem;
        }

        private void Start()
        {
            PlayFadeIn();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            PlayFadeIn();
        }

        private void OnDestroy()
        {
            DOTween.Kill(_fadeInPanel);
        }

        private void PlayFadeIn()
        {
            DOTween.To(
                () => _fadeInPanel.color.a,
                value => _fadeInPanel.color = new Color(_fadeInPanel.color.r, _fadeInPanel.color.g, _fadeInPanel.color.b, value),
                0.0f,
                0.3f
            ).SetTarget(_fadeInPanel);
        }

        public void Play()
        {
            if (_isPlaying)
                return;

            PlayAsync().Forget();
        }

        private async UniTaskVoid PlayAsync()
        {
            _isPlaying = true;

            _audioSystem.PlaySfx(_startSound);

            _humanImage.sprite = _skeletonWhiteSprite;
            await UniTask.Delay(TimeSpan.FromSeconds(SkeletonFlashDuration), cancellationToken: destroyCancellationToken);

            _humanImage.sprite = _skeletonBlackSprite;
            await UniTask.Delay(TimeSpan.FromSeconds(SkeletonFlashDuration), cancellationToken: destroyCancellationToken);

            DOTween.To(
                () => _fadeInPanel.color.a,
                value => _fadeInPanel.color = new Color(_fadeInPanel.color.r, _fadeInPanel.color.g, _fadeInPanel.color.b, value),
                1.0f,
                0.3f
            ).SetTarget(_fadeInPanel);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: destroyCancellationToken);

            EnterGameplay();
        }

        private void EnterGameplay()
        {
            _stateMachine.Enter<GameplayState>().Forget();
        }

        public void Exit()
        {
            Application.Quit();
        }
    }
}
