using System.Collections.Generic;

namespace NxtTipbot
{
    public class TransferableConfig
    {
        public ulong Id { get; }
        public string Name { get; }
        public string RecipientMessage { get; }
        public List<string> Monikers { get; }

        public TransferableConfig(ulong id, string name, string recipientMessage, IEnumerable<string> monikers)
        {
            Monikers = new List<string>();
            Monikers.AddRange(monikers);
            Id = id;
            Name = name;
            RecipientMessage = recipientMessage;
        }
    }
}
