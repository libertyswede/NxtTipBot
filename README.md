# Nxt Tipbot
An NXT tip bot for slack API.  
Written in .NET, with support for [.NET Standard 1.0](https://docs.microsoft.com/en-us/dotnet/articles/standard/library), making it cross platform (win, linux, os x).

# How to install and run?
1. Install [.NET Core](https://www.microsoft.com/net/core) for your platform.
2. Download latest code of NxtTipbot
3. Edit the config.json file in the src\NxtTipbot directory and set:
  * apitoken - Slack api token key.
  * walletFile - Path to where your Sqlite database file should be stored.
  * nxtServerAddress - The server address to the NXT server you wish to use.
4. In the the src\NxtTipbot directory, type 'dotnet restore' and then 'dotnet run'
5. Done!

# Todo
* ~~Migrate database to Entity Framework 7 to avoid SQL injection issues.~~
* ~~Unit tests~~
* ~~Support sending MS Currencies.~~
* Support sending Assets.
* Add support for more units (USD, EUR, beer, etc.).
* Add file logging (NLog).
* Use AES 256 encryption for secret phrases.  
  Decryption key to be stored in config file/passed as parameter/read from environment variable.
* Add new command 'initiate' where a public key is provided to initiate a new NXT address.
* Support for multiple slack teams.
