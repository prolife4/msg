using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models
{
    public partial class KafkaClients
    {
        public int Id { get; set; }
        [MaxLength(100)]
        public string ServerUrl { get; set; }
        [MaxLength(500)]
        public string RequestParams { get; set; }
        [MaxLength(1000)]
        public string Cluster { get; set; }
        [MaxLength(100)]
        public string DatasetTopic { get; set; }
        [MaxLength(500)]
        public string Version { get; set; }
        public int? SchemaId { get; set; }
        public string SchemaValue { get; set; }
        [MaxLength(1000)]
        public string BootstrapServer { get; set; }
        [MaxLength(100)]
        public string SchemaRegister { get; set; }
        [MaxLength(500)]
        public string GroupId { get; set; }
        public bool? IsActive { get; set; }
        public bool? SchemaWasSent { get; set; }
        public int? AutoOffsetReset { get; set; }
        public bool? UseSsl { get; set; }
        public string SslCaLocation { get; set; }
        public string SslCertificateLocation { get; set; }
        public string SslKeyLocation { get; set; }
        public string RelayUrl { get; set; }
    }
}
