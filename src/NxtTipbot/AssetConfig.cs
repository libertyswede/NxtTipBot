namespace NxtTipbot
{
    public class AssetConfig
    {
        public ulong Id { get; }
        public string Name { get; }
        public string RecipientMessage { get; }

        public AssetConfig(ulong id, string name, string recipientMessage)
        {
            Id = id;
            Name = name;
            RecipientMessage = recipientMessage;
        }
    }
}