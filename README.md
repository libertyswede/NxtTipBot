# Nxt Tipbot
NXT tip bot for [Slack](https://slack.com/).  
Written in .NET Core, that runs on Windows, Linux and macOS.  
[.NET Core](https://www.microsoft.com/net/core/platform) is an open source development platform from Microsoft.

# Features
* Make public tips in slack channels with NXT, MS Currencies or Assets
* See your own balance/deposit/withdraw in direct messages with the bot
* Help command

# How to install and run?
1. Install [.NET Core](https://www.microsoft.com/net/core) for your platform.
2. Download latest code of NxtTipbot
3. Edit the [config.json](src/NxtTipbot/config.json) file in the src/NxtTipbot directory and set:
  * apitoken - Slack api token key. - *You get this from the owner of the slack team*
  * walletFile - Path to where your Sqlite database file should be stored.
  * nxtServerAddress - The server address to the NXT server you wish to use.
  * masterKey - A secure 256-bit password used to generate deterministic secret phrases to each user.  
  *If this is unset on start, a password will be generated for you.  
  Copy it from the command line output and into the config file and restart the program.*
4. In the the src/NxtTipbot directory, type 'dotnet restore' and then 'dotnet run'
5. Done!

# How do I run the automated tests?
1. Do the install steps above.
2. In the test/NxtTipbot.Tests directory, type 'dotnet restore' and the 'dotnet test'

# Todo
* Improved help command, where you can say "help tip" to get more in depth help of the tip command.
* On startup, call getBlockchainStatus API function and check isDownloading property. If true, shut down with proper error message.
* Implement deposit/withdraw for MGW asset(s). Need to verify this with VB.
* Config option for local signing instead of relying on NRS signing transactions.
* Add support for self signed certificates when connecting to NXT Server.
* Utility to restore blockchain backup to sqlite database. (should be a separate application)
* Config option for posting confirmation on a separate channel, or maybe just as DM to the sender+recipient.
* Be able to send to NXT aliases as well as @username and NXT addresses.
* When trying to tip and sender has 0 NXT, use a more humoristic error message, like 'you're all out mate'.
* Support having tip command as a part of a multi line chat message.
* Stats command with support for total number of tips sent and total amount sent.
* Add support for more units (USD, EUR, beer, etc.).
* Add support for icons as units, ie. a beer icon instead of the text 'beer'.
* Add file logging (NLog).
* Use AES 256 encryption for secret phrases.  
  Decryption key to be stored in config file/passed as parameter/read from environment variable.
* Add new command 'initiate' where a public key is provided to initiate a new NXT address.
* Support for multiple slack teams.
