namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// One piece of deferred work, superseded by the next call.
    /// </summary>
    public sealed class Debounce : IDisposable
    {
        private readonly TimeSpan _delay;

        private CancellationTokenSource _cancellation;

        public Debounce(TimeSpan delay)
        {
            _delay = delay;
        }

        /// <summary>
        /// Wait out the delay, run <paramref name="work"/>, then hand its result to
        /// <paramref name="commit"/> - unless a newer call has superseded this one by then.
        /// </summary>
        public async Task Run<T>(Func<CancellationToken, Task<T>> work, Func<T, Task> commit)
        {
            Cancel();

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            CancellationToken token = cancellation.Token;

            try
            {
                await Task.Delay(_delay, token);

                T result = await work(token);

                // The work ran to completion but a newer call started while it did; committing now would
                // put a stale answer on screen and leave it there.
                if (token.IsCancellationRequested)
                    return;

                await commit(result);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                // A newer call superseded this one while its work was running, and then the work failed.
                // That failure is as irrelevant as a result would have been - and letting it escape would hand
                // the caller an error to act on over the top of the newer call's answer. A failure of the
                // current call is not swallowed: that one still means something.
            }
            finally
            {
                // Each run owns its source and releases it - one per keystroke otherwise, each holding the
                // delay's timer registration. The field is let go first, and only if it is still this run's:
                // Cancel on a released source throws, and it is called long after a run has finished - by
                // the exact toggle, and by every component's teardown.
                if (ReferenceEquals(_cancellation, cancellation))
                    _cancellation = null;

                cancellation.Dispose();
            }
        }

        /// <summary>As <see cref="Run{T}(Func{CancellationToken, Task{T}}, Func{T, Task})"/>, committing synchronously.</summary>
        public Task Run<T>(Func<CancellationToken, Task<T>> work, Action<T> commit)
        {
            return Run(work, result =>
            {
                commit(result);
                return Task.CompletedTask;
            });
        }

        /// <summary>Wait out the delay, then commit. For deferred work with nothing to hand over.</summary>
        public Task Run(Func<Task> commit) => Run(_ => Task.FromResult(0), _ => commit());

        /// <summary>Abandon whatever is in flight. The next <c>Run</c> does this for itself.</summary>
        public void Cancel() => _cancellation?.Cancel();

        public void Dispose() => Cancel();
    }
}
