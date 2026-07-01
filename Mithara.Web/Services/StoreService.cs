using Microsoft.EntityFrameworkCore;
using Mithara.Web.Data;
using Mithara.Web.Models;

namespace Mithara.Web.Services;

public class StoreService
{
    private readonly WebDbContext _db;
    private readonly GameDbService _gameDb;

    public StoreService(WebDbContext db, GameDbService gameDb)
    {
        _db = db;
        _gameDb = gameDb;
    }

    public async Task<List<ShopProduct>> GetCashProductsAsync()
    {
        return await _db.ShopProducts
            .Where(p => p.IsActive && p.Category == "cash")
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<List<ShopProduct>> GetFounderPacksAsync()
    {
        return await _db.ShopProducts
            .Where(p => p.IsActive && p.Category == "founder")
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<ShopProduct?> GetProductAsync(int productId)
    {
        return await _db.ShopProducts.FindAsync(productId);
    }

    public async Task<ShopOrder> CreateOrderAsync(int accountId, string username, int productId, int? targetCharacterId, string? targetCharacterName)
    {
        var product = await GetProductAsync(productId);
        if (product == null)
            throw new InvalidOperationException("Produto não encontrado");

        var order = new ShopOrder
        {
            AccountId = accountId,
            Username = username,
            ProductId = productId,
            ProductName = product.Name,
            Amount = product.Price,
            TargetCharacterId = targetCharacterId,
            TargetCharacterName = targetCharacterName,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };

        _db.ShopOrders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task EnsureDefaultProductsAsync()
    {
        if (!await _db.ShopProducts.AnyAsync())
        {
            _db.ShopProducts.AddRange(
                new ShopProduct
                {
                    Name = "Pacote Fundador Prata",
                    Description = "Título exclusivo \"Fundador Prata\" + 500 moedas de cash",
                    Category = "founder",
                    Price = 29.90m,
                    CashAmount = 500,
                    Tier = "prata",
                    IconClass = "pack-silver",
                    BadgeText = "PRATA",
                    SortOrder = 0,
                },
                new ShopProduct
                {
                    Name = "Pacote Fundador Ouro",
                    Description = "Título \"Fundador Ouro\" + 1500 cash + Montaria exclusiva",
                    Category = "founder",
                    Price = 59.90m,
                    CashAmount = 1500,
                    Tier = "ouro",
                    IconClass = "pack-gold",
                    BadgeText = "OURO",
                    SortOrder = 1,
                },
                new ShopProduct
                {
                    Name = "Pacote Fundador Diamante",
                    Description = "Título \"Fundador Diamante\" + 5000 cash + Montaria + Arma exclusiva + Pet",
                    Category = "founder",
                    Price = 99.90m,
                    CashAmount = 5000,
                    Tier = "diamante",
                    IconClass = "pack-diamond",
                    BadgeText = "DIAMANTE",
                    SortOrder = 2,
                },
                new ShopProduct
                {
                    Name = "100 Moedas de Cash",
                    Description = "100 moedas de cash para usar na loja do jogo",
                    Category = "cash",
                    Price = 5.00m,
                    CashAmount = 100,
                    Tier = "",
                    IconClass = "cash-small",
                    BadgeText = "",
                    SortOrder = 0,
                },
                new ShopProduct
                {
                    Name = "500 Moedas de Cash",
                    Description = "500 moedas de cash para usar na loja do jogo",
                    Category = "cash",
                    Price = 20.00m,
                    CashAmount = 500,
                    Tier = "",
                    IconClass = "cash-medium",
                    BadgeText = "MAIS VENDIDO",
                    SortOrder = 1,
                },
                new ShopProduct
                {
                    Name = "1500 Moedas de Cash",
                    Description = "1500 moedas de cash para usar na loja do jogo",
                    Category = "cash",
                    Price = 50.00m,
                    CashAmount = 1500,
                    Tier = "",
                    IconClass = "cash-large",
                    BadgeText = "MELHOR CUSTO",
                    SortOrder = 2,
                }
            );
            await _db.SaveChangesAsync();
        }
    }
}
