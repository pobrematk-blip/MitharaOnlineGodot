using Mithara.Web.Models.ViewModels;
using Npgsql;

namespace Mithara.Web.Services;

public sealed class MarketplaceService
{
    private readonly string _connectionString;
    private const int DefaultInventorySlots = 30;

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
                   quantity, price_per_unit_gold, price_total_cents, gold_amount, refine_level,
                   status, created_at, expires_at, payment_status
            FROM marketplace_listings
            WHERE status IN ('active', 'pending_payment')
              AND expires_at > CURRENT_TIMESTAMP
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
                CreatedAt = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc),
                ExpiresAt = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc),
                PaymentStatus = reader.GetString(15),
                IconUrl = GetItemIconUrl(itemId, itemName, reader.GetInt16(2)),
            });
        }

        return listings;
    }

    public async Task<List<MarketplaceCharacterOption>> GetAccountCharactersAsync(int accountId)
    {
        var result = new List<MarketplaceCharacterOption>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, level
            FROM characters
            WHERE account_id = @account
            ORDER BY slot_index, id
            """;
        cmd.Parameters.AddWithValue("@account", accountId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new MarketplaceCharacterOption
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Level = reader.GetInt32(2),
            });
        }

        return result;
    }

    public async Task<bool> CharacterBelongsToAccountAsync(int accountId, int characterId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM characters WHERE id = @character AND account_id = @account LIMIT 1";
        cmd.Parameters.AddWithValue("@character", characterId);
        cmd.Parameters.AddWithValue("@account", accountId);
        return await cmd.ExecuteScalarAsync() != null;
    }

    public async Task<MarketplaceListingViewModel?> GetPixListingAsync(long listingId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, seller_name, listing_type, currency_type, item_id, item_name, item_type,
                   quantity, price_per_unit_gold, price_total_cents, gold_amount, refine_level,
                   status, created_at, expires_at, payment_status
            FROM marketplace_listings
            WHERE id = @id
              AND currency_type = 2
              AND status IN ('active', 'pending_payment')
              AND expires_at > CURRENT_TIMESTAMP
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@id", listingId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        int currentItemType = reader.GetInt32(6);
        int itemId = reader.GetInt32(4);
        string itemName = reader.GetString(5);
        return new MarketplaceListingViewModel
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
            CreatedAt = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc),
            ExpiresAt = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc),
            PaymentStatus = reader.GetString(15),
            IconUrl = GetItemIconUrl(itemId, itemName, reader.GetInt16(2)),
        };
    }

    public async Task MarkMercadoPagoPreferenceAsync(long listingId, string preferenceId, string paymentStatus, int buyerAccountId, int buyerCharacterId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE marketplace_listings
            SET mercado_pago_preference_id = @preference,
                payment_status = @status,
                status = 'pending_payment',
                buyer_account_id = @buyerAccount,
                buyer_character_id = @buyerCharacter,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id
              AND currency_type = 2
              AND status IN ('active', 'pending_payment')
            """;
        cmd.Parameters.AddWithValue("@id", listingId);
        cmd.Parameters.AddWithValue("@preference", preferenceId);
        cmd.Parameters.AddWithValue("@status", paymentStatus);
        cmd.Parameters.AddWithValue("@buyerAccount", buyerAccountId);
        cmd.Parameters.AddWithValue("@buyerCharacter", buyerCharacterId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task EnsureMarketplacePaymentTablesAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            ALTER TABLE marketplace_listings ADD COLUMN IF NOT EXISTS mercado_pago_payment_id VARCHAR(255) NOT NULL DEFAULT '';

            CREATE TABLE IF NOT EXISTS marketplace_pix_payments (
                id BIGSERIAL PRIMARY KEY,
                payment_id VARCHAR(255) NOT NULL UNIQUE,
                preference_id VARCHAR(255) NOT NULL DEFAULT '',
                listing_id BIGINT NOT NULL,
                buyer_account_id INT NOT NULL DEFAULT 0,
                buyer_character_id INT NOT NULL DEFAULT 0,
                status VARCHAR(64) NOT NULL DEFAULT '',
                details TEXT NOT NULL DEFAULT '',
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> CompletePixPaymentAsync(
        string paymentId,
        string preferenceId,
        long listingId,
        int buyerAccountId,
        int buyerCharacterId,
        string status,
        string rawDetails)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await UpsertPaymentAuditAsync(conn, tx, paymentId, preferenceId, listingId, buyerAccountId, buyerCharacterId, status, rawDetails);

            if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                await tx.CommitAsync();
                return $"Pagamento {paymentId} registrado como {status}.";
            }

            var listing = await LoadPixListingForUpdateAsync(conn, tx, listingId);
            if (listing == null)
            {
                await AddMarketplaceAuditAsync(conn, tx, listingId, buyerAccountId, buyerCharacterId, "pix_payment_no_listing", paymentId);
                await tx.CommitAsync();
                return "Pagamento aprovado, mas anuncio nao esta disponivel para entrega.";
            }

            if (!string.IsNullOrWhiteSpace(listing.MercadoPagoPaymentId) || listing.Status == "sold")
            {
                await tx.CommitAsync();
                return "Pagamento ja processado anteriormente.";
            }

            if (listing.SellerCharacterId == buyerCharacterId)
            {
                await MarkPixDeliveryFailureAsync(conn, tx, listingId, paymentId, "buyer_is_seller");
                await tx.CommitAsync();
                return "Compra bloqueada: vendedor nao pode comprar o proprio anuncio.";
            }

            if (listing.ListingType == 1)
            {
                int slot = await FindFreeInventorySlotAsync(conn, tx, buyerAccountId, buyerCharacterId);
                if (slot < 0)
                {
                    await MarkPixDeliveryFailureAsync(conn, tx, listingId, paymentId, "inventory_full");
                    await tx.CommitAsync();
                    return "Pagamento aprovado, mas o inventario do comprador esta cheio.";
                }

                await InsertInventoryItemAsync(conn, tx, buyerCharacterId, slot, listing.ItemId, listing.Quantity, listing.RefineLevel, listing.RollData);
            }
            else
            {
                await using var giveGold = conn.CreateCommand();
                giveGold.Transaction = tx;
                giveGold.CommandText = "UPDATE characters SET gold = gold + @gold WHERE id = @character";
                giveGold.Parameters.AddWithValue("@gold", listing.GoldAmount);
                giveGold.Parameters.AddWithValue("@character", buyerCharacterId);
                await giveGold.ExecuteNonQueryAsync();
            }

            int taxCents = (int)Math.Ceiling(listing.PriceTotalCents * 0.20);
            await using (var sold = conn.CreateCommand())
            {
                sold.Transaction = tx;
                sold.CommandText = """
                    UPDATE marketplace_listings
                    SET status = 'sold',
                        buyer_account_id = @buyerAccount,
                        buyer_character_id = @buyerCharacter,
                        buyer_name = COALESCE((SELECT name FROM characters WHERE id = @buyerCharacter), ''),
                        mercado_pago_payment_id = @paymentId,
                        payment_status = 'approved',
                        updated_at = CURRENT_TIMESTAMP,
                        sold_at = CURRENT_TIMESTAMP
                    WHERE id = @listing
                      AND status IN ('active', 'pending_payment')
                      AND currency_type = 2
                    """;
                sold.Parameters.AddWithValue("@listing", listingId);
                sold.Parameters.AddWithValue("@buyerAccount", buyerAccountId);
                sold.Parameters.AddWithValue("@buyerCharacter", buyerCharacterId);
                sold.Parameters.AddWithValue("@paymentId", paymentId);
                await sold.ExecuteNonQueryAsync();
            }

            await AddMarketplaceAuditAsync(conn, tx, listingId, buyerAccountId, buyerCharacterId, "pix_payment_delivered",
                $"payment={paymentId};tax_cents={taxCents};server_fee=20%");

            await tx.CommitAsync();
            return "Pagamento aprovado e entrega realizada.";
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task UpsertPaymentAuditAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string paymentId,
        string preferenceId,
        long listingId,
        int buyerAccountId,
        int buyerCharacterId,
        string status,
        string rawDetails)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO marketplace_pix_payments
                (payment_id, preference_id, listing_id, buyer_account_id, buyer_character_id, status, details)
            VALUES
                (@payment, @preference, @listing, @buyerAccount, @buyerCharacter, @status, @details)
            ON CONFLICT (payment_id) DO UPDATE
            SET status = EXCLUDED.status,
                details = EXCLUDED.details,
                updated_at = CURRENT_TIMESTAMP
            """;
        cmd.Parameters.AddWithValue("@payment", paymentId);
        cmd.Parameters.AddWithValue("@preference", preferenceId);
        cmd.Parameters.AddWithValue("@listing", listingId);
        cmd.Parameters.AddWithValue("@buyerAccount", buyerAccountId);
        cmd.Parameters.AddWithValue("@buyerCharacter", buyerCharacterId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@details", rawDetails.Length > 4000 ? rawDetails[..4000] : rawDetails);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class PixListingRow
    {
        public int SellerCharacterId { get; init; }
        public int ListingType { get; init; }
        public int ItemId { get; init; }
        public int Quantity { get; init; }
        public int GoldAmount { get; init; }
        public int PriceTotalCents { get; init; }
        public int RefineLevel { get; init; }
        public string RollData { get; init; } = "";
        public string Status { get; init; } = "";
        public string MercadoPagoPaymentId { get; init; } = "";
    }

    private static async Task<PixListingRow?> LoadPixListingForUpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long listingId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT seller_character_id, listing_type, item_id, quantity, gold_amount, price_total_cents,
                   refine_level, roll_data, status, mercado_pago_payment_id
            FROM marketplace_listings
            WHERE id = @listing
              AND currency_type = 2
              AND status IN ('active', 'pending_payment')
            FOR UPDATE
            """;
        cmd.Parameters.AddWithValue("@listing", listingId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new PixListingRow
        {
            SellerCharacterId = reader.GetInt32(0),
            ListingType = reader.GetInt16(1),
            ItemId = reader.GetInt32(2),
            Quantity = reader.GetInt32(3),
            GoldAmount = reader.GetInt32(4),
            PriceTotalCents = reader.GetInt32(5),
            RefineLevel = reader.GetInt32(6),
            RollData = reader.GetString(7),
            Status = reader.GetString(8),
            MercadoPagoPaymentId = reader.GetString(9),
        };
    }

    private static async Task<int> FindFreeInventorySlotAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int accountId, int characterId)
    {
        int slotLimit = DefaultInventorySlots;
        await using (var limitCmd = conn.CreateCommand())
        {
            limitCmd.Transaction = tx;
            limitCmd.CommandText = "SELECT COALESCE(character_slots, @defaultSlots) FROM accounts WHERE id = @account";
            limitCmd.Parameters.AddWithValue("@defaultSlots", DefaultInventorySlots);
            limitCmd.Parameters.AddWithValue("@account", accountId);
            var value = await limitCmd.ExecuteScalarAsync();
            if (value != null)
                slotLimit = Math.Clamp(Convert.ToInt32(value), DefaultInventorySlots, 240);
        }

        var used = new HashSet<int>();
        await using (var usedCmd = conn.CreateCommand())
        {
            usedCmd.Transaction = tx;
            usedCmd.CommandText = "SELECT slot FROM items WHERE character_id = @character AND slot >= 0 AND slot < @limit";
            usedCmd.Parameters.AddWithValue("@character", characterId);
            usedCmd.Parameters.AddWithValue("@limit", slotLimit);
            await using var reader = await usedCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                used.Add(reader.GetInt32(0));
        }

        for (int slot = 0; slot < slotLimit; slot++)
        {
            if (!used.Contains(slot))
                return slot;
        }

        return -1;
    }

    private static async Task InsertInventoryItemAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        int characterId,
        int slot,
        int itemId,
        int quantity,
        int refineLevel,
        string rollData)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO items (character_id, slot, item_id, quantity, refine_level, roll_data)
            VALUES (@character, @slot, @item, @quantity, @refine, @roll)
            """;
        cmd.Parameters.AddWithValue("@character", characterId);
        cmd.Parameters.AddWithValue("@slot", slot);
        cmd.Parameters.AddWithValue("@item", itemId);
        cmd.Parameters.AddWithValue("@quantity", quantity);
        cmd.Parameters.AddWithValue("@refine", refineLevel);
        cmd.Parameters.AddWithValue("@roll", rollData);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task MarkPixDeliveryFailureAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long listingId, string paymentId, string reason)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE marketplace_listings
            SET status = 'delivery_failed',
                mercado_pago_payment_id = @payment,
                payment_status = @reason,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @listing
            """;
        cmd.Parameters.AddWithValue("@listing", listingId);
        cmd.Parameters.AddWithValue("@payment", paymentId);
        cmd.Parameters.AddWithValue("@reason", reason);
        await cmd.ExecuteNonQueryAsync();

        await AddMarketplaceAuditAsync(conn, tx, listingId, 0, 0, "pix_delivery_failed", $"payment={paymentId};reason={reason}");
    }

    private static async Task AddMarketplaceAuditAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        long listingId,
        int actorAccountId,
        int actorCharacterId,
        string action,
        string details)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO marketplace_audit_logs (listing_id, actor_account_id, actor_character_id, action, details)
            VALUES (@listing, @account, @character, @action, @details)
            """;
        cmd.Parameters.AddWithValue("@listing", listingId);
        cmd.Parameters.AddWithValue("@account", actorAccountId);
        cmd.Parameters.AddWithValue("@character", actorCharacterId);
        cmd.Parameters.AddWithValue("@action", action);
        cmd.Parameters.AddWithValue("@details", details);
        await cmd.ExecuteNonQueryAsync();
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
