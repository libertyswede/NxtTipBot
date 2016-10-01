using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NxtTipbot.Model
{
    [Table("account")]
    public class NxtAccount
    {
        [Column("id")]
        [Key]
        public int Id { get; set; }
        
        [Column("slack_id")]
        public string SlackId { get; set; }

        [Column("nxt_address")]
        public string NxtAccountRs { get; set; }

        [NotMapped]
        public string NxtPublicKey { get; set; }

        [NotMapped]
        public string SecretPhrase { get; set; }
    }
}