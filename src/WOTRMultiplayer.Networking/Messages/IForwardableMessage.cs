namespace WOTRMultiplayer.Networking.Messages
{
    /// <summary>
    /// There is no direct connection between clients, so server acts as a relay to forward the same notification to other clients (3+ players lobby)
    /// </summary>
    public interface IForwardableMessage
    {
    }
}
