using System.Collections.Generic;

namespace NxtTipbot
{
    public class TransferableConfig
    {
        public ulong Id { get; }
        public string Name { get; }
        public string RecipientMessage { get; }
        public List<string> Monikers { get; }

        public TransferableConfig(ulong id, string name, string recipientMessage)
        {
            Monikers = new List<string>();
            Id = id;
            Name = name;
            RecipientMessage = recipientMessage;
        }
    }
}
