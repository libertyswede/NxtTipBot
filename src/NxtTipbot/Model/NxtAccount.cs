using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NxtTipbot.Model
{
    [Table("account")]
    public class NxtAccount
    {
        [Column("id")]
        [Key]
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }
        
        [Column("slack_id")]
        [JsonProperty(PropertyName = "slack_id")]
        public string SlackId { get; set; }

        [Column("nxt_address")]
        [JsonProperty(PropertyName = "nxt_address")]
        public string NxtAccountRs { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string NxtPublicKey { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string SecretPhrase { get; set; }

        [JsonIgnore]
        public List<UserSetting> UserSettings { get; set; }
    }
}