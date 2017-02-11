using Newtonsoft.Json;

namespace NxtTipbot
{
    public class SlackReaction
    {
        public SlackReactionItem Item { get; set; }

        [JsonProperty(PropertyName = "item_user")]
        public string ItemUserId { get; set; }

        public string Reaction { get; set; }

        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
    }

    public class SlackReactionItem
    {
        [JsonProperty(PropertyName = "channel")]
        public string ChannelId { get; set; }
    }
}
