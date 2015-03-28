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

using Newtonsoft.Json;

namespace Yamster.Core
{
    internal class JsonAutoCompleteResult
    {
        [JsonProperty("file")]
        public object[] Files { get; set; }

        [JsonProperty("group")]
        public JsonSearchedGroup[] Groups { get; set; }

        [JsonProperty("open_graph_object")]
        public object[] OpenGraphObjects { get; set; }

        [JsonProperty("page")]
        public object[] Pages { get; set; }

        [JsonProperty("topic")]
        public object[] Topics { get; set; }

        [JsonProperty("user")]
        public object[] Users { get; set; }
    }

    public class JsonSearchedGroup
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("ranking")]
        public decimal Ranking { get; set; }

        [JsonProperty("ranking_index")]
        public string RankingIndex { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("photo")]
        public string Photo { get; set; }

        [JsonProperty("mugshot_url_template")]
        public string MugshotUrl { get; set; }

        [JsonProperty("web_url")]
        public string WebUrl { get; set; }

        [JsonProperty("current_user_can_post")]
        public bool CurrentUserCanPost { get; set; }

        [JsonProperty("show_in_directory")]
        public bool ShowInDirectory { get; set; }

        [JsonProperty("private")]
        public bool Private { get; set; }
    }
}
