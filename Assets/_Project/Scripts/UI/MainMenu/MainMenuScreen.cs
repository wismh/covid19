using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lumenwake.UIModule;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using Zenject;
using _Project.Mechanics.AppFlow;

namespace _Project.UI.MainMenu
{
    public class MainMenuScreen : BaseScreen
    {
        [SerializeField] private PlayableDirector _cutscene;
        [SerializeField] private Image _fadeInPanel;
        [SerializeField] private AudioSource _audioSource;

        private GlobalGameStateMachine _stateMachine;
        private bool _isPlaying;

        [Inject]
        public void Construct(GlobalGameStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        private void Awake()
        {
            if (_audioSource == null)
            {
                TryGetComponent(out _audioSource);
            }
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
            if (_fadeInPanel != null)
            {
                DOTween.Kill(_fadeInPanel);
            }
        }

        private void PlayFadeIn()
        {
            if (_fadeInPanel != null)
            {
                DOTween.To(
                    () => _fadeInPanel.color.a,
                    value => _fadeInPanel.color = new Color(_fadeInPanel.color.r, _fadeInPanel.color.g, _fadeInPanel.color.b, value),
                    0.0f,
                    0.3f
                ).SetTarget(_fadeInPanel);
            }
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

            if (_cutscene != null)
            {
                _cutscene.Play();
                if (_audioSource != null)
                {
                    _audioSource.Play();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: destroyCancellationToken);

                if (_fadeInPanel != null)
                {
                    DOTween.To(
                        () => _fadeInPanel.color.a,
                        value => _fadeInPanel.color = new Color(_fadeInPanel.color.r, _fadeInPanel.color.g, _fadeInPanel.color.b, value),
                        1.0f,
                        0.3f
                    ).SetTarget(_fadeInPanel);

                    await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: destroyCancellationToken);
                }

                EnterGameplay();
            }
            else
            {
                EnterGameplay();
            }
        }

        private void EnterGameplay()
        {
            if (_stateMachine != null)
            {
                _stateMachine.Enter<GameplayState>().Forget();
            }
            else
            {
                Debug.LogError("[MainMenuScreen] GlobalGameStateMachine could not be resolved.");
            }
        }

        public void Exit()
        {
            Application.Quit();
        }
    }
}
