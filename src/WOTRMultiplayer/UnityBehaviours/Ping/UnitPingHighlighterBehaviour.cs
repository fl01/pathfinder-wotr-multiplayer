using Owlcat.Runtime.Visual.RenderPipeline.RendererFeatures.Highlighting;
using UnityEngine;

namespace WOTRMultiplayer.UnityBehaviours.Ping
{
    public class UnitPingHighlighterBehaviour : DecayingBehaviourBase
    {
        private Highlighter _highlighter;

        private void Awake()
        {
            _highlighter = this.GetComponent<Highlighter>();
        }

        protected override void OnStarted()
        {
            if (_highlighter != null)
            {
                _highlighter.ConstantOn(Color.blue, 0f);
                _highlighter.FlashingOn(Color.blue, Color.yellow, 1.75f);
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
    }
}
