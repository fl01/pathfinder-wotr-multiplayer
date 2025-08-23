using FakeItEasy.Core;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.UnitTests.FakeRules
{
    public class NetworkReceiverFakeRule : IFakeObjectCallRule
    {
        public int? NumberOfTimesToCall => null;

        public void Apply(IInterceptedFakeObjectCall fakeObjectCall)
        {
            fakeObjectCall.SetReturnValue(fakeObjectCall.FakedObject);
        }

        public bool IsApplicableTo(IFakeObjectCall fakeObjectCall)
        {
            var isApplicable = fakeObjectCall.Method.DeclaringType == typeof(INetworkReceiver) && fakeObjectCall.Method.Name == "On";
            return isApplicable;
        }
    }
}
