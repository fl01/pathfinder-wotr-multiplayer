namespace WOTRMultiplayer.Entities.Rolls.Claiming.Values
{
    public abstract class TypedRollValueBase<TValue> : RollValueBase
    {
        public new TValue Value
        {
            get
            {
                return (TValue)base.Value;
            }
            set
            {
                base.Value = value;
            }
        }
    }
}
