namespace WOTRMultiplayer.Networking.Handlers
{
    public interface IMessageHandler<TMessage>
    {
        void Handle(TMessage message);
    }
}
