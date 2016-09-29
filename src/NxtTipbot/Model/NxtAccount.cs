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
        [Required]
        public string SlackId { get; set; }

        [Column("secret_phrase")]
        [Required]
        public string SecretPhrase { get; set; }

        [Column("nxt_address")]
        [Required]
        public string NxtAccountRs { get; set; }
    }
}