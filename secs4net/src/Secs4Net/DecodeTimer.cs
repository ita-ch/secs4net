using System;
using System.Threading;

namespace Secs4Net
{
    internal sealed class DecodeTimer : ITimer, IDisposable
    {
        private readonly Timer _timer;

        public DecodeTimer(int t, TimerCallback timerCallback)
        {
            Interval = t;
            _timer = new Timer(timerCallback, default, Timeout.Infinite, Timeout.Infinite);
        }

        public int Interval { get; }

        public void Dispose() => _timer.Dispose();
        void ITimer.Start() => _timer.Change(Interval, Timeout.Infinite);
        void ITimer.Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }
}
