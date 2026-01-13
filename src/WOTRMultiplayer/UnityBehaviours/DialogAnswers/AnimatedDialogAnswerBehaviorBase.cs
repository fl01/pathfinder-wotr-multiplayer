using System;
using DG.Tweening;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;

namespace WOTRMultiplayer.UnityBehaviours.DialogAnswers
{
    public abstract class AnimatedDialogAnswerBehaviorBase : DecayingBehaviourBase
    {
        private DialogAnswerPCView _view;
        private Action _onExpired;

        protected float Duration { get; private set; }

        public void Begin(float duration, Action onExpired)
        {
            Duration = duration;
            _onExpired = onExpired;

            base.Begin(TimeSpan.FromSeconds(duration));
        }

        private void Awake()
        {
            _view = GetComponent<DialogAnswerPCView>();
        }

        protected override void OnPartialDecay(float decayState)
        {
        }

        protected override void OnStarted()
        {
            _view.Button.Interactable = false;

            this.transform.DOKill();
        }

        protected override void OnExpired()
        {
            _view.Button.Interactable = true;
            _onExpired?.Invoke();
            DestroyImmediate(this);
        }
    }
}
