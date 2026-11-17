using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models
{
    public partial class MqttClients
    {
        public int Id { get; set; }
        [MaxLength(100)]
        public string ServerUrl { get; set; }
        [MaxLength(200)]
        public string Topic { get; set; }
        public int? Port { get; set; }
        [MaxLength(500)]
        public string RequestParams { get; set; }
        public bool? IsActive { get; set; }
        [MaxLength(100)]
        public string Login { get; set; }
        [MaxLength(250)]
        public string Password { get; set; }
    }
}
