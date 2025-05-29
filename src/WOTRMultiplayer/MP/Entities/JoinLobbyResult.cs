namespace WOTRMultiplayer.MP.Entities
{
    public class JoinLobbyResult
    {
        public bool IsOk { get; set; }

        public string Message { get; set; }

        public static JoinLobbyResult Error(string message)
        {
            return new JoinLobbyResult
            {
                Message = message,
                IsOk = false
            };
        }

        public static JoinLobbyResult Ok()
        {
            return new JoinLobbyResult
            {
                IsOk = true
            };
        }
    }
}
