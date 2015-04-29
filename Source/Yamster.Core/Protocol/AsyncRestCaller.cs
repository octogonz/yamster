#region MIT License

// Yamster!
// Copyright (c) Microsoft Corporation
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the ""Software""), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or 
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Yamster.Core
{
    public enum HttpRequestMethod
    {
        Get,
        Post,
        Delete,
        Put,
        Head
    }

    internal class AsyncRestCall
    {
        HttpWebRequest HttpWebRequest;
        Exception ResultException;
        byte[] ResultBytes;
        public SemaphoreSlim Semaphore = null;

        public AsyncRestCall(HttpWebRequest httpWebRequest)
        {
            this.HttpWebRequest = httpWebRequest;
        }

        public bool IsCompleted
        {
            get
            {
                return ResultException != null || ResultBytes != null;
            }
        }

        // This is the part that is safe to execute outside the main thread
        internal void ProcessThreadsafe()
        {
            if (this.IsCompleted)
                throw new InvalidOperationException("Process() was already called for this object");

            try
            {
                DateTime startTime = DateTime.Now;

                using (HttpWebResponse response = (HttpWebResponse)this.HttpWebRequest.GetResponse())
                {
                    TimeSpan duration = DateTime.Now - startTime;

                    Debug.WriteLine("Status: {0}, received {1} bytes, from cache: {2:l}, {3:N2}ms",
                        response.StatusCode, response.ContentLength, response.IsFromCache.ToString().ToLower(), duration.TotalMilliseconds);

                    using (var reader = new BinaryReader(response.GetResponseStream()))
                    {
                        this.ResultBytes = AsyncRestCall.ReadAllBytes(reader);
                    }
                }
            }
            catch (WebException ex)
            {
                try
                {
                    YamsterApi.CheckForErrors(ex);
                    this.ResultException = ex; // if Handle() didn't throw, then keep the original exception
                }
                catch (Exception ex2)
                {
                    this.ResultException = ex2;
                }
            }
            Debug.Assert(this.IsCompleted);
            
            // Signal that the operation is complete
            if (Semaphore != null)
                Semaphore.Release();
        }

        private static byte[] ReadAllBytes(BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (var destination = new MemoryStream())
            {
                var buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    destination.Write(buffer, 0, count);
                return destination.ToArray();
            }
        }


        public byte[] GetResult() 
        {
            if (!this.IsCompleted)
                throw new InvalidOperationException("The request has not completed yet");

            if (this.ResultException != null)
                throw this.ResultException;

            return this.ResultBytes;
        }
    }

    public class AsyncRestCaller : IDisposable
    {
        private static readonly string userAgent;

        AppContext appContext;
        object lockObject = new object();
        Thread backgroundThread;
        bool disposed = false;
        ManualResetEventSlim backgroundThreadEvent = new ManualResetEventSlim(true);
        // Note: Entries are dequeued starting with LinkedList.First
        LinkedList<AsyncRestCall> RequestQueue = new LinkedList<AsyncRestCall>();

        static AsyncRestCaller()
        {
            // TODO: Surface this as an API input:
            userAgent = String.Format("Yamster! {0} ({1}; {2})",
                 typeof(AsyncRestCaller).Assembly.GetName().Version,
                 Environment.OSVersion.VersionString,
                 Thread.CurrentThread.CurrentCulture.Name);

        }

        public AsyncRestCaller(AppContext appContext)
        {
            this.appContext = appContext;
        }

        #region Background Thread

        public void Dispose() // IDisposable
        {
            if (disposed)
                return;

            disposed = true;
            lock (this.lockObject)
            {
                backgroundThreadEvent.Set();
            }
        }

        void EnsureBackgroundThreadStarted()
        {
            if (backgroundThread == null)
            {
                Debug.WriteLine("AsyncRestCaller: Starting background thread");
                backgroundThread = new Thread(RunBackgroundThread);
                backgroundThread.Start();
            }
        }

        void RunBackgroundThread()
        {
            while (!disposed)
            {
                // Wait for an image to be added or Dispose() to be called
                backgroundThreadEvent.Wait();

                AsyncRestCall asyncRestCall = null;
                lock (this.lockObject)
                {
                    if (RequestQueue.Count > 0)
                    {
                        asyncRestCall = RequestQueue.First.Value;
                        RequestQueue.RemoveFirst();
                    }
                    else
                    {
                        // Queue is empty, so resume waiting
                        backgroundThreadEvent.Reset();
                    }
                }

                if (asyncRestCall != null)
                {
                    asyncRestCall.ProcessThreadsafe();
                }
            }
        }

        #endregion

        public T ProcessRequest<T>(HttpRequestMethod method, string url, Dictionary<string, string> parameters = null)
        {
            AsyncRestCall call = CreateRequestObject(method, url, parameters);
            call.ProcessThreadsafe();
            byte[] bytes = call.GetResult();
            return YamsterApi.ParseJsonResponse<T>(bytes);
        }

        public async Task<T> ProcessRequestAsync<T>(HttpRequestMethod method, string url, Dictionary<string, string> parameters = null)
        {
            byte[] bytes = await this.ProcessRawRequestAsync(method, url, parameters);
            return YamsterApi.ParseJsonResponse<T>(bytes);
        }

        public async Task<byte[]> ProcessRawRequestAsync(HttpRequestMethod method, string url, Dictionary<string, string> parameters = null)
        {
            AsyncRestCall asyncRestCall = CreateRequestObject(method, url, parameters);
            asyncRestCall.Semaphore = new SemaphoreSlim(initialCount: 0, maxCount: 1);
            this.EnsureBackgroundThreadStarted();
            lock (this.lockObject)
            {
                RequestQueue.AddLast(asyncRestCall);
                backgroundThreadEvent.Set();
            }

            await asyncRestCall.Semaphore.WaitAsync();
            asyncRestCall.Semaphore.Dispose();

            byte[] bytes = asyncRestCall.GetResult();
            return bytes;
        }

        AsyncRestCall CreateRequestObject(HttpRequestMethod method, string url, Dictionary<string, string> parameters)
        {
            string methodString = method.ToString().ToUpperInvariant();

            // Transform the URL, given the oauth settings
            // This should probably be a no-op
            var urlWithQuerystring = getFinalUrl(methodString, parameters, buildAbsoluteUrl(url));

            Debug.WriteLine("AsyncRestCaller: Starting request {0}: {1}", method,
                new Uri(urlWithQuerystring).GetLeftPart(UriPartial.Path));

            var request = (HttpWebRequest)HttpWebRequest.Create(urlWithQuerystring);

            request.Method = methodString;
            request.UserAgent = userAgent;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate);

            // Modify the request, given the oauth settings
            string token = this.appContext.Settings.OAuthToken;
            request.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            setBody(request, parameters);

            return new AsyncRestCall(request);
        }

        private string buildAbsoluteUrl(string url)
        {
            Uri tempUri;
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out tempUri))
            {
#if YAMSTER_MAC
                bool isAbsolute = tempUri.Host != "";
#else
                bool isAbsolute = tempUri.IsAbsoluteUri;
#endif
                if (isAbsolute)
                    return url;
            }

            return this.appContext.Settings.YammerServiceUrl + url;
        }

        private string getFinalUrl(string method, Dictionary<string, string> parameters, string part)
        {
            return !sendParamsAsBody(method) ? finishUrl(part, parameters) : part;
        }

        private void setBody(HttpWebRequest request, object parameters)
        {
            if (parameters == null || !sendParamsAsBody(request.Method)) return;

            var serializedContent = buildBody(parameters);

            request.ContentType = "application/json";
            request.ContentLength = serializedContent.Length;

            var stream = request.GetRequestStream();
            stream.Write(serializedContent, 0, serializedContent.Length);
        }

        private byte[] buildBody(object parameters)
        {
            var serializationSettings = new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            };

            return UTF8Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parameters, Formatting.None, serializationSettings));
        }

        private bool sendParamsAsBody(string method)
        {
            return method == "POST" || method == "PUT" || method == "DELETE";
        }

        private string finishUrl(string url, Dictionary<string, string> parameters)
        {
            string param = AsyncRestCaller.GetUrlEncodedParameters(parameters);
            if (string.IsNullOrEmpty(param))
                return url;

            return url + (url.Contains("?") ? "&" : "?") + param;
        }

        static private string GetUrlEncodedParameters(Dictionary<string, string> dict)
        {
            if (dict == null)
                return "";

            string[] items = dict.Keys
                .Select(key => String.Format("{0}={1}", key, HttpUtility.UrlEncode(dict[key])))
                .ToArray();

            return String.Join("&", items);
        }

        public async Task<byte[]> PostFormAsync(string url, NameValueCollection parameters,
            HttpRequestMethod method = HttpRequestMethod.Post)
        {
            using (var webClient = new WebClient())
            {
                webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

                string token = this.appContext.Settings.OAuthToken;
                webClient.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                webClient.Headers.Add("Accept-Encoding", "gzip, deflate");
                webClient.Headers.Add("Authorization", String.Format("Bearer {0}", token));
                webClient.Headers.Add("User-Agent", userAgent);

                string absoluteUrl = buildAbsoluteUrl(url);

                Debug.WriteLine("PostFormAsync: " + method.ToString() + " " + url);
                return await webClient.UploadValuesTaskAsync(absoluteUrl, 
                    method.ToString().ToUpperInvariant(), 
                    parameters);
            }
        }

    }
}
