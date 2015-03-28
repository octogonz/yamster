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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Yamster.Core
{
    internal static class XmlUtilities
    {

        public static XElement GetChildElement(XElement parentElement, string elementName)
        {
            XElement childElement = parentElement.Element(elementName);
            if (childElement == null)
            {
                throw new InvalidOperationException("The element \"" + parentElement.Name 
                    + "\" is missing the expected child element \"" + elementName + "\"");
            }
            return childElement;
        }

        public static string GetStringAttribute(XElement element, string attributeName)
        {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute == null)
            {
                throw new InvalidOperationException("The element \"" + element.Name
                    + "\" is missing the expected attribute \"" + attributeName + "\"");
            }
            return attribute.Value;
        }

        public static bool GetBooleanAttribute(XElement element, string attributeName)
        {
            string text = GetStringAttribute(element, attributeName);
            return bool.Parse(text);
        }

        public static T GetEnumAttribute<T>(XElement element, string attributeName)
        {
            string text = GetStringAttribute(element, attributeName);
            T enumValue = (T)Enum.Parse(typeof(T), text);
            return enumValue;
        }
    }
}
