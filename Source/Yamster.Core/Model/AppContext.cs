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
using System.IO;
using System.Threading;
using GLib;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    // The globals
    public class AppContext : IDisposable
    {
        bool disposed = false;
        string appDataFolder;
        int foregroundThreadId;

        YamsterApiSettings settings;
        SQLiteMapper sqliteMapper;
        YamsterArchiveDb yamsterArchiveDb;
        YamsterCoreDb yamsterCoreDb;

        YamsterCache yamsterCache;

        AsyncRestCaller asyncRestCaller;
        YamsterApi yamsterApi;
        MessagePuller messagePuller;
        LightweightUserManager userManager;
        ImageCache imageCache;

        static AppContext defaultInstance = null;

        public AppContext()
        {
            this.ValidateOsEnvironment();

            this.foregroundThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            this.settings = new YamsterApiSettings(this);
            this.settings.Load();
            this.settings.Save(); // normalize settings file

            this.asyncRestCaller = new AsyncRestCaller(this);
            this.yamsterApi = new YamsterApi(this.asyncRestCaller);

            this.userManager = new LightweightUserManager(this);

            this.imageCache = new ImageCache(this.asyncRestCaller);
        }

        public void Dispose()
        {
            if (this.disposed)
                return;
            this.disposed = true;

            this.sqliteMapper.Dispose();
            this.sqliteMapper = null;

            this.asyncRestCaller.Dispose();
            this.asyncRestCaller = null;
        }

        public bool DatabaseConnected
        {
            get
            {
                return this.yamsterCache != null;
            }
        }

        void ValidateOsEnvironment()
        {
            bool runningOnMac = false;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    runningOnMac = true;
                    break;
            }

#if YAMSTER_MAC
            if (!runningOnMac)
            {
                throw new Exception("Wrong OS: You are trying to run the Mac OSX build of Yamster."
                  + " Please download the Windows build of Yamster.");
            }
#else
            if (runningOnMac)
            {
                throw new Exception("Wrong OS: This are trying to run the Windows build of Yamster."
                  + " Please download the Mac OSX build of Yamster.");
            }
#endif
        }

        public void ConnectDatabase(EventHandler<SQLiteDataContextUpgradeEventArgs> beforeUpgradeHandler,
            EventHandler afterUpgradeHandler)
        {
            if (this.DatabaseConnected)
                throw new InvalidOperationException("The database is already connected");

            string databaseFilename = Path.Combine(AppDataFolder, "Yamster.db");
            this.sqliteMapper = new SQLiteMapper(databaseFilename, createIfMissing: true);
            this.sqliteMapper.Open();
            this.yamsterArchiveDb = new YamsterArchiveDb(sqliteMapper, 
                beforeUpgradeHandler, afterUpgradeHandler);
            this.yamsterCoreDb = new YamsterCoreDb(yamsterArchiveDb);
            this.yamsterCache = new YamsterCache(this);

            this.messagePuller = new MessagePuller(this);
        }

        public static void InitializeDefaultInstance(AppContext defaultInstance)
        {
            AppContext.defaultInstance = defaultInstance;
        }

        public static void InitializeDefaultInstance()
        {
            InitializeDefaultInstance(new AppContext());
        }

        public static void UninitializeDefaultInstance()
        {
            if (AppContext.DefaultInstanceInitialized)
                AppContext.Default.Dispose();
        }

        public static bool DefaultInstanceInitialized
        {
            get { return defaultInstance != null; }
        }

        public static AppContext Default {
            get {
                if (defaultInstance == null)
                    throw new InvalidOperationException("Not initialized");
                return defaultInstance;
            }
        }

        public YamsterApiSettings Settings { get { return this.settings; } }

        public AsyncRestCaller AsyncRestCaller { get { return this.asyncRestCaller; } }
        public YamsterArchiveDb YamsterArchiveDb { get { return this.yamsterArchiveDb; } }
        public YamsterCoreDb YamsterCoreDb { get { return this.yamsterCoreDb; } }
        public YamsterCache YamsterCache { get { return this.yamsterCache; } }
        public ImageCache ImageCache { get { return this.imageCache; } }
        public YamsterApi YamsterApi { get { return this.yamsterApi; } }
        public MessagePuller MessagePuller { get { return this.messagePuller; } }
        public LightweightUserManager UserManager { get { return this.userManager; } }

        public string AppDataFolder
        {
            get
            {
                if (this.appDataFolder == null)
                {
                    // Check for a standalone installation
                    string localSettingsFilename = Path.Combine(Utilities.ApplicationExeFolder, YamsterApiSettings.SettingsFilename);
                    if (File.Exists(localSettingsFilename))
                    {
                        this.appDataFolder = Utilities.ApplicationExeFolder;
                        Debug.WriteLine("Found a standalone settings file; setting AppDataFolder=\"" + this.appDataFolder + "\"");
                    }
                    else
                    {
#if YAMSTER_MAC
                        // For OS X, the database is stored in "~/Documents/Yamster/"
                        string userDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                        string folder = Path.Combine(userDir, "Documents/Yamster");
#else
                        // For Windows, the database is stored in the "Documents" folder
                        string myDocsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string folder = Path.Combine(myDocsFolder, "Yamster");
#endif
                        Debug.WriteLine("Setting AppDataFolder=\"" + folder + "\"");

                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }
                        this.appDataFolder = folder;
                    }
                }
                return this.appDataFolder;
            }
        }

        public void RequireForegroundThread()
        {
            int currentThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (currentThreadId != foregroundThreadId)
            {
                string message = string.Format(
                    "Program Bug: This operation is expected to run on the foreground thread #{0}"
                    + " but was found running on thread #{1}."
                    + "\r\n\r\nConsider calling GtkSynchronizationContext.Install()"
                    + " or PollingSynchronizationContext.Install() in your program.",
                    foregroundThreadId, currentThreadId);
                throw new InvalidOperationException(message);
            }
        }
    }
        
}
