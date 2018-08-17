namespace Secs4Net
{
    interface ITimer
    {
        int Interval { get; }
        void Start();
        void Stop();
    }
}
