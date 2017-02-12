using Newtonsoft.Json;

namespace NxtTipbot
{
    public class SlackChannelSession
    {
        public string Id { get; set; }
        public string Name { get; set; }

        [JsonProperty(PropertyName = "is_member")]
        public bool IsMember { get; set; }
    }
}