using System.Net.Http;

using Ocelot.Middleware;

namespace Ocelot.Requester
{
    public interface IHttpClientBuilder
    {
        HttpClient Create(DownstreamContext request);
        void Save();
    }
}
