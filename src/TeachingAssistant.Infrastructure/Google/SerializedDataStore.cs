using Google.Apis.Util.Store;

namespace TeachingAssistant.Infrastructure.Google;

public sealed class SerializedDataStore : IDataStore
{
    private readonly IDataStore _inner;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SerializedDataStore(IDataStore inner)
    {
        _inner = inner;
    }

    public Task StoreAsync<T>(string key, T value) => RunAsync(() => _inner.StoreAsync(key, value));

    public Task DeleteAsync<T>(string key) => RunAsync(() => _inner.DeleteAsync<T>(key));

    public async Task<T> GetAsync<T>(string key)
    {
        await _gate.WaitAsync();
        try
        {
            return await _inner.GetAsync<T>(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ClearAsync() => RunAsync(_inner.ClearAsync);

    private async Task RunAsync(Func<Task> operation)
    {
        await _gate.WaitAsync();
        try
        {
            await operation();
        }
        finally
        {
            _gate.Release();
        }
    }
}
