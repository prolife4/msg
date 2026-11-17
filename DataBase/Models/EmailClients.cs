using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models
{
    public partial class EmailClients
    {
        public int Id { get; set; }
        [MaxLength(100)]
        public string Address { get; set; }
        [MaxLength(100)]
        public string SubAddress { get; set; }
        public int? Port { get; set; }
        [MaxLength(100)]
        public string Login { get; set; }
        [MaxLength(100)]
        public string Password { get; set; }
        public bool? UseSsl { get; set; }
        public int? MaxAttemptCount { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDefault { get; set; }
        public bool? UseEWS { get; set; }
    }
}
