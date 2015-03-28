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
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;

namespace Yamster.Core
{
    public class YqlListContainsNode : YqlNode
    {
        public YqlNode List { get; set; }
        public YqlNode ItemToFind { get; set; }

        public YqlListContainsNode()
        {
            
        }
        public YqlListContainsNode(YqlNode list, YqlNode itemToFind)
        {
            this.List = list;
            this.ItemToFind = itemToFind;
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlListContainsNode) source;

            this.List = CloneIfNotNull(castedSource.List);
            this.ItemToFind = CloneIfNotNull(castedSource.ItemToFind);
        }

        protected override void SaveXmlElement(XElement element)
        {
            element.Add(new XElement("List", SaveToXmlOrNull(List)));
            element.Add(new XElement("ItemToFind", SaveToXmlOrNull(ItemToFind)));
        }

        protected override void LoadXmlElement(XElement element)
        {
            this.List = LoadFromXmlOrNull(element, "List");
            this.ItemToFind = LoadFromXmlOrNull(element, "ItemToFind");
        }

        public override string ToString()
        {
            return "YqlNode: ListContains";
        }

        static bool ContainsCallback<T>(T[] list, T itemToFind)
        {
            return ((IList<T>)list).Contains(itemToFind);
        }
        static MethodInfo Info_ContainsCallback = 
            Utilities.GetMethodInfo(typeof(YqlListContainsNode), "ContainsCallback",
            BindingFlags.Static | BindingFlags.NonPublic);

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            if (List == null)
                throw new ArgumentNullException("List");
            if (ItemToFind == null)
                throw new ArgumentNullException("ItemToFind");

            var compiledList = List.CompileNode(compiler);

            var listType = compiledList.Expression.Type;

            YqlCompiledNode compiledItemToFind = null;
            Expression expression = null;

            if (listType.IsArray)
            {
                Type itemType = compiledList.Expression.Type.GetElementType();
                compiledItemToFind = compiler.CoerceTo(
                    ItemToFind.CompileNode(compiler),
                    itemType
                );

                expression = Expression.Call(
                    Info_ContainsCallback.MakeGenericMethod(itemType),
                    compiledList.Expression,
                    compiledItemToFind.Expression
                );
            }
            else if (listType == typeof(YamsterUserSet))
            {
                compiledItemToFind = compiler.CoerceTo(
                    ItemToFind.CompileNode(compiler),
                    typeof(long)
                );
                
                // list.FindUserById(itemToFind) != null
                expression = Expression.NotEqual(
                    Expression.Call(
                        compiledList.Expression, 
                        YamsterUserSet.Info_FindUserById,
                        compiledItemToFind.Expression
                    ),
                    Expression.Constant(null)
                );
            }
            else
            {
                ReportError("The expression type " + listType.Name + " is not a list");
            }

            return new YqlCompiledNode(this, expression, new[] { compiledList, compiledItemToFind });
        }

    }

}
