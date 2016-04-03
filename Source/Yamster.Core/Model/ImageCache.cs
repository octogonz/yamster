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
using System.Diagnostics;
using System.Threading.Tasks;
using Gdk;

namespace Yamster.Core
{
    public class ImageCache
    {
        class CachedImageKey
        {
            public string Url { get; private set; }

            // Empty means retain original image size
            public Size ResizeDimensions { get; private set; }

            public CachedImageKey(string url, Size resizeDimensions)
            {
                this.Url = url;
                this.ResizeDimensions = resizeDimensions;
            }

            public override int GetHashCode()
            {
                return Tuple.Create(Url, ResizeDimensions).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var other = (CachedImageKey)obj;
                return ResizeDimensions == other.ResizeDimensions && StringComparer.Ordinal.Equals(Url, other.Url);
            }
        }

        class CachedImage : CachedImageKey
        {
            public Task<byte[]> RequestTask;
            public Pixbuf Pixbuf = null;
            public Exception LoadError = null;

            public CachedImage(Task<byte[]> requestTask, string url, Size size)
                : base(url,size)
            {
                this.RequestTask = requestTask;
            }
        }

        const int MaxCacheItems = 5000;
        Dictionary<CachedImageKey, CachedImage> imagesByUrl = new Dictionary<CachedImageKey, CachedImage>();
        AsyncRestCaller asyncRestCaller;

        public ImageCache(AsyncRestCaller asyncRestCaller)
        {
            this.asyncRestCaller = asyncRestCaller;
        }

        private void FinishLoadingImage(CachedImage imageToLoad)
        {
            try
            {
                Debug.WriteLine("ImageCache: Fetched image " + imageToLoad.Url);

                byte[] imageData = imageToLoad.RequestTask.Result;

                Pixbuf pixbuf = new Pixbuf(imageData);

                var resizeDimensions = imageToLoad.ResizeDimensions;
                if (!resizeDimensions.IsEmpty)
                {
                    pixbuf = pixbuf.ScaleSimple(resizeDimensions.Width, resizeDimensions.Height, InterpType.Hyper);
                }

                imageToLoad.Pixbuf = pixbuf;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ImageCache: Failed to load image " + imageToLoad.Url);
                imageToLoad.LoadError = ex;
            }

            // Free the memory returned by the REST call
            imageToLoad.RequestTask.Dispose();
            imageToLoad.RequestTask = null;
        }

        public Pixbuf TryGetImageResized(YamsterHttpRequest request, Size resizeDimensions, out Exception loadError)
        {
            CachedImageKey key = new CachedImageKey(request.Url, resizeDimensions);

            CachedImage cachedImage = null;
            Task<byte[]> requestTask;
            if (!imagesByUrl.TryGetValue(key, out cachedImage))
            {
                // Check hack to prevent the cache from growing too large
                if (imagesByUrl.Count > MaxCacheItems)
                {
                    imagesByUrl.Clear();
                }

                requestTask = this.asyncRestCaller.ProcessRawRequestAsync(request);

                cachedImage = new CachedImage(requestTask, request.Url, resizeDimensions);
                imagesByUrl.Add(key, cachedImage);
            }
            else
            {
                requestTask = cachedImage.RequestTask;
            }

            if (requestTask != null)
            {
                if (requestTask.IsCanceled || requestTask.IsCompleted || requestTask.IsFaulted)
                {
                    FinishLoadingImage(cachedImage);
                }
            }

            loadError = cachedImage.LoadError;
            return cachedImage.Pixbuf;
        }
        public Pixbuf TryGetImage(YamsterHttpRequest request, out Exception loadError)
        {
            return this.TryGetImageResized(request, new Size(), out loadError);
        }
    }
}
