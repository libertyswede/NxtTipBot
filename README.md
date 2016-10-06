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
  * apitoken - Slack api token key.
  * walletFile - Path to where your Sqlite database file should be stored.
  * nxtServerAddress - The server address to the NXT server you wish to use.
  * masterKey - A secure 256-bit password used to generate deterministic secret phrases to each user.  
  *If this is unset on start, a password will be generated for you.*
4. In the the src/NxtTipbot directory, type 'dotnet restore' and then 'dotnet run'
5. Done!

# How do I run the automated tests?
1. Do the install steps above.
2. In the test/NxtTipbot.Tests directory, type 'dotnet restore' and the 'dotnet test'

# Todo
* Add support for multiple recipients like: _tipper tip @user1, @user2, @user3 20 NXT_
* Add support for more units (USD, EUR, beer, etc.).
* Add file logging (NLog).
* Use AES 256 encryption for secret phrases.  
  Decryption key to be stored in config file/passed as parameter/read from environment variable.
* Add new command 'initiate' where a public key is provided to initiate a new NXT address.
* Support for multiple slack teams.
