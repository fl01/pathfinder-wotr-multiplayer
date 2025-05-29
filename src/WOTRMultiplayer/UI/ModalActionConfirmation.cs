using Kingmaker.UI;

namespace WOTRMultiplayer.UI
{
    public class ModalActionConfirmation
    {
        public string Text { get; set; }

        public MessageModalBase.ModalType ModalType { get; set; } = MessageModalBase.ModalType.Dialog;
    }
}
