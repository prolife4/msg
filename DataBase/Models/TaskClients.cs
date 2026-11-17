using System.ComponentModel.DataAnnotations;

namespace Vmx.Messenger.DataBase.Models;

public class TaskClients
{
    public int Id { get; set; }
    [MaxLength(150)]
    public string Name { get; set; }
    public string Url { get; set; }
    [MaxLength(1000)]
    public bool? IsActive { get; set; }

}
