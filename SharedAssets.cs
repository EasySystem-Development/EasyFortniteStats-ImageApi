using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Caching.Memory;

using SkiaSharp;

namespace EasyFortniteStats_ImageApi;

public class SharedAssets
{
    private static readonly MemoryCacheEntryOptions _cacheOptions = new() { Priority = CacheItemPriority.NeverRemove };
    private static readonly SemaphoreSlim _semaphore = new(1);
    private readonly IMemoryCache _memoryCache;

    public SharedAssets(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async ValueTask<SKBitmap?> GetBitmap(string format, string? arg1)
    {
        if (arg1 is null) return null;
        var path = string.Format(format, arg1);
        return await GetBitmap(path);
    }

    public async ValueTask<SKBitmap?> GetBitmap(string? path)
    {
        if (path is null) return null;

        var key = $"bmp_{path}";
        var cached = _memoryCache.Get<SKBitmap?>(key);
        if (cached is not null) return cached;

        await _semaphore.WaitAsync();

        cached = _memoryCache.Get<SKBitmap?>(key);
        if (cached is not null)
        {
            _semaphore.Release();
            return cached;
        }

        if (!File.Exists(path))
        {
            _memoryCache.Set(key, (SKBitmap?)null, _cacheOptions);
            _semaphore.Release();
            return null;
        }

        var fileData = await File.ReadAllBytesAsync(path);
        var bitmap = SKBitmap.Decode(fileData);
        _memoryCache.Set(key, bitmap, _cacheOptions);
        _semaphore.Release();
        return bitmap;
    }

    public async ValueTask<SKTypeface> GetFont(string path)
    {
        var key = $"font_{path}";
        var cached = _memoryCache.Get<SKTypeface>(key);
        if (cached is not null) return cached;

        await _semaphore.WaitAsync();

        cached = _memoryCache.Get<SKTypeface>(key);
        if (cached is not null)
        {
            _semaphore.Release();
            return cached;
        }

        var fileData = await File.ReadAllBytesAsync(path);

        unsafe
        {
            var fileDataBuffer = NativeMemory.Alloc((nuint)fileData.Length);
            fixed (byte* fileDataPtr = fileData)
            {
                Unsafe.CopyBlockUnaligned(fileDataBuffer, fileDataPtr, (uint)fileData.Length);
            }
            var data = SKData.Create(new IntPtr(fileDataBuffer), fileData.Length,
                (address, _) => NativeMemory.Free(address.ToPointer())); // TODO: test if can be disposed
            var typeface = SKTypeface.FromData(data);
            _memoryCache.Set(key, typeface, _cacheOptions);
            _semaphore.Release();
            return typeface;
        }
    }
}
