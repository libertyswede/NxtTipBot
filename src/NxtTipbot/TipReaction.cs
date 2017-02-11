namespace NxtTipbot
{
    public class TipReaction
    {
        public decimal Amount { get; }
        public string Comment { get; }
        public string ReactionId { get; }

        public TipReaction(string reactionId, string comment, decimal amount)
        {
            ReactionId = reactionId;
            Comment = comment;
            Amount = amount;
        }
    }
}
