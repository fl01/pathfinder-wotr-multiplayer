namespace WOTRMultiplayer.Networking.Configuration
{
    public class ExternalServerConfiguration
    {
        public ExternalServer Server { get; set; }

        public bool AutoCreateGame { get; set; }

        public string Password { get; set; }

        public int Port { get; set; }
    }
}
