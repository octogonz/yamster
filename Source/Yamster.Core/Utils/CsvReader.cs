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
using System.IO;
using System.Text;

namespace Yamster.Core
{
    public class CsvException : Exception
    {
        public CsvReader CsvReader { get; private set; }

        public CsvException(string message, CsvReader csvReader, Exception innerException)
            : base(message, innerException)
        {
            this.CsvReader = csvReader;
        }
    }

    public class CsvReader : IDisposable
    {
        enum State
        {
            Start,
            Unquoted,
            Quoted,
            QuotedHandlingQuote
        }

        StreamReader streamReader;
        string filename;
        int lineNumber = 0;
        public readonly ReadOnlyCollection<string> Header;
        private readonly List<string> values = new List<string>();
        public readonly ReadOnlyCollection<string> Values;

        public CsvReader(string filename)
        {
            this.filename = filename;

            WrapExceptions(() => {
                if (!File.Exists(filename))
                    throw new FileNotFoundException("File not found", filename);
                this.streamReader = new StreamReader(filename);

                if (!this.ReadNextLine())
                    throw new InvalidOperationException("Missing CSV header row");
            });

            // Store a copy of this.values as the header
            this.Header = new ReadOnlyCollection<string>(this.values.ToArray());
            this.values.Clear();

            this.Values = new ReadOnlyCollection<string>(this.values);
        }

        public void Dispose()
        {
            if (this.streamReader != null)
            {
                this.streamReader.Dispose();
                this.streamReader = null;
            }
        }

        public int LineNumber
        {
            get { return this.lineNumber; }
        }

        public string this[int columnIndex]
        {
            get { return this.Values[columnIndex]; }
        }

        public void WrapExceptions(Action action)
        {
            try
            {
                action();
            }
            catch (CsvException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string message = "Error reading " + Path.GetFileName(this.filename);
                if (this.lineNumber > 2)
                    message += " (line " + lineNumber + ")";
                message += ": " + ex.Message;
                throw new CsvException(message, this, ex);
            }
        }

        public int GetColumnIndex(string columnName)
        {
            int index = -1;
            WrapExceptions(() =>
            {
                index = this.Header.IndexOf(columnName);
                if (index < 0)
                    throw new KeyNotFoundException("The column \"" + columnName + "\" was not found in the CSV file");
            });
            return index;
        }

        public bool ReadNextLine()
        {
            bool result = false;
            this.WrapExceptions(() => {
                result = this.ReadNextLineInner();
            });
            return result;
        }

        private bool ReadNextLineInner()
        {
            this.values.Clear();

            StringBuilder builder = new StringBuilder();
            State state = State.Start;

            bool reachedEnd = false;

            for (; ; )
            {
                string line = streamReader.ReadLine();
                ++lineNumber;

                if (line == null)
                {
                    reachedEnd = true;
                    break;
                }

                for (int i=0; i<line.Length; ++i)
                {
                    char c = line[i];

                    if (c == '"')
                    {
                        switch (state)
                        {
                            case State.Start:
                                state = State.Quoted;
                                break;
                            case State.Quoted:
                                state = State.QuotedHandlingQuote;
                                break;
                            case State.QuotedHandlingQuote:
                                builder.Append('"');
                                state = State.Quoted;
                                break;
                            case State.Unquoted:
                                // This is not allowed:
                                // abc"
                                throw new InvalidOperationException("Invalid CSV syntax on line " + lineNumber);
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else if (c == ',')
                    {
                        switch (state)
                        {
                            case State.Start:
                            case State.QuotedHandlingQuote:
                            case State.Unquoted:
                                this.values.Add(builder.ToString());
                                builder.Clear();
                                state = State.Start;
                                break;
                            case State.Quoted:
                                builder.Append(c);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        switch (state)
                        {
                            case State.Start:
                            case State.Quoted:
                            case State.Unquoted:
                                builder.Append(c);
                                break;
                            case State.QuotedHandlingQuote:
                                // This is not allowed:
                                // "abc"d
                                throw new InvalidOperationException("Invalid CSV synxtax on line " + lineNumber);
                            default:
                                throw new NotImplementedException();
                        }
                    }
                }
                if (state != State.Quoted)
                    break;
                builder.AppendLine();
            }

            if (state != State.Start)
            {
                this.values.Add(builder.ToString());
                builder.Clear();
            }

            return !reachedEnd;
        }
    }
}
