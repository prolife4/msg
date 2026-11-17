using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Vmx.Common.Services.BaseModels;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.DataBase.Models;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;


namespace Vmx.Messenger.Services.MAX;

internal class MAXServiceDataModel(MessengerDbContext dbContext,
    IMemoryCache memoryCahe) : BaseServiceDataModel(dbContext, memoryCahe)
{
    #region consts
    public const string ENTRYKEY_MAX_CLINET = "vmxMaxClients";
    #endregion

    private MessengerDbContext DbContext => (MessengerDbContext)_dbContext;
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MAXServiceDataModel));

    internal async Task<MAXClientDTO> AddClient(MAXClientDTO client)
    {
        try
        {
            MAXClients clientDb = client.ToDbEntry();
            await DbContext.MAXClients.AddAsync(clientDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return new(clientDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXClientDTO>(500, e.Message);
        }
    }

    internal async Task<MAXClientDTO> UpdateClient(MAXClientDTO client)
    {
        try
        {
            MAXClients clientDb = client.ToDbEntry();
            DbContext.MAXClients.Update(clientDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return new(clientDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXClientDTO>(500, e.Message);
        }
    }

    internal async Task<MAXClientDTO> DeleteClient(MAXClientDTO client)
    {
        try
        {
            DbContext.MAXClients.Remove(await DbContext.MAXClients.FirstOrDefaultAsync(e => e.Id == client.Id));
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return client;
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXClientDTO>(500, e.Message);
        }
    }

    internal async Task<List<MAXClientDTO>> GetClients(int clientId = 0, bool noCache = false)
    {
        try
        {
            List<MAXClientDTO> MAXClients = GetItemsFromCache<List<MAXClientDTO>>(ENTRYKEY_MAX_CLINET);
            if (MAXClients == null || MAXClients.Count == 0 || noCache)
            {
                List<MAXClients> MAXClientsDb = await DbContext.MAXClients.AsNoTracking().ToListAsync();
                MAXClients = MAXClientsDb.ConvertAll<MAXClientDTO>(e => new(e));

                AddItemsToCache(ENTRYKEY_MAX_CLINET, MAXClients);
            }

            if (clientId > 0)
                MAXClients = MAXClients.Where(x => x.Id == clientId).ToList();

            return MAXClients;
        }
        catch (Exception e)
        {
            return [VmxBaseDTO.ToFailedDTO<MAXClientDTO>(500, e.Message)];
        }
    }

    internal async Task<MAXMessageDTO> AddMessage(MAXMessageDTO message)
    {
        try
        {
            message.Id = default;
            MAXMessages messageDb = message.ToDbEntry();
            messageDb.DateCreated = DateTime.Now;
            await DbContext.MAXMessages.AddAsync(messageDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return new(messageDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXMessageDTO>(500, e.Message);
        }
    }

    internal async Task<MAXMessageDTO> UpdateMessage(MAXMessageDTO message)
    {
        try
        {
            MAXMessages messageDb = message.ToDbEntry();
            DbContext.MAXMessages.Update(messageDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return new(messageDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXMessageDTO>(500, e.Message);
        }
    }

    internal async Task<MAXMessageDTO> DeleteMessages(MAXMessageDTO message)
    {
        try
        {
            DbContext.MAXMessages.Remove(await DbContext.MAXMessages.FirstOrDefaultAsync(e => e.Id == message.Id));
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return message;
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<MAXMessageDTO>(500, e.Message);
        }
    }
    internal IQueryable<MAXMessageDTO> GetMessagesQuery(int clientId)
    {
        try
        {
            return DbContext.MAXMessages.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.DateCreated)
                .Select(x=> new MAXMessageDTO(x));
        }
        catch (Exception e)
        {
            VmxLogger.Instance.Error(logger, e);
            return new List<MAXMessageDTO>().AsQueryable();
        }
    }

    internal async Task<List<MAXMessageDTO>> GetMessages(int clientId)
    {
        try
        {
            List<MAXMessages> messages = await DbContext.MAXMessages.AsNoTracking()
                .Where(m => m.ClientId == clientId && m.Status == (int)MessageStatus.InQueue)
                //.OrderByDescending(m => m.DateCreated)
                .ToListAsync();

            return messages.ConvertAll<MAXMessageDTO>(m => new(m));
        }
        catch (Exception e)
        {
            return [VmxBaseDTO.ToFailedDTO<MAXMessageDTO>(500, e.Message)];
        }
    }

    internal async Task<VmxBaseDTO> DeleteOldMessages(DateTime olderThan)
    {
        try
        {
            DbContext.MAXMessages.RemoveRange(DbContext.MAXMessages.Where(m => m.DateCreated <= olderThan && m.Status != (int)MessageStatus.InQueue));
            await DbContext.SaveChangesAsync();

            return new();
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<VmxBaseDTO>(500, e.Message);
        }
    }
}
