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
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;

namespace Yamster.Core
{
    public class YqlListNode : YqlNode
    {
        public readonly List<YqlNode> Items = new List<YqlNode>();

        public YqlListNode()
        {
        }

        public YqlListNode(params YqlNode[] items)
        {
            Items.AddRange(items);
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlListNode) source;

            this.Items.Clear();
            this.Items.AddRange(castedSource.Items.Select(x => x.Clone()));
        }

        protected override void SaveXmlElement(XElement element)
        {
            foreach (YqlNode item in Items)
            {
                element.Add(item.SaveToXml());
            }
        }

        protected override void LoadXmlElement(XElement element)
        {
            Debug.Assert(Items.Count == 0);
            foreach (XElement childElement in element.Elements())
            {
                Items.Add(LoadFromXml(childElement));
            }
        }

        public override string ToString()
        {
            return "YqlNode: Items (" + Items.Count + " items)";
        }

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            if (Items.Count == 0)
            {
                ReportError("The list must contain at least one item");
            }
            
            var compiledItems = compiler.CompileNodes(Items);

            Type itemType = compiledItems[0].Expression.Type;

            // Enforce that the other items have the same type
            for (int i=0; i<compiledItems.Length; ++i)
            {
                var compiledItem = compiledItems[i];
                if (compiledItem.Expression.Type != itemType)
                {
                    ReportError("The list items must all have the same type as the first item: " 
                        + itemType.Name);
                }
            }

            // TODO: It would be more efficient to store the array in ExecutionContext
            // rather than reconstructing it each time the expression is evaluated
            NewArrayExpression expression = Expression.NewArrayInit(itemType,
                compiledItems.Select(x => x.Expression).ToArray()
            );
            return new YqlCompiledNode(this, expression, compiledItems);
        }
    }
}
