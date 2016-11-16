using Newtonsoft.Json;

namespace NxtTipbot.Model
{
    public class EncryptedMessage
    {
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public string Nonce { get; set; }
    }
}
