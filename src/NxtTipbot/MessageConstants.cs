namespace NxtTipbot
{
    public static class MessageConstants
    {
        public const string HelpText = "*Direct Message Commands*\n"
            + "_balance_ - Wallet balance\n"
            + "_deposit_ - shows your deposit address (or creates one if you don't have one already)\n"
            + "_withdraw [nxt address] amount [unit]_ - withdraws amount to specified NXT address\n\n"
            + "*Channel Commands*\n"
            + "_tipper tip @user amount [unit] [comment]_ - sends a tip to specified user";
        
        public const string UnknownCommandReply = "huh? try typing *help* for a list of available commands.";
        
        public const string UnknownChannelCommandReply = "huh? try typing *help* in a direct message for a list of available commands.";

        public const string NoAccount = "You do currently not have an account, try *deposit* command to create one.";

        public const string CantTipBotChannel = "Thanx, but no thanx. I'm already one of the super wealthy ya know ;)";

        public const string CantTipYourselfChannel = "Have you ever tried to take money out of your wallet, and then put it back again? Did it make you feel any richer?";

        public const string NoAccountChannel = "Sorry mate, you do not have an account. Try sending me *help* in a direct message and I'll help you out set one up.";

        public const string InvalidAddress = "Not a valid NXT address";

        public const string CommentTooLongChannel = "Are you writing an article? Try shortening your comment.";

        public static string CurrentBalance(decimal balance, NxtTransferable transferable)
        {
            if (transferable.Type == NxtTransferableType.Nxt)
            {
                return $"Your current balance is {balance} NXT.";
            }
            return $"You also have {balance} {transferable.Name}.";
        }

        public static string AccountCreated(string accountRs)
        {
            return $"I have created account with address: {accountRs} for you.\n" + 
                    "Please do not deposit large amounts, as it is not a secure wallet like the core client or mynxt wallets.";
        }

        public static string DepositAddress(string accountRs)
        {
            return $"You may deposit Nxt or accepted currency/asset here: {accountRs}";
        }

        public static string NotEnoughFunds(decimal balance, string unit)
        {
            return $"Not enough {unit}. You only have {balance} {unit}.";
        }

        public static string NotEnoughFundsNeedFee(decimal balance)
        {
            return $"Not enough NXT. 1 NXT is needed for the transaction fee and you only have {balance} NXT.";
        }

        public static string Withdraw(decimal amount, string unit, ulong txId)
        {
            return $"{amount} {unit} was sent to the specified address, (https://nxtportal.org/transactions/{txId})";
        }

        public static string UnknownUnit(string unit)
        {
            return $"Unknown currency or asset {unit}";
        }

        public static string TipRecieved(string senderSlackId)
        {
            return $"Hi, you recieved a tip from <@{senderSlackId}>.\n" + 
                    "So I have set up an account for you that you can use." +
                    "Type *help* to get more information about what commands are available.";
        }

        public static string TipToAddressRsSentChannel(string senderSlackId, string addressRs, decimal amount, string unit, ulong txId, string message)
        {
            return $"<@{senderSlackId}> => {addressRs} {amount} {unit} {message} (https://nxtportal.org/transactions/{txId})";
        }

        public static string TipSentChannel(string senderSlackId, string recipientSlackId, decimal amount, string unit, ulong txId, string message)
        {
            return $"<@{senderSlackId}> => <@{recipientSlackId}> {amount} {unit} {message} (https://nxtportal.org/transactions/{txId})";
        }

        public static string NxtTipTransactionMessage(string sender, string recipient, string comment)
        {
            var message = $"tip from slack tipper - from {sender}";
            if (!string.IsNullOrEmpty(recipient))
            {
                message += $" to {recipient}";
            }
            if (!string.IsNullOrEmpty(comment))
            {
                message += $" - {comment}";
            }
            return message;
        }
    }
}