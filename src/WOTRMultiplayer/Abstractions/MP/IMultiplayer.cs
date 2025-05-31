using System;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer : IDisposable
    {
        IUIFactory Factory { get; }

        bool InitializeMultiplayer(GameObject menuButtonToCopy, Transform parent);
    }
}
