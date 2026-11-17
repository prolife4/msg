using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Vmx.Common.Logger;
using Vmx.Common.Models.DTO;
using Vmx.Common.Services.BaseModels;
using Vmx.Messenger.DataBase;
using Vmx.Messenger.DataBase.Models;
using Vmx.Messenger.Models;
using Vmx.Messenger.Models.DTO;

namespace Vmx.Messenger.Services.Telegram;

internal class TelegramServiceDataModel(MessengerDbContext dbContext,
    IMemoryCache memoryCahe) : BaseServiceDataModel(dbContext, memoryCahe)
{
    #region consts
    public const string ENTRYKEY_TELEGRAM_CLIENT = "vmxTelegramClients";
    #endregion

    private MessengerDbContext DbContext => (MessengerDbContext)_dbContext;
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(TelegramServiceDataModel));

    internal async Task<TelegramClientDTO> AddTelegramClient(TelegramClientDTO telegramClient)
    {
        try
        {
            TelegramClients telegramClientDb = telegramClient.ToDbEntry();
            await DbContext.TelegramClients.AddAsync(telegramClientDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return new(telegramClientDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramClientDTO>(500, e.Message);
        }
    }

    internal async Task<TelegramClientDTO> UpdateTelegramClient(TelegramClientDTO telegramClient)
    {
        try
        {
            TelegramClients telegramClientDb = telegramClient.ToDbEntry();
            DbContext.TelegramClients.Update(telegramClientDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return new(telegramClientDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramClientDTO>(500, e.Message);
        }
    }

    internal async Task<TelegramClientDTO> DeleteTelegramClient(TelegramClientDTO telegramClient)
    {
        try
        {
            DbContext.TelegramClients.Remove(await DbContext.TelegramClients.FirstOrDefaultAsync(e => e.Id == telegramClient.Id));
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();
            BaseServiceDataModel.ClearCache();
            return telegramClient;
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramClientDTO>(500, e.Message);
        }
    }

    internal async Task<List<TelegramClientDTO>> GetTelegramClients(int clientId = 0, bool noCache = false)
    {
        try
        {
            List<TelegramClientDTO> TelegramClients = GetItemsFromCache<List<TelegramClientDTO>>(ENTRYKEY_TELEGRAM_CLIENT);
            if (TelegramClients == null || TelegramClients.Count == 0 || noCache)
            {
                List<TelegramClients> TelegramClientsDb = await DbContext.TelegramClients.AsNoTracking().ToListAsync();
                TelegramClients = TelegramClientsDb.ConvertAll<TelegramClientDTO>(e => new(e));

                AddItemsToCache(ENTRYKEY_TELEGRAM_CLIENT, TelegramClients);
            }

            if (clientId > 0)
                TelegramClients = TelegramClients.Where(x => x.Id == clientId).ToList();

            return TelegramClients;
        }
        catch (Exception e)
        {
            return [VmxBaseDTO.ToFailedDTO<TelegramClientDTO>(500, e.Message)];
        }
    }

    internal async Task<TelegramMessageDTO> AddTelegramMessage(TelegramMessageDTO TelegramMessage)
    {
        try
        {
            TelegramMessage.Id = default;
            TelegramMessages TelegramMessageDb = TelegramMessage.ToDbEntry();
            TelegramMessageDb.DateCreated = DateTime.Now;
            await DbContext.TelegramMessages.AddAsync(TelegramMessageDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return new(TelegramMessageDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramMessageDTO>(500, e.Message);
        }
    }

    internal async Task<TelegramMessageDTO> UpdateTelegramMessage(TelegramMessageDTO TelegramMessage)
    {
        try
        {
            TelegramMessages TelegramMessageDb = TelegramMessage.ToDbEntry();
            DbContext.TelegramMessages.Update(TelegramMessageDb);
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return new(TelegramMessageDb);
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramMessageDTO>(500, e.Message);
        }
    }

    internal async Task<TelegramMessageDTO> DeleteTelegramMessages(TelegramMessageDTO TelegramMessage)
    {
        try
        {
            DbContext.TelegramMessages.Remove(await DbContext.TelegramMessages.FirstOrDefaultAsync(e => e.Id == TelegramMessage.Id));
            await DbContext.SaveChangesAsync();

            DbContext.ChangeTracker.Clear();

            return TelegramMessage;
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<TelegramMessageDTO>(500, e.Message);
        }
    }
    internal IQueryable<TelegramMessageDTO> GetMessagesQuery(int clientId)
    {
        try
        {
            return DbContext.TelegramMessages.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.DateCreated)
                .Select(x=> new TelegramMessageDTO(x));
        }
        catch (Exception e)
        {
            VmxLogger.Instance.Error(logger, e);
            return new List<TelegramMessageDTO>().AsQueryable();
        }
    }

    internal async Task<List<TelegramMessageDTO>> GetTelegramMessagesMessages(int clientId)
    {
        try
        {
            List<TelegramMessages> TelegramMessages = await DbContext.TelegramMessages.AsNoTracking()
                .Where(m => m.ClientId == clientId && m.Status == (int)MessageStatus.InQueue)
                //.OrderByDescending(m => m.DateCreated)
                .ToListAsync();

            return TelegramMessages.ConvertAll<TelegramMessageDTO>(m => new(m));
        }
        catch (Exception e)
        {
            return [VmxBaseDTO.ToFailedDTO<TelegramMessageDTO>(500, e.Message)];
        }
    }

    internal async Task<VmxBaseDTO> DeleteOldMessages(DateTime olderThan)
    {
        try
        {
            DbContext.TelegramMessages.RemoveRange(DbContext.TelegramMessages.Where(m => m.DateCreated <= olderThan && m.Status != (int)MessageStatus.InQueue));
            await DbContext.SaveChangesAsync();

            return new();
        }
        catch (Exception e)
        {
            return VmxBaseDTO.ToFailedDTO<VmxBaseDTO>(500, e.Message);
        }
    }
}
