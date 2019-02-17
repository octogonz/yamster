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
using System.Diagnostics;
using System.Linq;
using Yamster.Core;

namespace YamsterCmd
{
    static class Program
    {
        static Command[] Commands;

        static void Main(string[] args)
        {
            Commands = new Command[] {
                new DeleteSyncedThreadsCommand(),
                new LoadCsvDumpCommand(),
                new PostMessageCommand(),
                new SetGroupSyncCommand(),
                new SyncCommand()
            };

            try
            {
                Utils.Log("");
                Utils.Log("YamsterCmd - Command Line for Yamster!");
                Utils.Log("Version {0}", Utilities.YamsterVersion);

                Debug.WriteLine("Parsing Command Line: " + string.Join(" ", args));

                if (args.Length == 0)
                {
                    ShowGeneralHelp();
                    return;
                }

                for (int i=0; i<args.Length; ++i)
                {
                    var arg = args[i];
                    switch (arg.ToUpperInvariant())
                    {
                        case "/?":
                        case "-?":
                        case "/HELP":
                        case "-HELP":
                            if (i == 0 && i+1<args.Length)
                            {
                                var helpCommand = ParseCommand(args[i+1]);
                                Utils.Log("");
                                Utils.Log("ABOUT THE {0} COMMAND:", helpCommand.CommandId.ToString().ToUpper());
                                Utils.Log("");
                                helpCommand.ShowHelp(detailed: true);
                            }
                            else
                            {
                                ShowGeneralHelp();
                            }
                            return;
                    }
                }

                var command = ParseCommand(args[0]);

                string parseError = command.Parse(args.Skip(1).ToList());

                if (parseError != null)
                {
                    Utils.LogError(parseError);
                    Utils.Log("");
                    Utils.Log("  (Use \"YamsterCmd -Help\" to see usage instructions.)");
                    return;
                }

                Utils.Log("");

                AppContext.InitializeDefaultInstance();

                AppContext.Default.ConnectDatabase(
                    (sender, eventArgs) => {
                        Utils.Log(eventArgs.GetUpgradeMessage());
                    },
                    (sender, eventArgs) => {
                        Utils.Log("The upgrade completed successfully.\r\n");
                    }
                );

                PollingSynchronizationContext.Install(AppContext.Default);

                command.Run();

                // Finish any remaining async work
                while (ForegroundSynchronizationContext.ProcessAsyncTasks())
                {
                }
            }
            catch (Exception ex)
            {
                Utils.Log("");
                Utils.LogError(ex.Message);
            }
            finally
            {
                AppContext.UninitializeDefaultInstance();
                if (Debugger.IsAttached)
                    Debugger.Break();
            }
        }

        private static Command ParseCommand(string commandName)
        {
            const string helpInfo = "\r\n\r\nTry \"YamsterCmd -Help\" for general help.";

            CommandId subCommandId;
            if (!commandName.StartsWith("-"))
                throw new InvalidOperationException("The command name must start with a \"-\"" + helpInfo);

            if (Enum.TryParse<CommandId>(commandName.Substring(1), true, out subCommandId))
            {
                foreach (var command in Commands)
                {
                    if (command.CommandId == subCommandId)
                        return command;
                }
            }

            throw new InvalidOperationException("\"" + commandName + "\" is not a valid command" + helpInfo);
        }

        private static void ShowGeneralHelp()
        {
            Utils.Log(@"https://github.com/octogonz/yamster

YamsterCmd -Help
YamsterCmd -Help <command>

  Show general help, or detailed help for a specific command.

  Example:
    YamsterCmd -Help -SetGroupSync

Commands:
");
            foreach (var command in Commands)
            {
                command.ShowHelp(detailed: false);
            }
            
        }
    }
}
