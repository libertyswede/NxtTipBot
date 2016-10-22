namespace NxtTipbot
{
    public class TransferableConfig
    {
        public ulong Id { get; }
        public string Name { get; }
        public string RecipientMessage { get; }

        public TransferableConfig(ulong id, string name, string recipientMessage)
        {
            Id = id;
            Name = name;
            RecipientMessage = recipientMessage;
        }
    }
}
