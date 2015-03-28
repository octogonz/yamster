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
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Yamster.Core
{
    public class YqlTextMatchNode : YqlNode
    {
        #region class TextMatcher
        class TextMatcher
        {
            public readonly string SearchText;
            public bool WholeWords;
            public bool MatchAllWords;

            List<Regex> regexes = new List<Regex>();

            public TextMatcher(string searchText, bool wholeWords, bool matchAllWords)
            {
                this.SearchText = searchText;
                this.WholeWords = wholeWords;
                this.MatchAllWords = matchAllWords;

                HashSet<string> usedWords = new HashSet<string>();

                string[] rawWords = Regex.Split(SearchText, @"[^a-z0-9]", RegexOptions.IgnoreCase);

                foreach (string word in rawWords)
                {
                    string scrubbedWord = word.Trim();
                    if (string.IsNullOrEmpty(scrubbedWord))
                        continue;
                    if (!usedWords.Contains(scrubbedWord))
                        usedWords.Add(scrubbedWord);
                }

                if (usedWords.Count == 0)
                    return;

                foreach (string usedWord in usedWords)
                {
                    string boundary = this.WholeWords ? @"\b" : "";
                    string pattern = boundary + usedWord + boundary;
                    Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    regexes.Add(regex);
                }
            }

            public bool IsMatch(string sourceString)
            {
                if (regexes.Count == 0)
                    return true;

                if (this.MatchAllWords)
                {
                    foreach (var regex in regexes)
                    {
                        bool match = regex.IsMatch(sourceString);
                        if (!match)
                            return false;
                    }
                    return true;
                }
                else
                {
                    foreach (var regex in regexes)
                    {
                        bool match = regex.IsMatch(sourceString);
                        if (match)
                            return true;
                    }
                    return false;
                }
            }

            static internal MethodInfo Info_IsMatch =
                Utilities.GetMethodInfo(typeof(TextMatcher), "IsMatch");

        }
        #endregion

        public YqlNode SourceString { get; set; }
        string searchText = "";
        public bool WholeWords { get; set; }
        public bool MatchAllWords { get; set; }

        public YqlTextMatchNode()
        {
        }

        public YqlTextMatchNode(YqlNode sourceString,
            string searchText, bool wholeWords, bool matchAllWords)
        {
            this.SourceString = sourceString;
            this.SearchText = searchText;
            this.WholeWords = wholeWords;
            this.MatchAllWords = matchAllWords;
        }

        public string SearchText
        {
            get { return this.searchText; }
            set 
            {
                if (value == null)
                    throw new ArgumentNullException("SearchText");
                this.searchText = value;
            }
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlTextMatchNode)source;

            this.SourceString = castedSource.SourceString;
            this.SearchText = castedSource.SearchText;
            this.WholeWords = castedSource.WholeWords;
            this.MatchAllWords = castedSource.MatchAllWords;
        }

        protected override void SaveXmlElement(XElement element)
        {
            element.Add(
                new XAttribute("SearchText", SearchText),
                new XAttribute("WholeWords", WholeWords),
                new XAttribute("MatchAllWords", MatchAllWords)
            );

            if (SourceString != null)
            {
                element.Add(SourceString.SaveToXml());
            }
            
        }

        protected override void LoadXmlElement(XElement element)
        {
            this.SearchText = XmlUtilities.GetStringAttribute(element, "SearchText");
            this.WholeWords = XmlUtilities.GetBooleanAttribute(element, "WholeWords");
            this.MatchAllWords = XmlUtilities.GetBooleanAttribute(element, "MatchAllWords");

            XElement sourceStringElement = element.Elements().FirstOrDefault();
            if (sourceStringElement != null)
            {
                if (element.Elements().Count() > 1)
                    throw new InvalidOperationException("Extra child elements found under " + element.Name);
                this.SourceString = LoadFromXml(sourceStringElement);
            }
        }

        public override string ToString()
        {
            string result = "YqlTextMatchNode: SearchText=\"" + SearchText + "\"";
            if (WholeWords)
                result += " +WholeWords";
            if (MatchAllWords)
                result += " +MatchAllWords";
            return result;
        }

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            if (SourceString == null)
                throw new ArgumentNullException("SourceString");

            YqlCompiledNode sourceStringNode = SourceString.CompileNode(compiler);
            
            // Get the body text
            TextMatcher textMatcher = new TextMatcher(this.searchText, this.WholeWords, this.MatchAllWords);

            Expression expression = Expression.Call(Expression.Constant(textMatcher), 
                TextMatcher.Info_IsMatch, 
                sourceStringNode.Expression);

            return new YqlCompiledNode(this, expression, new[] { sourceStringNode });
        }

    }

}
