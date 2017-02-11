using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NxtTipbot.Model
{
    [Table("user_setting")]
    public class UserSetting
    {
        [Column("id")]
        [Key]
        public int Id { get; set; }

        [Column("key")]
        public string Key { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }

        [ForeignKey("AccountId")]
        public NxtAccount Account { get; set; }

        [Column("value")]
        public string Value { get; set; }
    }
}
