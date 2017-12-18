## Installing Yamster

1. Yamster requires the **.NET Framework 4.0** for Microsoft Windows.  If your operating system is missing this component, you can download it here:

http://www.microsoft.com/en-us/download/details.aspx?id=17851

2. Yamster requires you to install the **Gtk#** support library.  The recommended version can be downloaded from here:

http://download.xamarin.com/GTKforWindows/Windows/gtk-sharp-2.12.25.msi

Other Gtk versions can be found on this page: 

http://monodevelop.com/Download

3. REBOOT YOUR PC AFTER INSTALLING LIBRARIES FROM THE ABOVE STEPS.  This is the solution for several strange problems that people have reported.

4. Download and run the [YamsterSetup-2.9.2.msi](https://github.com/pgonzal/yamster/releases) to install the Yamster application.  To make it easier to use the command-line interface, it is suggested to install in a folder such as `C:\Yamster` instead of the default `C:\Program Files (x86)\Yamster`.

5. To start the Yamster application, use the Yamster icon on your desktop or Start Menu.

6. To invoke the command line tool, open a Windows "Command Prompt" and navigate to the folder from step #4, for example:

```
C:\Users\YourName>cd "C:\Program Files (x86)\Yamster\"
C:\Program Files (x86)\Yamster>YamsterCmd
```

## Walkthrough

1. Follow the above instructions to install Yamster.

2. Perform OAuth single-signon authentication by starting `Yamster.exe` and clicking the "Yammer Sync..." button on the right side of the window.  If prompted, enter your e-mail address (e.g. youralias@example.com) and click "Login".  When you reach the "Sync Status" window, that means you have authenticated successfully.
  
3. Go to the "Sync Settings" tab and click "Add Another Group".  Search for a group that you are interested in and add it to the list of synced groups.  (NOTE: Start with a small number of groups, since each group increases the overall sync time significantly.)  You can also check "Sync threads that appear in the Yammer Inbox" to fetch interesting threads from many different groups without having to subscribe to everything in those groups.

4. After choosing your Sync Settings, click the "Yammer Sync..." button, and then click "Start".  It will begin downloading messages from all groups, working backwards in time.  The "History Progress" field gives a general idea of how far backwards it has progressed in the Yammer history.  When the full history has been synced, the "Status" field will say "Up to date."  Syncing is slow!  For a large network, the initial sync may take overnight to complete.  See below for more info.

NOTE: If you close the Yammer Sync window, the sync operation will continue in the background; however, if you close and restart the application, the syncing does NOT restart automatically.  (This will be improved in the future.)

NOTE: You can also use the `YamsterCmd.exe` tool to perform syncing.
   
5. After some messages have been downloaded, go the "Groups" tab, where you can see the groups and threads that were downloaded.  (This is view is based on your local database, and does not require an internet connection.)  Threads that you have not read yet are highlighted in bold. For a high traffic group where you don't intend to read every message, you can uncheck the "Track Read/Unread" box (on the Sync Settings tab) to disable this highlighting.
   
6. If you see a particular message that you want to follow up on later, click the star icon.  These messages will appear in the "Starred Yams" view.

7. The "Conversations" tab shows private conversations, aggregated by participants similar to SMS history on a cell phone.  For example, if you click on the row for "John Doe", you will see a combined view of every private thread with John Doe (but excluding threads where other people participated).

8. For data mining using SQL queries, you might try [SQLite Expert Personal](http://www.sqliteexpert.com/download.html) which is a freeware tool for the Windows operating system.  Launch SQLite Expert Personal, then click  File->Open Database and choose `My Documents\Yamster\Yamster.db`.  The SQL tables you will be most interested in are called Groups, Messages, and Users.
   
9. To erase your database and start over, you can safely delete the `Yamster.db` file; it will be created again the next time you run the application.  You can also delete your saved OAuth credentials by deleting the file `Settings.xml`.

## Notes

#### Persisted Credentials
* The command line tool relies on the persisted credentials that are saved by the desktop application in a file called `My Documents\Yamster\Settings.xml`.  In other words, `YamsterCmd.exe` will not work until you successfully login using `Yamster.exe`.

* By default, the OAuthToken in `Settings.xml` is protected so that it can't be opened by a different PC or user account.  To make the file portable (for example to a Mac OS X machine), you can edit this file to set ProtectTokenWhenSaving=false.  However, be warned that this creates a security risk, since another person could use the file to access your Yammer account.

#### Rate Limits
To prevent excessive load, Yammer imposes a rate limit on your usage.  This limit is "per user per app".  The Yamster application will attempt to consume the maximum bandwidth possible without exceeding this limit, under the assumption that it is the only instance.  This means you should NOT sync data using multiple instances of Yamster simultaneously.  (When Yamster encounters a "RateLimitExceededException" in C#, it will automatically backoff by sleeping for a while, showing `> Throttled <` in the application status bar.)  You can read more about rate limits in Yammer's [REST API documentation](https://developer.yammer.com/restapi/).
  
#### Sync Options
Yamster supports two syncing algorithms.  
* The **"Optimize Reading"** algorithm checks for new messages every few minutes, and then works on downloading missing history only if it didn't find any new messages.  For an interactive reader application, this is the best choice.
* The other algorithm is called **"Bulk Download"**, which prioritizes downloading the entire history, and only looks for new messages after the history has been completely filled.  For downloading lots of messages from Yammer, this is much more efficient.  It is currently the default.

#### Support

If you have questions or feedback, please create a [GitHub Issue](https://github.com/pgonzal/yamster/issues).  Have fun!
