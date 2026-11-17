namespace Vmx.Messenger.DataBase.Models
{
    public partial class TaskMessages
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string Message { get; set; }
        public int? AttemptCount { get; set; }
        public DateTime? DateSent { get; set; }
        public string Comment { get; set; }
    }
}
