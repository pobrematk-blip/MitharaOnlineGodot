using Mithara.Web.Models.ViewModels;
using Npgsql;

namespace Mithara.Web.Services;

public sealed class MarketplaceService
{
    private readonly string _connectionString;

    public MarketplaceService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<MarketplaceListingViewModel>> GetListingsAsync(string search, int itemType, int limit = 120)
    {
        var listings = new List<MarketplaceListingViewModel>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, seller_name, listing_type, currency_type, item_id, item_name, item_type,
                   quantity, price_per_unit_gold, price_total_cents, gold_amount, refine_level, status, created_at
            FROM marketplace_listings
            WHERE status IN ('active', 'pending_payment')
              AND (@type < 0 OR item_type = @type)
              AND (@search = '' OR LOWER(item_name) LIKE LOWER(@like) OR LOWER(seller_name) LIKE LOWER(@like))
            ORDER BY created_at DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@type", itemType);
        cmd.Parameters.AddWithValue("@search", search.Trim());
        cmd.Parameters.AddWithValue("@like", "%" + search.Trim() + "%");
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int currentItemType = reader.GetInt32(6);
            int itemId = reader.GetInt32(4);
            string itemName = reader.GetString(5);
            listings.Add(new MarketplaceListingViewModel
            {
                Id = reader.GetInt64(0),
                SellerName = reader.GetString(1),
                ListingType = reader.GetInt16(2),
                CurrencyType = reader.GetInt16(3),
                ItemId = itemId,
                ItemName = itemName,
                ItemType = currentItemType,
                ItemTypeName = GetItemTypeName(currentItemType),
                Quantity = reader.GetInt32(7),
                PricePerUnitGold = reader.GetInt32(8),
                PriceTotalCents = reader.GetInt32(9),
                GoldAmount = reader.GetInt32(10),
                RefineLevel = reader.GetInt32(11),
                Status = reader.GetString(12),
                CreatedAt = reader.GetDateTime(13),
                IconUrl = GetItemIconUrl(itemId, itemName, reader.GetInt16(2)),
            });
        }

        return listings;
    }

    private static string GetItemIconUrl(int itemId, string itemName, int listingType)
    {
        if (listingType == 2)
            return "/images/items/Moeda de Gold.png";

        string safeName = itemName.Trim();
        if (!string.IsNullOrWhiteSpace(safeName))
            return $"/images/items/{Uri.EscapeDataString(safeName)}.png";

        return $"/images/items/{itemId}.png";
    }

    private static string GetItemTypeName(int type)
    {
        return type switch
        {
            1 => "Arma",
            2 => "Escudo",
            3 => "Capacete",
            4 => "Peitoral",
            5 => "Cinto",
            6 => "Luvas",
            7 => "Calca",
            8 => "Botas",
            9 => "Colar",
            10 => "Anel",
            11 => "Brinco",
            12 => "Runa",
            13 => "Asa",
            14 => "Montaria",
            15 => "Pet",
            16 => "Skin",
            17 => "Consumivel",
            18 => "Material",
            21 => "Bolsa",
            _ => "Outros",
        };
    }
}
