using System;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours
{
    public abstract class DecayingBehaviourBase : MonoBehaviour
    {
        private TimeSpan? _expiration;
        private Action<GameObject> _onExpired;
        private DateTimeOffset _startedAt;

        public void Begin(TimeSpan expiration, Action<GameObject> onExpired)
        {
            _expiration = expiration;
            _onExpired = onExpired;
            RefreshDuration();
            OnStarted();
        }

        public void RefreshDuration()
        {
            _startedAt = DateTime.UtcNow;
        }

        protected abstract void OnPartialDecay(float decayState);

        protected virtual void OnStarted()
        {
            base.gameObject.SetActive(true);
        }

        protected virtual void OnExpired()
        {
            base.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_expiration == null)
            {
                return;
            }

            var decay = (DateTime.UtcNow - _startedAt).TotalMilliseconds / _expiration.Value.TotalMilliseconds;
            var decayState = decay < float.MinValue || decay > float.MaxValue ? 1f : (float)decay;
            if (decayState >= 1f)
            {
                _expiration = null;
                OnExpired();
                _onExpired?.Invoke(this.gameObject);
                return;
            }

            OnPartialDecay(decayState);
        }
    }
}
