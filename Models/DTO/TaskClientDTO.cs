using System.Collections.Concurrent;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.DataBase.Models;

namespace Vmx.Messenger.Models.DTO;

public class TaskClientDTO : VmxBaseDTO
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }

    public ConcurrentQueue<TaskClientDTO> Messages { get; set; } = new();

    public TaskClientDTO() { }

    public TaskClientDTO(TaskClients client)
    { 
        Id = client.Id;
        Name = client.Name;
        Url = client.Url;
        Description = $"{client.Name}";
        IsActive = client.IsActive.GetValueOrDefault();
    }

    public TaskClients ToDbEntry() =>
        new()
        {
            Id = Id,
            Name = Name,
            Url = Url,
            IsActive = IsActive
        };
}
