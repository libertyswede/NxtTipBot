using Newtonsoft.Json;

namespace NxtTipbot
{
    public class SlackMessage
    {
        [JsonProperty(PropertyName = "channel")]
        public string ChannelId { get; set; }

        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
        public string Text { get; set; }
    }
}