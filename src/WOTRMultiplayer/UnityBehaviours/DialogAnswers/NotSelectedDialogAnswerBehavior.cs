using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Services.GameInteraction;

namespace WOTRMultiplayer.UnityBehaviours.DialogAnswers
{
    public class NotSelectedDialogAnswerBehavior : AnimatedDialogAnswerBehaviorBase
    {
        protected override void OnStart()
        {
            base.OnStart();
            this.gameObject.GetComponentInChildren<TextMeshProUGUI>().DOFade(0.1f, Duration);
        }

        protected override void OnPartialDecay(float decayState)
        {
            var transparency = Math.Max(0f, 1f - decayState);

            var suggestions = GetSuggestionIcons().ToList();
            foreach (var suggestion in suggestions)
            {
                var color = suggestion.color;
                color.a = transparency;
                suggestion.color = color;
            }
        }

        private IEnumerable<Image> GetSuggestionIcons()
        {
            foreach (Transform child in this.transform)
            {
                if (child.name.StartsWith(DialogInteractionService.SuggestionIconObjectPrefix))
                {
                    yield return child.GetComponent<Image>();
                }
            }
        }
    }
}
