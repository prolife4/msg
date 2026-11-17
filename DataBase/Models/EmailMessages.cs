using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models
{
    public partial class EmailMessages
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string ToEmails { get; set; }
        [MaxLength(350)]
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Attachments { get; set; }
        public int? AttachmentCount { get; set; }
        public int? Status { get; set; }
        public int? AttemptCount { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateSent { get; set; }
        public int? Type { get; set; }
        public bool? UseHtmlBody { get; set; }
        public string Comment { get; set; }

    }
}
