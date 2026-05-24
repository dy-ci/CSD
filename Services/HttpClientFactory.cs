using System.Net.Http;

namespace CSD.Services;

public static class AppHttpClient
{
    private static readonly HttpClient _instance = new();

    public static HttpClient Instance => _instance;
}
