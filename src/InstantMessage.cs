using Newtonsoft.Json;

namespace NxtTipBot
{
    public class InstantMessage
    {
        public string Id { get; set; }
        public string User { get; set; }

        [JsonProperty(PropertyName = "is_open")]
        public bool IsOpen { get; set; }
    }
}