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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Yamster.Core
{
    [DebuggerDisplay("{DebugText}")]
    public sealed class YqlCompiledNode
    {
        public YqlNode Node { get; private set; }
        public Expression Expression { get; private set; }
        public ReadOnlyCollection<YqlCompiledNode> CompiledArgs { get; private set; }

        public YqlCompiledNode(YqlNode node, Expression expression, IEnumerable<YqlCompiledNode> compiledArgs)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            this.Node = node;
            this.Expression = expression;
            this.CompiledArgs = new ReadOnlyCollection<YqlCompiledNode>(compiledArgs.ToArray());
        }
        public YqlCompiledNode(YqlNode node, Expression expression)
            : this(node, expression, new YqlCompiledNode[0])
        {
        }

        private string DebugText { get { return this.ToString(); } }

        public override string ToString()
        {
            return "Compiled " + Node.ToString();
        }
    }

    internal class YqlExecutionContext
    {
        AppContext appContext;
        public YqlExecutionContext(AppContext appContext)
        {
            this.appContext = appContext;
        }

        public YamsterMessage Message { get; set; }
        static internal PropertyInfo Info_Message = Utilities.GetPropertyInfo(typeof(YqlExecutionContext), "Message");

        public YamsterThread Thread { get; set; }
        static internal PropertyInfo Info_Thread = Utilities.GetPropertyInfo(typeof(YqlExecutionContext), "Thread");

        public long CurrentUserId
        {
            get
            {
                return appContext.YamsterCache.CurrentUserId;
            }
        }
        static internal PropertyInfo Info_CurrentUserId = Utilities.GetPropertyInfo(typeof(YqlExecutionContext), "CurrentUserId");
    }

    public class YqlCompiler
    {
        public readonly YamsterModelType ModelType;
        public readonly ParameterExpression ExecutionContextParameter;

        private YqlCompiler(YamsterModelQuery query)
        {
            this.ModelType = query.ModelType;
            ExecutionContextParameter = Expression.Parameter(typeof(YqlExecutionContext), "context");
        }

        internal Func<YqlExecutionContext, bool> CompileMessage(YqlNode rootNode)
        {
            var compiledNode = CompileNode(rootNode.Clone());
            //Debug.WriteLine(compiledNode.Expression.ToString());
            //Debug.WriteLine("-------");
            var lambda = Expression.Lambda<Func<YqlExecutionContext, bool>>(compiledNode.Expression, ExecutionContextParameter);
            //Debug.WriteLine(lambda.ToString());
            //Debug.WriteLine("-------");
            Func<YqlExecutionContext, bool> func = lambda.Compile();
            return func;
        }

        public YqlCompiledNode CompileNode(YqlNode node)
        {
            return node.CompileNode(this);
        }

        public YqlCompiledNode[] CompileNodes(IEnumerable<YqlNode> nodes)
        {
            return nodes.Select(x => CompileNode(x)).ToArray();
        }

        public YqlCompiledNode CoerceTo(YqlCompiledNode compiledNode, Type desiredType,
            string messageIfFailed = null)
        {
            var actualType = compiledNode.Expression.Type;
            if (actualType == desiredType)
                return compiledNode; // nothing to do

            if (messageIfFailed == null)
            {
                messageIfFailed = "The value should be of type " + actualType.Name 
                    + ", but instead is " + "actualType.Name";
            }
            compiledNode.Node.ReportError(messageIfFailed);
            return null;
        }

        internal static Func<YqlExecutionContext, bool> Compile(YamsterModelQuery query)
        {
            var compiler = new YqlCompiler(query);
            var compiledQuery = compiler.CoerceTo(compiler.CompileNode(query.FilterNode), typeof(bool),
                "Expecting a boolean expression");

            var filterExpression = Expression.Lambda<Func<YqlExecutionContext, bool>>(
                compiledQuery.Expression, compiler.ExecutionContextParameter);

            var compiledFunc = filterExpression.Compile();
            return compiledFunc;
        }
    }

}
