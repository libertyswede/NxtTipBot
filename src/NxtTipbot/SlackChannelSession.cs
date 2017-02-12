using Newtonsoft.Json;

namespace NxtTipbot
{
    public class SlackChannelSession
    {
        public string Id { get; set; }
        public string Name { get; set; }

        [JsonProperty(PropertyName = "is_member")]
        public bool IsMember { get; set; } = true;

        [JsonProperty(PropertyName = "is_channel")]
        public bool IsChannel { get; set; } = false; // Regular, public channel

        [JsonProperty(PropertyName = "is_group")]
        public bool IsGroup { get; set; } = false; // Private channel

        [JsonProperty(PropertyName = "is_mpim")]
        public bool IsMpim { get; set; } = false; // Multiparty IM
    }
}