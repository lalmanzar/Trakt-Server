using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;

namespace Trakt.Net
{
    public class HttpClientManager
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        public HttpClientManager(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
            Instance = this;
        }

        public static HttpClientManager Instance { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="resourcePool"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Stream> Post(string url, Dictionary<string, string> postData, SemaphoreSlim resourcePool,
                                       CancellationToken cancellationToken)
        {
            if (postData == null)
            {
                throw new ArgumentNullException("postData");
            }

            var strings = postData.Keys.Select(key => string.Format("{0}={1}", key, postData[key]));
            var postContent = string.Join("&", strings.ToArray());
            var content = new StringContent(postContent, Encoding.UTF8, "application/x-www-form-urlencoded");

            return await PostInternal(url, content, resourcePool, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="resourcePool"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Stream> Post(string url, string postData, SemaphoreSlim resourcePool,
                                       CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");
            
            var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
            
            return await PostInternal(url, content, resourcePool, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="resourcePool"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<Stream> PostInternal(string url, HttpContent postData, SemaphoreSlim resourcePool, CancellationToken cancellationToken)
        {
            if (resourcePool != null)
            {
                await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var handler = new WebRequestHandler
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
                AutomaticDecompression = DecompressionMethods.None
            };

            var client = new HttpClient(handler);

            try
            {
                var data = await client.PostAsync(url, postData, cancellationToken).ConfigureAwait(false);

                if (!data.IsSuccessStatusCode)
                    throw new HttpException("Error");

                return await data.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new OperationCanceledException(ex.Message, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException(ex.Message, ex);
            }
            finally
            {
                if (resourcePool != null)
                {
                    resourcePool.Release();
                }
            }
        }
    }
}
