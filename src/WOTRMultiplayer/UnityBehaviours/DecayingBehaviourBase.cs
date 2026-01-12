using System;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours
{
    public abstract class DecayingBehaviourBase : MonoBehaviour
    {
        private TimeSpan? _expiration;
        private Action<GameObject> _onExpired;
        private DateTime _startedAt;

        public void Initialize(TimeSpan expiration, Action<GameObject> onExpired)
        {
            _expiration = expiration;
            _onExpired = onExpired;
            _startedAt = DateTime.UtcNow;
            OnStart();
        }

        protected abstract void OnPartialDecay(float decayState);

        protected virtual void OnStart()
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
