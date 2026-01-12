using DG.Tweening;

namespace WOTRMultiplayer.UnityBehaviours.DialogAnswers
{
    public class SelectedDialogAnswerBehavior : AnimatedDialogAnswerBehaviorBase
    {
        protected override void OnStart()
        {
            base.OnStart();
            this.transform.DOShakePosition(Duration, 1.5f, 15);
        }
    }
}
