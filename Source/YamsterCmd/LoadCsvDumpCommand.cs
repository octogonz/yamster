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

using System.Collections.Generic;
using System.IO;
using Yamster.Core;

namespace YamsterCmd
{
    class LoadCsvDumpCommand : Command
    {
        public string Folder = null;
        public bool IncludeDeleted = false;

        public override CommandId CommandId
        {
            get { return CommandId.LoadCsvDump; }
        }

        public override void ShowHelp(bool detailed)
        {
            Utils.Log(
@"YamsterCmd -LoadCsvDump -Folder <string>
");
            if (detailed)
            {
                Utils.Log(
@"Imports a zip archive that was created using the ""Export Data"" command
on Yammer's administrator control panel.  The imported data completely
replaces any existing data in Yamster's database.

  -Folder <string>      
    The path of a folder containing the unzipped archive contents.  This
    folder should contain files such as Messages.csv, Groups.csv, etc.

  -IncludeDeleted
    If specified, then the command will also import CSV objects that are
    marked as having been deleted; normally these objects are ignored.

  Examples:
    YamsterCmd -LoadCsvDump -Folder C:\Unzipped
    YamsterCmd -LoadCsvDump -IncludeDeleted -Folder ""C:\My Folder""
");
//---------------------------------------------------------------------------
            }
        }

        public override string Parse(IList<string> args)
        {
            for (int i = 0; i < args.Count; ++i)
            {
                string flag = args[i];

                string nextArg = null;
                if (i + 1 < args.Count)
                    nextArg = args[i + 1];

                switch (flag.ToUpperInvariant())
                {
                    case "-FOLDER":
                        if (nextArg == null)
                            return "The -Folder option requires a path";

                        this.Folder = nextArg.Trim();
                        ++i; // consume nextArg
                        break;
                    case "-INCLUDEDELETED":
                        this.IncludeDeleted = true;
                        break;
                    default:
                        return "Unrecognized flag \"" + flag + "\"";
                }
            }

            if (Folder == null)
                return "The -Folder option was not specified";

            if (!Directory.Exists(Folder))
            {
                return "The specified -Folder location does not exist: \"" + Folder + "\"";
            }

            return null;
        }

        public override void Run()
        {
            var appContext = AppContext.Default;

            Utils.Log("Starting import from folder:\r\n\"" + Folder + "\"");

            var csvDumpLoader = new CsvDumpLoader(AppContext.Default);
            csvDumpLoader.IncludeDeletedObjects = this.IncludeDeleted;
            
            csvDumpLoader.Load(this.Folder);

            Utils.Log("\r\nThe import completed successfully.");
        }
    }
}
