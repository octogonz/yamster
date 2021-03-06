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
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Yamster.Core
{
    public abstract class YamsterModelQuery
    {
        public string Title { get; private set; }
        public YqlNode FilterNode { get; set; }

        public YamsterModelQuery(string title, YqlNode filterNode)
        {
            this.Title = title;
            this.FilterNode = filterNode;
        }
        public YamsterModelQuery()
            : this("", null)
        {
        }

        abstract public YamsterModelType ModelType { get; }

        public YamsterModelQuery Clone()
        {
            var cloned = (YamsterModelQuery)Activator.CreateInstance(GetType());
            cloned.CopyFrom(this);
            return cloned;
        }

        public virtual void CopyFrom(YamsterModelQuery source)
        {
            this.Title = source.Title;
            this.FilterNode = YqlNode.CloneIfNotNull(source.FilterNode);
        }

        public XDocument SaveToXml()
        {
            XDocument document = new XDocument();

            XElement rootElement;
            switch (this.ModelType)
            {
                case YamsterModelType.Message:
                    rootElement = new XElement("YamsterMessageQuery");
                    break;
                case YamsterModelType.Thread:
                    rootElement = new XElement("YamsterThreadQuery");
                    break;
                default:
                    throw new NotSupportedException();
            }
            document.Add(rootElement);

            rootElement.Add(new XAttribute("Title", this.Title));

            XElement filterNodeElement = null;
            if (FilterNode != null)
                filterNodeElement = FilterNode.SaveToXml();

            rootElement.Add(new XElement("FilterNode", filterNodeElement));

            return document;
        }

        public string SaveToXmlString()
        {
            XDocument document = SaveToXml();

            using (var writer = new StringWriter())
            {
                var xmlWriterSettings = new XmlWriterSettings() { Indent = true, IndentChars = "    " };
                using (var xmlWriter = XmlWriter.Create(writer, xmlWriterSettings))
                {
                    document.Save(xmlWriter);
                }
                writer.WriteLine(); // append a newline
                return writer.ToString();
            }
        }

        public static YamsterModelQuery LoadFromXml(XDocument document)
        {
            YamsterModelQuery query;
            switch (document.Root.Name.ToString())
            {
                case "YamsterMessageQuery":
                    query = new YamsterMessageQuery();
                    break;
                case "YamsterThreadQuery":
                    query = new YamsterThreadQuery();
                    break;
                default:
                    throw new InvalidOperationException("Unknown element: " + document.Root.Name.ToString());
            }

            query.LoadFromXml(document.Root);
            return query;
        }

        protected void LoadFromXml(XElement element)
        {
            this.Title = XmlUtilities.GetStringAttribute(element, "Title");
            this.FilterNode = YqlNode.LoadFromXml(XmlUtilities.GetChildElement(element, "FilterNode"));
        }
    }
}
