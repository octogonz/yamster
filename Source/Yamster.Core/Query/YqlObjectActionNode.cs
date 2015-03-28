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
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Yamster.Core
{
    public abstract class YqlObjectActionNode : YqlNode
    {
        public YqlNode TargetObject { get; set; }
        public YamsterModelType TargetObjectType { get; private set; }

        protected YqlObjectActionNode(YamsterModelType targetObjectType, YqlNode targetObject)
        {
            this.TargetObject = targetObject;
            this.TargetObjectType = targetObjectType;
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlObjectActionNode)source;
            this.TargetObject = YqlNode.CloneIfNotNull(castedSource.TargetObject);
            this.TargetObjectType = castedSource.TargetObjectType;
        }

        protected override void SaveXmlElement(XElement element)
        {
            if (TargetObject != null)
            {
                element.Add(TargetObject.SaveToXml());
            }
        }

        protected override void LoadXmlElement(XElement element)
        {
            XElement targetObjectElement = element.Elements().FirstOrDefault();
            if (targetObjectElement != null)
            {
                if (element.Elements().Count() > 1)
                    throw new InvalidOperationException("Extra child elements found under " + element.Name);
                this.TargetObject = LoadFromXml(targetObjectElement);
            }
        }

        protected Expression CompileTargetExpression(YqlCompiler compiler, out YqlCompiledNode[] compiledArgs)
        {
            Type targetType = YamsterModel.GetModelClass(this.TargetObjectType);

            Expression targetExpression;

            if (TargetObject != null)
            {
                var compiledTargetObject = compiler.CompileNode(TargetObject);
                if (compiledTargetObject.Expression.Type != targetType)
                {
                    ReportError("The target object is " + compiledTargetObject.Expression.Type.Name
                        + ", which cannot be converted to " + targetType.Name);
                }
                targetExpression = compiledTargetObject.Expression;
                compiledArgs = new[] { compiledTargetObject };
            }
            else
            {
                if (compiler.ModelType != TargetObjectType)
                {
                    ReportError("This expression attempts to retrieve a {0} property from the"
                        + " current context item, which is a {1} object",
                        this.TargetObjectType, compiler.ModelType);
                }

                switch (compiler.ModelType)
                {
                    case YamsterModelType.Thread:
                        targetExpression = Expression.Property(compiler.ExecutionContextParameter,
                            YqlExecutionContext.Info_Thread);
                        break;
                    case YamsterModelType.Message:
                        targetExpression = Expression.Property(compiler.ExecutionContextParameter,
                            YqlExecutionContext.Info_Message);
                        break;
                    default:
                        throw new NotSupportedException();
                }
                compiledArgs = new YqlCompiledNode[0];
            }
            return targetExpression;
        }
    }
}
