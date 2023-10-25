namespace AbogadoV2.Providers;

public interface IProxyProvider
{
    string GetRandomProxy();
    HashSet<string> GetAllProxies();
    void RemoveProxy(string proxy);
}

public class ProxyProvider : IProxyProvider
{
    private readonly Random _rnd = new();
    private readonly HashSet<string> _proxies = File.ReadAllLines("proxies.txt").ToHashSet();

    public string GetRandomProxy()
    {
        return _proxies.ElementAt(_rnd.Next(_proxies.Count));
    }

    public HashSet<string> GetAllProxies()
    {
        return _proxies;
    }

    public void RemoveProxy(string proxy)
    {
        _proxies.Remove(proxy);
        File.WriteAllLines("proxies.txt", _proxies);
    }
}