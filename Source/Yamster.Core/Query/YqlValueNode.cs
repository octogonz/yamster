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
using System.Linq.Expressions;
using System.Xml.Linq;

namespace Yamster.Core
{
    public class YqlValueNode : YqlNode
    {
        object value;

        public YqlValueNode()
        {
        }
        public YqlValueNode(object value)
        {
            this.Value = value;
        }

        public object Value
        {
            get { return this.value; }
            set
            {
                if (value != null)
                {
                    switch (Convert.GetTypeCode(value))
                    {
                        case TypeCode.Boolean:
                        case TypeCode.DateTime:
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                        case TypeCode.String:
                            break;
                        default:
                            ReportError("The type " + value.GetType().Name + " is not supported");
                            break;
                    }
                }
                this.value = value;
            }
        }
        
        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlValueNode) source;

            Value = castedSource.Value;
        }

        protected override void SaveXmlElement(XElement element)
        {
            element.Name = Convert.GetTypeCode(value).ToString();
            element.Add(Convert.ToString(value));
        }

        protected override void LoadXmlElement(XElement element)
        {
            TypeCode typeCode = (TypeCode)Enum.Parse(typeof(TypeCode), element.Name.ToString());
            string text = element.Value;
            this.value = Convert.ChangeType(text, typeCode);
        }

        internal static void RegisterXmlElements()
        {
            var typeCodes = new TypeCode[]
            {
                TypeCode.Boolean,
                TypeCode.DateTime,
                TypeCode.Int16,
                TypeCode.Int32,
                TypeCode.Int64,
                TypeCode.UInt16,
                TypeCode.UInt32,
                TypeCode.UInt64,
                TypeCode.String
            };
            foreach (TypeCode typeCode in typeCodes)
            {
                YqlNode.RegisterXmlElement(typeof(YqlValueNode), typeCode.ToString());
            }
        }

        public override string ToString()
        {
            return "YqlNode: Value=" + (value ?? "(null)").ToString();
        }

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            Expression valueExpression = Expression.Constant(Value);
            return new YqlCompiledNode(this, valueExpression);
        }

    }
}
