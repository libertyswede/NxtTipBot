using Newtonsoft.Json;

namespace NxtTipbot
{
    public class InstantMessage
    {
        public string Id { get; set; }

        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
    }
}