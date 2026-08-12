using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Artsy.API.Services
{
    public static class IPv4HttpClientHelper
    {
        public static async ValueTask<Stream> ConnectCallback(
            SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }

        public static HttpClient CreateHttpClient(IHttpClientFactory factory, string? bearerToken = null)
        {
            var handler = new SocketsHttpHandler { ConnectCallback = ConnectCallback };
            var client = new HttpClient(handler);
            if (!string.IsNullOrEmpty(bearerToken))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            return client;
        }
    }
}
