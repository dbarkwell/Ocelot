using System;
using System.Linq;
using System.Net;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

using Ocelot.Configuration;
using Ocelot.Logging;
using Ocelot.Middleware;

namespace Ocelot.Requester
{
    public class HttpClientBuilder : IHttpClientBuilder, Microsoft.Extensions.DependencyInjection.IHttpClientBuilder
    {
        private readonly IDelegatingHandlerHandlerFactory _factory;
        private readonly IHttpClientCache _cacheHandlers;
        private readonly IOcelotLogger _logger;
        private readonly TimeSpan _defaultTimeout;
        private readonly IHttpClientFactory _httpClientFactory;

        private string _cacheKey;
        private HttpClient _httpClient;

        public HttpClientBuilder(
            IDelegatingHandlerHandlerFactory factory, 
            IHttpClientCache cacheHandlers, 
            IOcelotLogger logger,
            IHttpClientFactory httpClientFactory)
        {
            _factory = factory;
            _cacheHandlers = cacheHandlers;
            _logger = logger;

            // This is hardcoded at the moment but can easily be added to configuration
            // if required by a user request.
            _defaultTimeout = TimeSpan.FromSeconds(90);

            _httpClientFactory = httpClientFactory;

            Services = new ServiceCollection();
        }

        public string Name { get; private set; } = Options.DefaultName;

        public IServiceCollection Services { get; }

        public HttpClient Create(DownstreamContext context)
        {
            _cacheKey = GetCacheKey(context);

            var httpClient = _cacheHandlers.Get(_cacheKey);

            if (httpClient != null)
            {
                return httpClient;
            }

            var handler = CreateHandler(context);

            if (context.DownstreamReRoute.DangerousAcceptAnyServerCertificateValidator)
            {
                handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true;

                _logger
                    .LogWarning($"You have ignored all SSL warnings by using DangerousAcceptAnyServerCertificateValidator for this DownstreamReRoute, UpstreamPathTemplate: {context.DownstreamReRoute.UpstreamPathTemplate}, DownstreamPathTemplate: {context.DownstreamReRoute.DownstreamPathTemplate}");
            }

            var timeout = context.DownstreamReRoute.QosOptions.TimeoutValue == 0
                ? _defaultTimeout 
                : TimeSpan.FromMilliseconds(context.DownstreamReRoute.QosOptions.TimeoutValue);

            if (!string.IsNullOrEmpty(context.DownstreamReRoute.ServiceName))
            {
                Name = context.DownstreamReRoute.ServiceName;
            }

            Services.Configure<HttpClientFactoryOptions>(
                Name,
                options => options.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        builder.PrimaryHandler = CreateHttpMessageHandler(handler, context.DownstreamReRoute);
                        builder.Build();
                    }));

            _httpClient = _httpClientFactory.CreateClient(Name);
            _httpClient.Timeout = timeout;

            return _httpClient;
        }

        private HttpClientHandler CreateHandler(DownstreamContext context)
        {
            // Dont' create the CookieContainer if UseCookies is not set or the HttpClient will complain
            // under .Net Full Framework
            bool useCookies = context.DownstreamReRoute.HttpHandlerOptions.UseCookieContainer;
            
            if (useCookies)
            {
                return UseCookiesHandler(context);
            }
            else
            {
                return UseNonCookiesHandler(context);
            }
        }

        private HttpClientHandler UseNonCookiesHandler(DownstreamContext context)
        {
            return new HttpClientHandler
            {
                AllowAutoRedirect = context.DownstreamReRoute.HttpHandlerOptions.AllowAutoRedirect,
                UseCookies = context.DownstreamReRoute.HttpHandlerOptions.UseCookieContainer,
            };
        }

        private HttpClientHandler UseCookiesHandler(DownstreamContext context)
        {
            return new HttpClientHandler
                {
                    AllowAutoRedirect = context.DownstreamReRoute.HttpHandlerOptions.AllowAutoRedirect,
                    UseCookies = context.DownstreamReRoute.HttpHandlerOptions.UseCookieContainer,
                    CookieContainer = new CookieContainer()
                };
        }

        public void Save()
        {
            _cacheHandlers.Set(_cacheKey, _httpClient, TimeSpan.FromHours(24));
        }

        private HttpMessageHandler CreateHttpMessageHandler(HttpMessageHandler httpMessageHandler, DownstreamReRoute request)
        {
            //todo handle error
            var handlers = _factory.Get(request).Data;

            handlers
                .Select(handler => handler)
                .Reverse()
                .ToList()
                .ForEach(handler =>
                {
                    var delegatingHandler = handler();
                    delegatingHandler.InnerHandler = httpMessageHandler;
                    httpMessageHandler = delegatingHandler;
                });
            return httpMessageHandler;
        }

        private string GetCacheKey(DownstreamContext request)
        {
            var cacheKey = $"{request.DownstreamRequest.Method}:{request.DownstreamRequest.OriginalString}";

            this._logger.LogDebug($"Cache key for request is {cacheKey}");

            return cacheKey;
        }
    }
}
