namespace Aliceestrap.Utility
{
    public sealed class AsyncMutex : IAsyncDisposable
    {
        private readonly bool _initiallyOwned;
        private readonly string _name;
        private Task? _mutexTask;
        private ManualResetEventSlim? _releaseEvent;
        private CancellationTokenSource? _cancellationTokenSource;

        public AsyncMutex(bool initiallyOwned, string name)
        {
            _initiallyOwned = initiallyOwned;
            _name = name;
        }

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource taskCompletionSource = new();

            _releaseEvent = new ManualResetEventSlim();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _mutexTask = Task.Factory.StartNew(
                state =>
                {
                    try
                    {
                        CancellationToken cancellationToken = _cancellationTokenSource.Token;
                        using var mutex = new Mutex(_initiallyOwned, _name);
                        try
                        {
                            if (WaitHandle.WaitAny(new[] { mutex, cancellationToken.WaitHandle }) != 0)
                            {
                                taskCompletionSource.SetCanceled(cancellationToken);
                                return;
                            }
                        }
                        catch (AbandonedMutexException)
                        {
                        }

                        taskCompletionSource.SetResult();

                        _releaseEvent.Wait();

                        mutex.ReleaseMutex();
                    }
                    catch (OperationCanceledException)
                    {
                        taskCompletionSource.TrySetCanceled(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                state: null,
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            return taskCompletionSource.Task;
        }

        public async Task ReleaseAsync()
        {
            _releaseEvent?.Set();

            if (_mutexTask != null)
            {
                await _mutexTask;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource?.Cancel();

            await ReleaseAsync();

            _releaseEvent?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
