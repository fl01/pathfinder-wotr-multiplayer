using System;
using Owlcat.Runtime.Visual.RenderPipeline.RendererFeatures.Highlighting;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours.Ping
{
    public class WorldEntityHighlighterBehaviour : DecayingBehaviourBase
    {
        private Highlighter _highlighter;
        private bool _isEnemy;

        public void Begin(bool isEnemy, TimeSpan duration)
        {
            _isEnemy = isEnemy;
            Begin(duration);
        }

        protected override void OnStarted()
        {
            if (_highlighter != null)
            {
                var firstColor = _isEnemy ? Color.red : Color.blue;
                var secondColor = _isEnemy ? Color.white : Color.yellow;
                _highlighter.ConstantOn(firstColor, 0f);
                _highlighter.FlashingOn(firstColor, secondColor, 1.75f);
            }
        }

        protected override void OnExpired()
        {
            _highlighter?.FlashingOff();
            _highlighter?.ConstantOff();
            UnityEngine.Object.DestroyImmediate(this);
        }

        protected override void OnPartialDecay(float decayState)
        {
        }

        private void Awake()
        {
            _highlighter = this.GetComponent<Highlighter>();
        }
    }
}
