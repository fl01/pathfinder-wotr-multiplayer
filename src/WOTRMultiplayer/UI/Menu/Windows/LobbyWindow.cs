using System;
using System.Collections.Concurrent;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.UI.Menu.Windows
{
    public class LobbyWindow : UIWindow
    {
        private ILogger<LobbyWindow> _logger;

        public ConcurrentQueue<Action> MainThreadQueue { get; } = new ConcurrentQueue<Action>();

        public Action OnClose { get; set; }

        public void SetLogger(ILogger<LobbyWindow> logger)
        {
            _logger = logger;
        }

        public override void OnHide()
        {
            _logger.LogInformation("OnHide");
            Game.Instance.UI.EscManager.Unsubscribe(Close);
        }

        public override void OnShow()
        {
            Game.Instance.UI.EscManager.Subscribe(Close);
            _logger.LogInformation("OnShow");
        }

        private void Close()
        {
            _logger.LogInformation("Close lobby window");
            OnClose?.Invoke();
            this.Show(false);
        }

        void Update()
        {
            while (MainThreadQueue.TryDequeue(out var action))
            {
                _logger.LogInformation("Executing action. RemainingActionsCount={remainingActionsCount}", MainThreadQueue.Count);
                action();
            }
        }
    }
}
