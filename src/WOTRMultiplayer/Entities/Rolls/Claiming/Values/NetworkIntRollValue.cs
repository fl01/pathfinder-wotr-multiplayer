namespace WOTRMultiplayer.Entities.Rolls.Claiming.Values
{
    public class NetworkIntRollValue : TypedRollValueBase<int>
    {
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
