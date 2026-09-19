using Microsoft.Extensions.Caching.Memory;

namespace FakeVeresiye.Api.Services.Import;

/// <summary>
/// Holds parsed backups between the preview / validate / import requests, keyed by a token
/// handed back to the client. In-memory and process-local — fine for this single-node tool.
/// </summary>
public interface IPreviewStore
{
    Guid Add(V5Backup backup);
    bool TryGet(Guid token, out V5Backup backup);
    void Remove(Guid token);
}

/// <summary>
/// Backed by <see cref="IMemoryCache"/> so an abandoned preview (uploaded but never
/// validated/imported) doesn't sit in memory forever: each entry expires after a period of
/// inactivity, capped by an absolute ceiling so repeated validation calls can't keep a stale
/// upload alive indefinitely.
/// </summary>
public class PreviewStore(IMemoryCache cache) : IPreviewStore
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromHours(1);

    public Guid Add(V5Backup backup)
    {
        var token = Guid.NewGuid();
        cache.Set(Key(token), backup, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
            AbsoluteExpirationRelativeToNow = AbsoluteExpiration,
        });
        return token;
    }

    public bool TryGet(Guid token, out V5Backup backup) => cache.TryGetValue(Key(token), out backup!);

    public void Remove(Guid token) => cache.Remove(Key(token));

    private static string Key(Guid token) => $"import-preview:{token}";
}
