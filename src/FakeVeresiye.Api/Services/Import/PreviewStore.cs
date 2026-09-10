using System.Collections.Concurrent;

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

public class PreviewStore : IPreviewStore
{
    private readonly ConcurrentDictionary<Guid, V5Backup> _previews = new();

    public Guid Add(V5Backup backup)
    {
        var token = Guid.NewGuid();
        _previews[token] = backup;
        return token;
    }

    public bool TryGet(Guid token, out V5Backup backup) => _previews.TryGetValue(token, out backup!);

    public void Remove(Guid token) => _previews.TryRemove(token, out _);
}
