namespace WOTRMultiplayer.Networking
{
    public enum NetworkErrorType
    {
        Generic,
        Disconnected,
        SocketError,

        UnreachableSignalingServer,
        GameHostUnavailable,
        GameNotFound,

        P2PTimeout
    }
}
