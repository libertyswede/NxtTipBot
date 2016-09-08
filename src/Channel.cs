using System.Collections.Generic;
using Newtonsoft.Json;

namespace NxtTipBot
{
    public class Channel
    {
        public string Id { get; set; }
        public string Name { get; set; }

        [JsonProperty(PropertyName = "is_member")]
        public bool IsMember { get; set; }

        public List<string> Members { get; set; } = new List<string>();
    }
}