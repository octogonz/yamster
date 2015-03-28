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
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;

namespace Yamster.Core
{
    public class YqlSourceLocation
    {
        public int LineNumber { get; set; }
    }

    [DebuggerDisplay("{DebugText}")]
    public abstract class YqlNode
    {
        #region Static Members

        static Dictionary<string, Type> xmlNameToType = new Dictionary<string, Type>();
        static Dictionary<Type, string> typeToXmlName = new Dictionary<Type, string>();

        protected static void RegisterXmlElement(Type type, string xmlName)
        {
            xmlNameToType.Add(xmlName, type);
        }

        static void RegisterSingleXmlElement(Type type, string xmlName)
        {
            xmlNameToType.Add(xmlName, type);
            typeToXmlName.Add(type, xmlName);
        }
        
        static YqlNode()
        {
            RegisterSingleXmlElement(typeof(YqlTextMatchNode), "TextMatch");
            RegisterSingleXmlElement(typeof(YqlListContainsNode), "ListContains");
            RegisterSingleXmlElement(typeof(YqlListNode), "List");
            RegisterSingleXmlElement(typeof(YqlMessagePropertyNode), "MessageProperty");
            RegisterSingleXmlElement(typeof(YqlThreadPropertyNode), "ThreadProperty");
            RegisterSingleXmlElement(typeof(YqlUserPropertyNode), "UserProperty");

            // Multi-element
            YqlOperationNode.RegisterXmlElements();
            YqlValueNode.RegisterXmlElements();
            YqlPropertyNode.RegisterXmlElements(YamsterModelType.User,
                typeof(YqlUserPropertyNode), typeof(YqlUserProperty));
            YqlPropertyNode.RegisterXmlElements(YamsterModelType.Message,
                typeof(YqlMessagePropertyNode), typeof(YqlMessageProperty));
            YqlPropertyNode.RegisterXmlElements(YamsterModelType.Thread,
                typeof(YqlThreadPropertyNode), typeof(YqlThreadProperty));
        }

        #endregion

        public YqlSourceLocation SourceLocation { get; set; }

        public YqlNode()
        {
            
        }

        private string DebugText { get { return this.ToString(); } }

        public virtual void CopyFrom(YqlNode source)
        {
            this.SourceLocation = source.SourceLocation;
        }

        public YqlNode Clone()
        {
#if true
            YqlNode clonedNode = (YqlNode)Activator.CreateInstance(GetType());
            clonedNode.CopyFrom(this);
#else
            XElement xxx = this.SaveToXml();
            Debug.WriteLine(xxx.ToString());
            YqlNode clonedNode = YqlNode.LoadFromXml(xxx);
#endif
            return clonedNode;
        }

        public static YqlNode CloneIfNotNull(YqlNode source)
        {
            if (source == null)
                return null;
            return source.Clone();
        }

        internal XElement SaveToXml()
        {
            XElement element = new XElement("NotAssignedYet");

            string xmlName;
            if (typeToXmlName.TryGetValue(GetType(), out xmlName))
                element.Name = xmlName;

            SaveXmlElement(element);

            if (element.Name == "NotAssignedYet")
            {
                throw new InvalidOperationException("Program Bug: The SaveXmlElement() did not assign the element name");
            }
            if (!xmlNameToType.ContainsKey(element.Name.ToString()))
            {
                throw new InvalidOperationException("Program Bug: The SaveXmlElement() assigned an unregistered name: "
                    + element.Name);
            }

            return element;
        }

        protected abstract void SaveXmlElement(XElement element);

        internal static XElement SaveToXmlOrNull(YqlNode node)
        {
            if (node == null)
                return null;
            return node.SaveToXml();
        }

        internal static YqlNode LoadFromXml(XElement element)
        {
            if (element == null)
                return null;
            Type type = xmlNameToType[element.Name.ToString()];
            YqlNode node = (YqlNode)Activator.CreateInstance(type);
            node.LoadXmlElement(element);
            return node;
        }

        internal static YqlNode LoadFromXmlOrNull(XElement element, string childElementName)
        {
            XElement childElement = XmlUtilities.GetChildElement(element, childElementName);
            XElement firstChildChildElement = childElement.Elements().FirstOrDefault();
            if (firstChildChildElement == null)
                return null;
            return LoadFromXml(firstChildChildElement);
        }
        
        protected abstract void LoadXmlElement(XElement element);

        internal void ReportError(string message)
        {
            // TODO: Add SourceLocation
            throw new InvalidOperationException(message);
        }

        internal void ReportError(string message, params object[] args)
        {
            ReportError(string.Format(message, args));
        }

        internal protected abstract YqlCompiledNode CompileNode(YqlCompiler compiler);
    }
}
