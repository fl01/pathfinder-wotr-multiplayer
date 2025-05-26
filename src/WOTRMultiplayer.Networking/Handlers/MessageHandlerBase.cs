namespace WOTRMultiplayer.Networking.Handlers
{
    public abstract class MessageHandlerBase<TMessage> : IMessageHandler<TMessage>
    {
        protected abstract void HandleInternal(TMessage message);

        public void Handle(TMessage message)
        {
            try
            {
                HandleInternal(message);
            }
            catch (System.Exception ex)
            {
            }
        }
    }
}
