﻿#region MIT License

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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yamster.Core
{
    public class YamsterReferenceJsonConverter : JsonConverter
    {
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(Type objectType) { return objectType == typeof(IReferenceJson[]); }

        private static readonly Dictionary<string, Type> typeMap = new Dictionary<string, Type> {
            { "user", typeof(JsonUserReference) },
            { "guide", typeof(JsonUserReference) },
            { "bot", typeof(JsonUserReference) },
            { "group", typeof(JsonGroupReference) },
            { "conversation", typeof(JsonConversationReference) },
            { "thread", typeof(ThreadReferenceJson) },
            { "page", typeof(JsonPageReference) },
            { "message", typeof(JsonMessageReference) },
            { "tag", typeof(JsonTopicReference) },
        };

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return JArray.Load(reader)
                .Where(item => typeMap.ContainsKey(item["type"].ToString()))
                .Select(item => (IReferenceJson)serializer.Deserialize(item.CreateReader(), typeMap[item["type"].ToString()]))
                .ToArray();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("This converter cannot write.");
        }
    }
}
