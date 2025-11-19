using Kingmaker.UI;

namespace WOTRMultiplayer.UI
{
    public class ModalActionConfirmation
    {
        public string MessageKey { get; set; }

        public MessageModalBase.ModalType ModalType { get; set; } = MessageModalBase.ModalType.Dialog;
    }
}
