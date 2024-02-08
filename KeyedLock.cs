using System.Diagnostics;

namespace EasyFortniteStats_ImageApi;

public sealed class NamedLock
{
    private readonly Dictionary<string, (SemaphoreSlim, int)> _perKey;
    private readonly Stack<SemaphoreSlim> _pool;
    private readonly int _poolCapacity;

    public NamedLock(int poolCapacity = 10)
    {
        _perKey = new Dictionary<string, (SemaphoreSlim, int)>();
        _pool = new Stack<SemaphoreSlim>(poolCapacity);
        _poolCapacity = poolCapacity;
    }

    public async Task<bool> WaitAsync(string key, int millisecondsTimeout,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetSemaphore(key);
        var entered = false;
        try
        {
            entered = await semaphore.WaitAsync(millisecondsTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!entered) ReleaseSemaphore(key, entered: false);
        }

        return entered;
    }

    public Task WaitAsync(string key, CancellationToken cancellationToken = default)
        => WaitAsync(key, Timeout.Infinite, cancellationToken);

    public bool Wait(string key, int millisecondsTimeout,
        CancellationToken cancellationToken = default)
    {
        var semaphore = GetSemaphore(key);
        var entered = false;
        try
        {
            entered = semaphore.Wait(millisecondsTimeout, cancellationToken);
        }
        finally
        {
            if (!entered) ReleaseSemaphore(key, entered: false);
        }

        return entered;
    }

    public void Wait(string key, CancellationToken cancellationToken = default)
        => Wait(key, Timeout.Infinite, cancellationToken);

    public void Release(string key) => ReleaseSemaphore(key, entered: true);

    private SemaphoreSlim GetSemaphore(string key)
    {
        SemaphoreSlim semaphore;
        lock (_perKey)
        {
            if (_perKey.TryGetValue(key, out var entry))
            {
                (semaphore, var counter) = entry;
                _perKey[key] = (semaphore, ++counter);
            }
            else
            {
                lock (_pool) semaphore = _pool.Count > 0 ? _pool.Pop() : null;
                semaphore ??= new SemaphoreSlim(1, 1);
                _perKey[key] = (semaphore, 1);
            }
        }

        return semaphore;
    }

    private void ReleaseSemaphore(string key, bool entered)
    {
        SemaphoreSlim semaphore;
        int counter;
        lock (_perKey)
        {
            if (_perKey.TryGetValue(key, out var entry))
            {
                (semaphore, counter) = entry;
                counter--;
                if (counter == 0)
                    _perKey.Remove(key);
                else
                    _perKey[key] = (semaphore, counter);
            }
            else
            {
                throw new InvalidOperationException("Key not found.");
            }
        }

        if (entered) semaphore.Release();
        if (counter != 0) return;

        Debug.Assert(semaphore.CurrentCount == 1);
        lock (_pool)
            if (_pool.Count < _poolCapacity)
                _pool.Push(semaphore);
    }
}