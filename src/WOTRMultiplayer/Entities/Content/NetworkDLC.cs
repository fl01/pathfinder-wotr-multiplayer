namespace WOTRMultiplayer.Entities.Content
{
    public class NetworkDLC
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public bool IsAvailable { get; set; }

        public string FullName => string.IsNullOrEmpty(Title) ? Id : $"{Id} - {Title}";
    }
}
