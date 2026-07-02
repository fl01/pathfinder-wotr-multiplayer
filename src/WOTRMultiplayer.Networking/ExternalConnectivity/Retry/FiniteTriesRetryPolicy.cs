using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.Retry
{
    public class FiniteTriesRetryPolicy : IRetryPolicy
    {
        public int RetryCount { get; set; }

        public TimeSpan Delay { get; set; }

        public FiniteTriesRetryPolicy(int retryCount, TimeSpan delay)
        {
            RetryCount = Math.Max(0, retryCount);
            Delay = delay;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount >= RetryCount)
            {
                return null;
            }

            return Delay;
        }
    }
}
