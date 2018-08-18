using System;
using System.Threading;
using System.Threading.Tasks;

namespace Secs4Net.Sml
{
    public static class AsyncHelper
    {
        private static readonly TaskFactory MyTaskFactory =
            new TaskFactory(CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

        public static TResult RunSync<TResult>(Func<Task<TResult>> func)
        {
            return MyTaskFactory
              .StartNew(func)
              .Unwrap()
              .GetAwaiter()
              .GetResult();
        }

        public static TResult RunSync<TResult>(Func<object, Task<TResult>> func, object arg0)
        {
            return MyTaskFactory
                .StartNew(func, arg0)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }

        public static void RunSync(Func<Task> func)
        {
            MyTaskFactory
              .StartNew(func)
              .Unwrap()
              .GetAwaiter()
              .GetResult();
        }

        public static void RunAsync(Func<Task> func)
        {
            MyTaskFactory
              .StartNew(func)
              .Unwrap();
        }
    }
}
