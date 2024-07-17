using TalkingBot.Core.Caching;

namespace TalkingBot.Services;

public struct CachedMessageRole {
    public ulong messageId;
    public ulong roleId;
}

public class MessageCacher {
    public List<CachedMessageRole> CachedMessages {
        get => _cached_message_role;
        set => _cached_message_role = value;
    }
    private List<CachedMessageRole> _cached_message_role;
    private Cacher<CachedMessageRole> _message_cacher;

    public MessageCacher() {
        _message_cacher = new();
        var loaded_cache = _message_cacher.LoadCached(nameof(CachedMessageRole));
        if(loaded_cache is null) {
            _cached_message_role = [];
        } else {
            _cached_message_role = [.. loaded_cache];
        }
    }

    public void SaveCache() {
        _message_cacher.SaveCached(nameof(CachedMessageRole), [.. _cached_message_role]);
    }

    public void LoadCached() {
        _message_cacher = new();
        var loaded_cache = _message_cacher.LoadCached(nameof(CachedMessageRole));
        if(loaded_cache is null) {
            _cached_message_role = [];
        } else {
            _cached_message_role = [.. loaded_cache];
        }
    }
}
