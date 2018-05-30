using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ocelot.Requester
{
    public interface IHttpClientCache
    {
        bool Exists(string id);
        HttpClient Get(string id);
        void Remove(string id);
        void Set(string id, HttpClient handler, TimeSpan expirationTime);
    }
}
