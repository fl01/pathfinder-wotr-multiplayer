using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours
{
    public class DecayingMeshRenderersBehaviour : DecayingBehaviourBase
    {
        private List<MeshRenderer> _renderers;

        public void Initialize(TimeSpan expiration, Action<GameObject> onExpired, List<MeshRenderer> renderers)
        {
            _renderers = renderers;

            base.Initialize(expiration, onExpired);
        }

        protected override void OnPartialDecay(float decayState)
        {
            var transparency = Math.Max(0f, 1f - decayState + 0.25f);

            foreach (var renderer in _renderers)
            {
                var color = renderer.material.color;
                color.a = transparency;
                renderer.material.color = color;
            }
        }
    }
}
