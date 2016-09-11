# Nxt Tipbot
An NXT tip bot for slack API.  
Written in .NET, with support for [.NET Standard 1.0](https://docs.microsoft.com/en-us/dotnet/articles/standard/library), making it cross platform (win, linux, os x).

# How to install and run?
1. Install [.NET Core](https://www.microsoft.com/net/core) for your platform.
2. Download latest code of NxtTipBot
3. Edit the config.json file in the src directory and set:
  * apitoken - Slack api token key.
  * walletFile - Path to where your Sqlite database file should be stored.
  * nxtServerAddress - The server address to the NXT server you wish to use.
4. In the the src directory, type 'dotnet restore' and the 'dotnet run'
5. Done!
