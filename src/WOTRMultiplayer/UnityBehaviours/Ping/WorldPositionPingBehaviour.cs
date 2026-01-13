using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UI;
using Owlcat.Runtime.Core.Utils;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours.Ping
{
    public class WorldPositionPingBehaviour : DecayingBehaviourBase
    {
        private List<MeshRenderer> _renderers;

        public void Begin(TimeSpan expiration, Vector3 position)
        {
            transform.SetParent(ClickPointerManager.Instance.transform);
            transform.localPosition = position;

            Begin(expiration);
        }

        protected override void OnExpired()
        {
            DestroyImmediate(gameObject);
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

        private void Awake()
        {
            InitRenderers();
        }

        private void InitRenderers()
        {
            _renderers = [.. transform.Children().SelectMany(x => x.Children()).Select(x => x.GetComponent<MeshRenderer>())];
            var even = new Color(0f, 0f, 0.6f, 1f);
            var odd = new Color(0.887f, 0.273f, 0.263f, 1f);
            for (int i = 0; i < _renderers.Count; i++)
            {
                var color = i % 2 == 0 ? even : odd;
                var renderer = _renderers[i];
                renderer.material.color = color;
            }
        }
    }
}
