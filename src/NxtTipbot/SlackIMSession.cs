using Newtonsoft.Json;

namespace NxtTipbot
{
    public class SlackIMSession
    {
        public string Id { get; set; }

        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
    }
}