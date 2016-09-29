using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NxtTipbot.Model
{
    [Table("setting")]
    public class Setting
    {
        [Column("id")]
        [Key]
        public long Id { get; set; }

        [Column("key")]
        [Required]
        public string Key { get; set; }

        [Column("value")]
        [Required]
        public string Value { get; set; }
    }
}
