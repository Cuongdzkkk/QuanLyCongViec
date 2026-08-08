using System.Collections.Concurrent;

namespace TaskManagement.Infrastructure.Services
{
    internal static class PersonalEntityMutationLock
    {
        private static readonly ConcurrentDictionary<string, LockEntry> Locks =
            new(StringComparer.Ordinal);

        public static async Task<IDisposable> AcquireAsync(string key)
        {
            LockEntry entry;
            while (true)
            {
                entry = Locks.GetOrAdd(key, _ => new LockEntry());
                lock (entry)
                {
                    if (Locks.TryGetValue(key, out var current) &&
                        ReferenceEquals(current, entry))
                    {
                        entry.ReferenceCount += 1;
                        break;
                    }
                }
            }

            await entry.Semaphore.WaitAsync();
            return new Releaser(key, entry);
        }

        private sealed class LockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int ReferenceCount { get; set; }
        }

        private sealed class Releaser : IDisposable
        {
            private readonly string _key;
            private readonly LockEntry _entry;
            private bool _disposed;

            public Releaser(string key, LockEntry entry)
            {
                _key = key;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _entry.Semaphore.Release();

                lock (_entry)
                {
                    _entry.ReferenceCount -= 1;
                    if (_entry.ReferenceCount == 0)
                    {
                        Locks.TryRemove(
                            new KeyValuePair<string, LockEntry>(_key, _entry));
                        _entry.Semaphore.Dispose();
                    }
                }
            }
        }
    }
}
