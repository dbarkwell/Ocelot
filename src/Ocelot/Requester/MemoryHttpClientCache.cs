using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ocelot.Requester
{
    public class MemoryHttpClientCache : IHttpClientCache
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<HttpClient>> _httpClientsCache = new ConcurrentDictionary<string, ConcurrentQueue<HttpClient>>();

        public void Set(string id, HttpClient client, TimeSpan expirationTime)
        {
            ConcurrentQueue<HttpClient> connectionQueue;
            if (_httpClientsCache.TryGetValue(id, out connectionQueue))
            {
                connectionQueue.Enqueue(client);
            }
            else
            {
                connectionQueue = new ConcurrentQueue<HttpClient>();
                connectionQueue.Enqueue(client);
                _httpClientsCache.TryAdd(id, connectionQueue);
            }
        }

        public bool Exists(string id)
        {
            ConcurrentQueue<HttpClient> connectionQueue;
            return _httpClientsCache.TryGetValue(id, out connectionQueue);
        }

        public HttpClient Get(string id)
        {
            HttpClient client = null;
            ConcurrentQueue<HttpClient> connectionQueue;
            if (_httpClientsCache.TryGetValue(id, out connectionQueue))
            {
                connectionQueue.TryDequeue(out client);
            }

            return client;
        }

        public void Remove(string id)
        {
            ConcurrentQueue<HttpClient> connectionQueue;
            _httpClientsCache.TryRemove(id, out connectionQueue);
        }        
    }
}
