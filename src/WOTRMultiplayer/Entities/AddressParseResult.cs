namespace WOTRMultiplayer.Entities
{
    public class AddressParseResult
    {
        public bool IsOk { get; set; }

        public string MessageKey { get; set; }

        public static AddressParseResult Error(string messageKey)
        {
            return new AddressParseResult
            {
                MessageKey = messageKey,
                IsOk = false
            };
        }

        public static AddressParseResult Ok()
        {
            return new AddressParseResult
            {
                IsOk = true
            };
        }
    }
}
