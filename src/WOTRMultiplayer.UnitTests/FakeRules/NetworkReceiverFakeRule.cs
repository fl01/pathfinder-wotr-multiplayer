using FakeItEasy.Core;

namespace WOTRMultiplayer.UnitTests.FakeRules
{
    public class NetworkReceiverFakeRule<T> : IFakeObjectCallRule
    {
        public int? NumberOfTimesToCall => null;

        public void Apply(IInterceptedFakeObjectCall fakeObjectCall)
        {
            fakeObjectCall.SetReturnValue(fakeObjectCall.FakedObject);
        }

        public bool IsApplicableTo(IFakeObjectCall fakeObjectCall)
        {
            var isApplicable = fakeObjectCall.Method.DeclaringType == typeof(T) && fakeObjectCall.Method.Name == "On";
            return isApplicable;
        }
    }
}
