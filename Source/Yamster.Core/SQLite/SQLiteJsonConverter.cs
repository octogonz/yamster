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
using Newtonsoft.Json;

namespace Yamster.Core.SQLite
{
    public static class SQLiteJsonConverter
    {
        const int ENCODING_VERSION = 1000;

        [JsonObject(MemberSerialization.OptOut)]
        class EmbeddedHeader
        {
            public int Version { get; set; }
            public object Value { get; set; }
        }

        public static string SaveToJson(object value)
        {
            if (value == null)
                return null;

            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Formatting = Formatting.None;

            string json = JsonConvert.SerializeObject(value, settings);
            return ENCODING_VERSION + ";" + json;
        }

        public static object LoadFromJson(string json, Type type)
        {
            if (json == null)
                return null;

            int split = json.IndexOf(';');
            if (split < 0)
                throw new ArgumentException("Invalid JSON syntax; missing header");
            int version = int.Parse(json.Substring(0, split));
            if (version != ENCODING_VERSION)
                throw new ArgumentException("Unsupported JSON encoding version");

            JsonSerializerSettings settings = new JsonSerializerSettings();

            string body = json.Substring(split+1);
            object value = JsonConvert.DeserializeObject(body, type, settings);
            return value;
        }

        public static T LoadFromJson<T>(string json)
        {
            return (T)LoadFromJson(json, typeof(T));
        }
    }
}
