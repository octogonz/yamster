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
    public enum YqlOperation
    {
        Invalid = 0,

        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual,

        And,
        Or,

        Not,

        MyUserId
    }

    public class YqlOperationNode : YqlNode
    {
        public YqlOperation Operation;
        public readonly List<YqlNode> Args = new List<YqlNode>();

        public YqlOperationNode()
        {
        }
        public YqlOperationNode(YqlOperation operation, params YqlNode[] args)
        {
            this.Operation = operation;
            this.Args.AddRange(args);
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlOperationNode)source;

            Operation = castedSource.Operation;
            Args.Clear();
            Args.AddRange(castedSource.Args.Select(x => x.Clone()));
        }

        protected override void SaveXmlElement(XElement element)
        {
            // As a special case, YqlOperationNode defines an XML element for each operation
            element.Name = Operation.ToString();

            foreach (YqlNode arg in Args)
            {
                element.Add(arg.SaveToXml());
            }
        }

        protected override void LoadXmlElement(XElement element)
        {
            // As a special case, YqlOperationNode defines an XML element for each operation
            Operation = (YqlOperation)Enum.Parse(typeof(YqlOperation), element.Name.ToString());

            Debug.Assert(Args.Count == 0);
            foreach (XElement childElement in element.Elements())
            {
                Args.Add(LoadFromXml(childElement));
            }
        }

        internal static void RegisterXmlElements()
        {
            foreach (YqlOperation value in Enum.GetValues(typeof(YqlOperation)))
            {
                YqlNode.RegisterXmlElement(typeof(YqlOperationNode), value.ToString());
            }
        }

        public override string ToString()
        {
            return "YqlNode: " + Operation + " (" + Args.Count + " args)";
        }

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            switch (Operation)
            {
                case YqlOperation.GreaterThan:
                case YqlOperation.GreaterThanOrEqual:
                case YqlOperation.LessThan:
                case YqlOperation.LessThanOrEqual:
                case YqlOperation.Equal:
                case YqlOperation.NotEqual:
                    return CompileBinaryOp(compiler);

                case YqlOperation.And:
                case YqlOperation.Or:
                    return CompileAndOr(compiler);

                case YqlOperation.Not:
                    return CompileUnary(compiler);

                case YqlOperation.MyUserId:
                    return CompileSimpleOperation(compiler);

                default:
                    ReportError("Unimplemented operation " + Operation);
                    return null;
            }
        }

        YqlCompiledNode CompileBinaryOp(YqlCompiler compiler)
        {
            if (Args.Count != 2)
            {
                ReportError("The " + Operation + " operation requires two arguments");
            }

            var compiledArgs = compiler.CompileNodes(Args);
            var leftExpression = compiledArgs[0].Expression;
            var rightExpression = compiledArgs[1].Expression;

            Expression expression;

            switch (Operation)
            {
                case YqlOperation.GreaterThan:
                    expression = Expression.GreaterThan(leftExpression, rightExpression);
                    break;
                case YqlOperation.GreaterThanOrEqual:
                    expression = Expression.GreaterThanOrEqual(leftExpression, rightExpression);
                    break;
                case YqlOperation.LessThan:
                    expression = Expression.LessThan(leftExpression, rightExpression);
                    break;
                case YqlOperation.LessThanOrEqual:
                    expression = Expression.LessThanOrEqual(leftExpression, rightExpression);
                    break;
                case YqlOperation.Equal:
                    expression = Expression.Equal(leftExpression, rightExpression);
                    break;
                case YqlOperation.NotEqual:
                    expression = Expression.NotEqual(leftExpression, rightExpression);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return new YqlCompiledNode(this, expression, compiledArgs);
        }

        YqlCompiledNode CompileAndOr(YqlCompiler compiler)
        {
            if (Args.Count == 0)
            {
                ReportError("The " + Operation + " operation requires at least one argument");
            }

            var compiledArgs = compiler.CompileNodes(Args);

            Expression expression = compiledArgs[0].Expression;

            for (int i = 1; i < compiledArgs.Length; ++i)
            {
                if (Operation == YqlOperation.And)
                    expression = Expression.And(expression, compiledArgs[i].Expression);
                else
                    expression = Expression.Or(expression, compiledArgs[i].Expression);
            }

            return new YqlCompiledNode(this, expression, compiledArgs);
        }

        YqlCompiledNode CompileUnary(YqlCompiler compiler)
        {
            if (Args.Count != 1)
            {
                ReportError("The " + Operation + " operation requires one argument");
            }

            var compiledArgs = compiler.CompileNodes(Args);
            var operandExpression = compiledArgs[0].Expression;

            Expression expression;

            switch (Operation)
            {
                case YqlOperation.Not:
                    expression = Expression.Not(operandExpression);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return new YqlCompiledNode(this, expression, compiledArgs);
        }


        YqlCompiledNode CompileSimpleOperation(YqlCompiler compiler)
        {
            if (Args.Count != 0)
            {
                ReportError("The " + Operation + " operation cannot have any arguments");
            }

            Expression expression;
            switch (Operation)
            {
                case YqlOperation.MyUserId:
                    expression = Expression.Property(compiler.ExecutionContextParameter,
                        YqlExecutionContext.Info_CurrentUserId);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return new YqlCompiledNode(this, expression);
        }

    }

}
