using System.Security.Cryptography;
using System.Text;
using Mithara.Server.Entities;
using Npgsql;

namespace Mithara.Server.Database;

public class DatabaseManager
{
    private readonly string _connectionString;

    public DatabaseManager(string host, int port, string database, string user, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = user,
            Password = password,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 10,
            ConnectionIdleLifetime = 30,
            Timeout = 30,
        };
        _connectionString = builder.ToString();
    }

    public void Initialize()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts (
                id SERIAL PRIMARY KEY,
                username VARCHAR(255) UNIQUE NOT NULL,
                password_hash VARCHAR(255) NOT NULL,
                security_question VARCHAR(255) NOT NULL DEFAULT '',
                security_answer VARCHAR(255) NOT NULL DEFAULT '',
                salt VARCHAR(255) NOT NULL DEFAULT '',
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS characters (
                id SERIAL PRIMARY KEY,
                account_id INT NOT NULL,
                slot_index INT NOT NULL DEFAULT 0,
                name VARCHAR(255) NOT NULL,
                class VARCHAR(50) NOT NULL,
                race VARCHAR(50) NOT NULL,
                level INT NOT NULL DEFAULT 1,
                xp BIGINT NOT NULL DEFAULT 0,
                forca INT NOT NULL DEFAULT 0,
                agilidade INT NOT NULL DEFAULT 0,
                destreza INT NOT NULL DEFAULT 0,
                inteligencia INT NOT NULL DEFAULT 0,
                pos_x DOUBLE PRECISION NOT NULL DEFAULT 1000,
                pos_y DOUBLE PRECISION NOT NULL DEFAULT 1000,
                bank_gold INT NOT NULL DEFAULT 0,
                gold INT NOT NULL DEFAULT 50,
                stat_points INT NOT NULL DEFAULT 10,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS items (
                id SERIAL PRIMARY KEY,
                character_id INT NOT NULL,
                slot INT NOT NULL DEFAULT 0,
                item_id INT NOT NULL DEFAULT 0,
                quantity INT NOT NULL DEFAULT 1,
                refine_level INT NOT NULL DEFAULT 0,
                roll_data TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS guilds (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) UNIQUE NOT NULL,
                level INT NOT NULL DEFAULT 1,
                xp INT NOT NULL DEFAULT 0,
                skill_points INT NOT NULL DEFAULT 0,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS guild_members (
                guild_id INT NOT NULL,
                entity_id BIGINT NOT NULL,
                name VARCHAR(255) NOT NULL,
                rank INT NOT NULL DEFAULT 4,
                PRIMARY KEY (guild_id, entity_id),
                FOREIGN KEY (guild_id) REFERENCES guilds(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS guild_skills (
                guild_id INT NOT NULL,
                skill_id VARCHAR(100) NOT NULL,
                level INT NOT NULL DEFAULT 0,
                PRIMARY KEY (guild_id, skill_id),
                FOREIGN KEY (guild_id) REFERENCES guilds(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS player_quests (
                id SERIAL PRIMARY KEY,
                character_id INT NOT NULL,
                quest_id INT NOT NULL,
                progress TEXT NOT NULL,
                completed SMALLINT NOT NULL DEFAULT 0,
                claimed SMALLINT NOT NULL DEFAULT 0,
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE,
                UNIQUE (character_id, quest_id)
            );

            CREATE TABLE IF NOT EXISTS character_talents (
                character_id INT NOT NULL,
                node_id VARCHAR(150) NOT NULL,
                unlocked_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (character_id, node_id),
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS character_skill_slots (
                character_id INT NOT NULL,
                slot_index INT NOT NULL,
                skill_id INT NOT NULL DEFAULT 0,
                PRIMARY KEY (character_id, slot_index),
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS character_pets (
                id SERIAL PRIMARY KEY,
                character_id INT NOT NULL,
                pet_id INT NOT NULL,
                pet_name VARCHAR(255) NOT NULL DEFAULT '',
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS lojinhas (
                id BIGSERIAL PRIMARY KEY,
                owner_character_id INT NOT NULL,
                owner_name VARCHAR(255) NOT NULL DEFAULT '',
                shop_name VARCHAR(255) NOT NULL DEFAULT '',
                owner_class VARCHAR(80) NOT NULL DEFAULT '',
                owner_race VARCHAR(80) NOT NULL DEFAULT '',
                is_open SMALLINT NOT NULL DEFAULT 0,
                x DOUBLE PRECISION NOT NULL,
                y DOUBLE PRECISION NOT NULL,
                channel_id INT NOT NULL DEFAULT 0,
                gold_earned INT NOT NULL DEFAULT 0,
                max_slots INT NOT NULL DEFAULT 5,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (owner_character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS lojinha_items (
                id BIGSERIAL PRIMARY KEY,
                lojinha_id BIGINT NOT NULL,
                slot INT NOT NULL DEFAULT 0,
                item_id INT NOT NULL,
                quantity INT NOT NULL DEFAULT 1,
                price INT NOT NULL DEFAULT 0,
                roll_data TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (lojinha_id) REFERENCES lojinhas(id) ON DELETE CASCADE
            );

            ALTER TABLE lojinhas ADD COLUMN IF NOT EXISTS max_slots INT NOT NULL DEFAULT 5;
            ALTER TABLE lojinhas ADD COLUMN IF NOT EXISTS shop_name VARCHAR(255) NOT NULL DEFAULT '';
            ALTER TABLE lojinhas ADD COLUMN IF NOT EXISTS owner_class VARCHAR(80) NOT NULL DEFAULT '';
            ALTER TABLE lojinhas ADD COLUMN IF NOT EXISTS owner_race VARCHAR(80) NOT NULL DEFAULT '';
            ALTER TABLE lojinhas ADD COLUMN IF NOT EXISTS is_open SMALLINT NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS item_definitions (
                id INT PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                type INT NOT NULL DEFAULT 0,
                max_stack INT NOT NULL DEFAULT 1,
                is_stackable SMALLINT NOT NULL DEFAULT 0,
                is_bag SMALLINT NOT NULL DEFAULT 0,
                extra_slots INT NOT NULL DEFAULT 0,
                forca INT NOT NULL DEFAULT 0,
                agilidade INT NOT NULL DEFAULT 0,
                destreza INT NOT NULL DEFAULT 0,
                inteligencia INT NOT NULL DEFAULT 0,
                base_attack INT NOT NULL DEFAULT 0,
                defense INT NOT NULL DEFAULT 0,
                magic_defense INT NOT NULL DEFAULT 0,
                hp INT NOT NULL DEFAULT 0,
                mana INT NOT NULL DEFAULT 0,
                evasion REAL NOT NULL DEFAULT 0,
                buy_price INT NOT NULL DEFAULT 0,
                affix_pool TEXT NOT NULL DEFAULT ''
                ,definition_data TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();

        TryAddColumns(conn);
        TryNormalizeItems(conn);
        TryAddIndexes(conn);
        Logger.Info("Banco de dados PostgreSQL inicializado.");
    }

    private void TryAddColumns(NpgsqlConnection conn)
    {
        var columns = new (string table, string column, string type)[]
        {
            ("accounts", "security_question", "VARCHAR(255) NOT NULL DEFAULT ''"),
            ("accounts", "security_answer", "VARCHAR(255) NOT NULL DEFAULT ''"),
            ("accounts", "salt", "VARCHAR(255) NOT NULL DEFAULT ''"),
            ("items", "refine_level", "INT NOT NULL DEFAULT 0"),
            ("items", "roll_data", "TEXT NOT NULL DEFAULT ''"),
            ("item_definitions", "magic_defense", "INT NOT NULL DEFAULT 0"),
            ("item_definitions", "hp", "INT NOT NULL DEFAULT 0"),
            ("item_definitions", "mana", "INT NOT NULL DEFAULT 0"),
            ("item_definitions", "evasion", "REAL NOT NULL DEFAULT 0"),
            ("item_definitions", "definition_data", "TEXT NOT NULL DEFAULT ''"),
            ("accounts", "vip_expiry", "TIMESTAMP NOT NULL DEFAULT '2000-01-01 00:00:00'"),
            ("characters", "current_map", "VARCHAR(64) NOT NULL DEFAULT 'main'"),
        };

        foreach (var (table, column, type) in columns)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {column} {type}";
                cmd.ExecuteNonQuery();
                Logger.Info($"Coluna '{column}' adicionada em '{table}'.");
            }
            catch (Exception ex)
            {
                Logger.Info($"N?o foi poss?vel adicionar coluna '{column}' em '{table}': {ex.Message}");
            }
        }
    }

    private void TryNormalizeItems(NpgsqlConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM items
                WHERE id IN (
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY character_id, slot
                                   ORDER BY id DESC
                               ) AS rn
                        FROM items
                    ) t
                    WHERE t.rn > 1
                )
                """;
            int removed = cmd.ExecuteNonQuery();
            if (removed > 0)
                Logger.Info($"Normalizacao de items: {removed} duplicata(s) de slot removida(s).");
        }
        catch (Exception ex)
        {
            Logger.Info($"N?o foi poss?vel normalizar items: {ex.Message}");
        }
    }

    private void TryAddIndexes(NpgsqlConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_items_character_slot ON items(character_id, slot)";
            cmd.ExecuteNonQuery();
            Logger.Info("Indice unico ux_items_character_slot verificado.");
        }
        catch (Exception ex)
        {
            Logger.Info($"N?o foi poss?vel criar ?ndice ?nico de items: {ex.Message}");
        }
    }

    public int? CreateAccount(string username, string password, string securityQuestion = "", string securityAnswer = "")
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM accounts WHERE username = @u";
        check.Parameters.AddWithValue("@u", username);
        var exists = check.ExecuteScalar();
        if (exists != null) return null;

        using var cmd = conn.CreateCommand();
        string salt = GenerateSalt();
        cmd.CommandText = "INSERT INTO accounts (username, password_hash, security_question, security_answer, salt) VALUES (@u, @p, @q, @a, @s) RETURNING id";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", HashPassword(password, salt));
        cmd.Parameters.AddWithValue("@q", securityQuestion);
        cmd.Parameters.AddWithValue("@a", HashPassword(securityAnswer, salt));
        cmd.Parameters.AddWithValue("@s", salt);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int? LoginAccount(string username, string password)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, password_hash, salt FROM accounts WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var hash = reader.GetString(1);
        var salt = reader.IsDBNull(2) ? "" : reader.GetString(2);
        if (hash != HashPassword(password, salt)) return null;

        return reader.GetInt32(0);
    }

    public int CreateCharacter(int accountId, string name, string className, string race)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var slotCmd = conn.CreateCommand();
        slotCmd.CommandText = "SELECT COALESCE(MAX(slot_index), -1) + 1 FROM characters WHERE account_id = @a";
        slotCmd.Parameters.AddWithValue("@a", accountId);
        int slotIndex = Convert.ToInt32(slotCmd.ExecuteScalar());

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO characters (account_id, slot_index, name, class, race, level)
            VALUES (@a, @s, @n, @c, @r, 1)
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("@a", accountId);
        cmd.Parameters.AddWithValue("@s", slotIndex);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@c", className);
        cmd.Parameters.AddWithValue("@r", race);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<CharacterRow> GetCharacters(int accountId)
    {
        var result = new List<CharacterRow>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, slot_index, name, class, race, level, xp, forca, agilidade, destreza, inteligencia, pos_x, pos_y, bank_gold, gold, stat_points, current_map FROM characters WHERE account_id = @a ORDER BY slot_index";
        cmd.Parameters.AddWithValue("@a", accountId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CharacterRow
            {
                Id = reader.GetInt32(0),
                SlotIndex = reader.GetInt32(1),
                Name = reader.GetString(2),
                Class = reader.GetString(3),
                Race = reader.GetString(4),
                Level = reader.GetInt32(5),
                Xp = reader.GetInt64(6),
                Forca = reader.GetInt32(7),
                Agilidade = reader.GetInt32(8),
                Destreza = reader.GetInt32(9),
                Inteligencia = reader.GetInt32(10),
                PosX = (float)reader.GetDouble(11),
                PosY = (float)reader.GetDouble(12),
                BankGold = reader.GetInt32(13),
                Gold = reader.GetInt32(14),
                StatPoints = reader.GetInt32(15),
                CurrentMap = reader.GetString(16),
            });
        }
        return result;
    }

    public string? GetSecurityQuestion(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT security_question FROM accounts WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return null;
        string q = (string)result;
        return string.IsNullOrEmpty(q) ? null : q;
    }

    public bool RecoverPassword(string username, string answer, string newPassword)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, security_answer, salt FROM accounts WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;

        var storedHash = reader.GetString(1);
        var salt = reader.IsDBNull(2) ? "" : reader.GetString(2);
        if (storedHash != HashPassword(answer, salt)) return false;

        int accountId = reader.GetInt32(0);
        reader.Close();

        string newSalt = GenerateSalt();
        using var update = conn.CreateCommand();
        update.CommandText = "UPDATE accounts SET password_hash = @p, salt = @s WHERE id = @i";
        update.Parameters.AddWithValue("@p", HashPassword(newPassword, newSalt));
        update.Parameters.AddWithValue("@s", newSalt);
        update.Parameters.AddWithValue("@i", accountId);
        update.ExecuteNonQuery();
        return true;
    }

    public void SaveCharacterXp(int characterId, long xp)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET xp = @x WHERE id = @i";
        cmd.Parameters.AddWithValue("@x", xp);
        cmd.Parameters.AddWithValue("@i", characterId);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterLevel(int characterId, int level)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET level = @l WHERE id = @i";
        cmd.Parameters.AddWithValue("@l", level);
        cmd.Parameters.AddWithValue("@i", characterId);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterPosition(int characterId, float x, float y, string currentMap = "main")
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET pos_x = @x, pos_y = @y, current_map = @m WHERE id = @i";
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y);
        cmd.Parameters.AddWithValue("@m", currentMap);
        cmd.Parameters.AddWithValue("@i", characterId);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterBankGold(int characterId, int bankGold)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET bank_gold = @g WHERE id = @i";
        cmd.Parameters.AddWithValue("@g", bankGold);
        cmd.Parameters.AddWithValue("@i", characterId);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterGold(int characterId, int gold)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET gold = @g WHERE id = @i";
        cmd.Parameters.AddWithValue("@g", gold);
        cmd.Parameters.AddWithValue("@i", characterId);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterStats(int characterId, int forca, int agilidade, int destreza, int inteligencia, int statPoints)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET forca = @f, agilidade = @a, destreza = @d, inteligencia = @i, stat_points = @s WHERE id = @c";
        cmd.Parameters.AddWithValue("@f", forca);
        cmd.Parameters.AddWithValue("@a", agilidade);
        cmd.Parameters.AddWithValue("@d", destreza);
        cmd.Parameters.AddWithValue("@i", inteligencia);
        cmd.Parameters.AddWithValue("@s", statPoints);
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.ExecuteNonQuery();
    }

    public DateTime LoadVipExpiry(int accountId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT vip_expiry FROM accounts WHERE id = @a";
        cmd.Parameters.AddWithValue("@a", accountId);
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return DateTime.SpecifyKind((DateTime)result, DateTimeKind.Utc);
    }

    public void SaveVipExpiry(int accountId, DateTime expiry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE accounts SET vip_expiry = @e WHERE id = @a";
        cmd.Parameters.AddWithValue("@e", expiry);
        cmd.Parameters.AddWithValue("@a", accountId);
        cmd.ExecuteNonQuery();
    }

    public List<Mithara.Server.Entities.ItemInstance> LoadItems(int characterId)
    {
        var result = new List<Mithara.Server.Entities.ItemInstance>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, item_id, slot, quantity, refine_level, roll_data FROM items WHERE character_id = @c ORDER BY slot";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var instance = new Mithara.Server.Entities.ItemInstance
            {
                DbId = reader.GetInt32(0),
                ItemId = reader.GetInt32(1),
                Slot = reader.GetInt32(2),
                Quantity = reader.GetInt32(3),
                RefineLevel = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            };
            if (!reader.IsDBNull(5) && !string.IsNullOrWhiteSpace(reader.GetString(5)))
            {
                try
                {
                    instance.Roll = System.Text.Json.JsonSerializer.Deserialize<Mithara.Server.Entities.ItemRoll>(reader.GetString(5)) ?? new();
                }
                catch (System.Text.Json.JsonException)
                {
                    instance.Roll = new();
                }
            }
            result.Add(instance);
        }
        return result;
    }

    public List<Mithara.Server.Entities.ItemInstance> LoadBankItems(int characterId)
    {
        var result = new List<Mithara.Server.Entities.ItemInstance>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, item_id, slot, quantity, refine_level, roll_data FROM items WHERE character_id = @c AND slot >= 1000 AND slot < 1100 ORDER BY slot";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var instance = new Mithara.Server.Entities.ItemInstance
            {
                DbId = reader.GetInt32(0),
                ItemId = reader.GetInt32(1),
                Slot = reader.GetInt32(2) - 1000,
                Quantity = reader.GetInt32(3),
                RefineLevel = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            };
            if (!reader.IsDBNull(5) && !string.IsNullOrWhiteSpace(reader.GetString(5)))
            {
                try
                {
                    instance.Roll = System.Text.Json.JsonSerializer.Deserialize<Mithara.Server.Entities.ItemRoll>(reader.GetString(5)) ?? new();
                }
                catch (System.Text.Json.JsonException)
                {
                    instance.Roll = new();
                }
            }
            result.Add(instance);
        }
        return result;
    }

    public void SaveItem(int characterId, Mithara.Server.Entities.ItemInstance item)
    {
        Mithara.Server.Entities.ItemRoller.EnsureRolled(item);
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        if (item.DbId > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET slot = @s, item_id = @ii, quantity = @q, refine_level = @r, roll_data = @roll WHERE id = @i AND character_id = @c";
            cmd.Parameters.AddWithValue("@s", item.Slot);
            cmd.Parameters.AddWithValue("@ii", item.ItemId);
            cmd.Parameters.AddWithValue("@q", item.Quantity);
            cmd.Parameters.AddWithValue("@r", item.RefineLevel);
            cmd.Parameters.AddWithValue("@roll", System.Text.Json.JsonSerializer.Serialize(item.Roll));
            cmd.Parameters.AddWithValue("@i", item.DbId);
            cmd.Parameters.AddWithValue("@c", characterId);
            int affected = cmd.ExecuteNonQuery();
            if (affected > 0)
                return;

            item.DbId = 0;
        }

        if (item.DbId <= 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO items (character_id, slot, item_id, quantity, refine_level, roll_data)
                VALUES (@c, @s, @ii, @q, @r, @roll)
                ON CONFLICT (character_id, slot)
                DO UPDATE SET item_id = EXCLUDED.item_id,
                              quantity = EXCLUDED.quantity,
                              refine_level = EXCLUDED.refine_level,
                              roll_data = EXCLUDED.roll_data
                RETURNING id
                """;
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", item.Slot);
            cmd.Parameters.AddWithValue("@ii", item.ItemId);
            cmd.Parameters.AddWithValue("@q", item.Quantity);
            cmd.Parameters.AddWithValue("@r", item.RefineLevel);
            cmd.Parameters.AddWithValue("@roll", System.Text.Json.JsonSerializer.Serialize(item.Roll));
            item.DbId = Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void DeleteItem(int characterId, int dbId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = @i AND character_id = @c";
        cmd.Parameters.AddWithValue("@i", dbId);
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteItemBySlot(int characterId, int slot)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE character_id = @c AND slot = @s";
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@s", slot);
        cmd.ExecuteNonQuery();
    }

    public void SavePet(int characterId, int petId, string petName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM character_pets WHERE character_id = @c AND pet_id = @p";
        check.Parameters.AddWithValue("@c", characterId);
        check.Parameters.AddWithValue("@p", petId);
        var existing = check.ExecuteScalar();
        if (existing != null) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO character_pets (character_id, pet_id, pet_name) VALUES (@c, @p, @n)";
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@p", petId);
        cmd.Parameters.AddWithValue("@n", petName);
        cmd.ExecuteNonQuery();
    }

    public List<(int petId, string petName)> LoadPets(int characterId)
    {
        var result = new List<(int, string)>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pet_id, pet_name FROM character_pets WHERE character_id = @c ORDER BY id";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        }
        return result;
    }

    public ulong SaveLojinha(LojinhaEntity lojinha)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lojinhas (owner_character_id, owner_name, shop_name, owner_class, owner_race, is_open, x, y, channel_id, gold_earned, max_slots)
            VALUES (@o, @n, @sn, @oc, @or, @io, @x, @y, @c, @g, @m)
            RETURNING id";
        cmd.Parameters.AddWithValue("@o", lojinha.OwnerCharacterId);
        cmd.Parameters.AddWithValue("@n", lojinha.OwnerName);
        cmd.Parameters.AddWithValue("@sn", lojinha.ShopName);
        cmd.Parameters.AddWithValue("@oc", lojinha.OwnerClass);
        cmd.Parameters.AddWithValue("@or", lojinha.OwnerRace);
        cmd.Parameters.AddWithValue("@io", lojinha.IsOpen ? 1 : 0);
        cmd.Parameters.AddWithValue("@x", lojinha.X);
        cmd.Parameters.AddWithValue("@y", lojinha.Y);
        cmd.Parameters.AddWithValue("@c", lojinha.ChannelId);
        cmd.Parameters.AddWithValue("@g", lojinha.GoldEarned);
        cmd.Parameters.AddWithValue("@m", lojinha.MaxSlots);
        return Convert.ToUInt64(cmd.ExecuteScalar());
    }

    public void SaveLojinhaItems(ulong lojinhaId, List<LojinhaItem> items)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM lojinha_items WHERE lojinha_id = @l";
        del.Parameters.AddWithValue("@l", (long)lojinhaId);
        del.ExecuteNonQuery();

        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO lojinha_items (lojinha_id, slot, item_id, quantity, price, roll_data)
                VALUES (@l, @s, @i, @q, @p, @r)";
            cmd.Parameters.AddWithValue("@l", (long)lojinhaId);
            cmd.Parameters.AddWithValue("@s", item.Slot);
            cmd.Parameters.AddWithValue("@i", item.ItemId);
            cmd.Parameters.AddWithValue("@q", item.Quantity);
            cmd.Parameters.AddWithValue("@p", item.PricePerUnit);
            cmd.Parameters.AddWithValue("@r", item.RollData);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteLojinha(ulong lojinhaDbId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM lojinhas WHERE id = @l";
        cmd.Parameters.AddWithValue("@l", (long)lojinhaDbId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateLojinhaGold(ulong lojinhaDbId, int goldEarned)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE lojinhas SET gold_earned = @g WHERE id = @l";
        cmd.Parameters.AddWithValue("@g", goldEarned);
        cmd.Parameters.AddWithValue("@l", (long)lojinhaDbId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateLojinhaConfig(ulong lojinhaDbId, string shopName, bool isOpen)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE lojinhas SET shop_name = @n, is_open = @o WHERE id = @l";
        cmd.Parameters.AddWithValue("@n", shopName);
        cmd.Parameters.AddWithValue("@o", isOpen ? 1 : 0);
        cmd.Parameters.AddWithValue("@l", (long)lojinhaDbId);
        cmd.ExecuteNonQuery();
    }

    public void LoadAllLojinhas(Action<LojinhaEntity> onLojinha, Action<ulong, LojinhaItem> onItem)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, owner_character_id, owner_name, shop_name, owner_class, owner_race, is_open, x, y, channel_id, gold_earned, max_slots, created_at FROM lojinhas ORDER BY id";
        using var reader = cmd.ExecuteReader();
        var lojinhaById = new Dictionary<ulong, LojinhaEntity>();
        while (reader.Read())
        {
            var lojinha = new LojinhaEntity((float)reader.GetDouble(7), (float)reader.GetDouble(8))
            {
                OwnerCharacterId = reader.GetInt32(1),
                OwnerName = reader.GetString(2),
                ShopName = reader.GetString(3),
                OwnerClass = reader.GetString(4),
                OwnerRace = reader.GetString(5),
                IsOpen = reader.GetInt16(6) != 0,
                ChannelId = reader.GetInt32(9),
                GoldEarned = reader.GetInt32(10),
                MaxSlots = reader.GetInt32(11),
                CreatedAt = reader.GetDateTime(12),
            };
            var dbId = reader.GetInt64(0);
            lojinha.DbId = (ulong)dbId;
            lojinhaById[lojinha.Id] = lojinha;
            onLojinha(lojinha);
        }
        reader.Close();

        if (lojinhaById.Count == 0) return;

        using var itemCmd = conn.CreateCommand();
        itemCmd.CommandText = "SELECT lojinha_id, slot, item_id, quantity, price, roll_data FROM lojinha_items ORDER BY lojinha_id, slot";
        using var itemReader = itemCmd.ExecuteReader();
        while (itemReader.Read())
        {
            var lojinhaId = itemReader.GetInt64(0);
            if (!lojinhaById.TryGetValue((ulong)lojinhaId, out var lojinha)) continue;
            var item = new LojinhaItem
            {
                Slot = itemReader.GetInt32(1),
                ItemId = itemReader.GetInt32(2),
                Quantity = itemReader.GetInt32(3),
                PricePerUnit = itemReader.GetInt32(4),
                RollData = itemReader.IsDBNull(5) ? "" : itemReader.GetString(5),
            };
            lojinha.Items.Add(item);
            onItem(lojinha.Id, item);
        }
    }

    public void SaveGuild(int guildId, string name, int level, int xp, int skillPoints)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO guilds (id, name, level, xp, skill_points)
            VALUES (@i, @n, @l, @x, @s)
            ON CONFLICT (id) DO UPDATE SET name=@n, level=@l, xp=@x, skill_points=@s";
        cmd.Parameters.AddWithValue("@i", guildId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@l", level);
        cmd.Parameters.AddWithValue("@x", xp);
        cmd.Parameters.AddWithValue("@s", skillPoints);
        cmd.ExecuteNonQuery();
    }

    public void SaveGuildMember(int guildId, ulong entityId, string name, int rank)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using (var cleanup = conn.CreateCommand())
        {
            cleanup.Transaction = tx;
            cleanup.CommandText = @"
                DELETE FROM guild_members
                WHERE name = @n AND (guild_id <> @g OR entity_id <> @e)";
            cleanup.Parameters.AddWithValue("@n", name);
            cleanup.Parameters.AddWithValue("@g", guildId);
            cleanup.Parameters.AddWithValue("@e", (long)entityId);
            cleanup.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO guild_members (guild_id, entity_id, name, rank)
                VALUES (@g, @e, @n, @r)
                ON CONFLICT (guild_id, entity_id) DO UPDATE SET name=@n, rank=@r";
            cmd.Parameters.AddWithValue("@g", guildId);
            cmd.Parameters.AddWithValue("@e", (long)entityId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@r", rank);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void DeleteGuildMember(int guildId, ulong entityId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM guild_members WHERE guild_id = @g AND entity_id = @e";
        cmd.Parameters.AddWithValue("@g", guildId);
        cmd.Parameters.AddWithValue("@e", (long)entityId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGuildMemberByName(int guildId, string name)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM guild_members WHERE guild_id = @g AND name = @n";
        cmd.Parameters.AddWithValue("@g", guildId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGuild(int guildId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using (var members = conn.CreateCommand())
        {
            members.Transaction = tx;
            members.CommandText = "DELETE FROM guild_members WHERE guild_id = @i";
            members.Parameters.AddWithValue("@i", guildId);
            members.ExecuteNonQuery();
        }

        using (var skills = conn.CreateCommand())
        {
            skills.Transaction = tx;
            skills.CommandText = "DELETE FROM guild_skills WHERE guild_id = @i";
            skills.Parameters.AddWithValue("@i", guildId);
            skills.ExecuteNonQuery();
        }

        using (var guild = conn.CreateCommand())
        {
            guild.Transaction = tx;
            guild.CommandText = "DELETE FROM guilds WHERE id = @i";
            guild.Parameters.AddWithValue("@i", guildId);
            guild.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void SaveGuildSkill(int guildId, string skillId, int level)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO guild_skills (guild_id, skill_id, level)
            VALUES (@g, @s, @l)
            ON CONFLICT (guild_id, skill_id) DO UPDATE SET level=@l";
        cmd.Parameters.AddWithValue("@g", guildId);
        cmd.Parameters.AddWithValue("@s", skillId);
        cmd.Parameters.AddWithValue("@l", level);
        cmd.ExecuteNonQuery();
    }

    public void LoadAllGuilds(Action<int, string, int, int, int> onGuild,
        Action<int, ulong, string, int> onMember,
        Action<int, string, int> onSkill)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var gcmd = conn.CreateCommand();
        gcmd.CommandText = "SELECT id, name, level, xp, skill_points FROM guilds";
        using var greader = gcmd.ExecuteReader();
        while (greader.Read())
        {
            onGuild(greader.GetInt32(0), greader.GetString(1), greader.GetInt32(2),
                greader.GetInt32(3), greader.GetInt32(4));
        }
        greader.Close();

        using var mcmd = conn.CreateCommand();
        mcmd.CommandText = @"
            SELECT gm.guild_id, gm.entity_id, gm.name, gm.rank
            FROM guild_members gm
            INNER JOIN guilds g ON g.id = gm.guild_id";
        using var mreader = mcmd.ExecuteReader();
        while (mreader.Read())
        {
            onMember(mreader.GetInt32(0), (ulong)mreader.GetInt64(1), mreader.GetString(2), mreader.GetInt32(3));
        }
        mreader.Close();

        using var scmd = conn.CreateCommand();
        scmd.CommandText = "SELECT guild_id, skill_id, level FROM guild_skills";
        using var sreader = scmd.ExecuteReader();
        while (sreader.Read())
        {
            onSkill(sreader.GetInt32(0), sreader.GetString(1), sreader.GetInt32(2));
        }
    }

    public void DeleteCharacter(int characterId, string characterName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var tx = conn.BeginTransaction();

        int delLojinhaItemsRows = 0;
        using (var delLojinhaItems = conn.CreateCommand())
        {
            delLojinhaItems.Transaction = tx;
            delLojinhaItems.CommandText = """
                DELETE FROM lojinha_items
                WHERE lojinha_id IN (SELECT id FROM lojinhas WHERE owner_character_id = @c)
                """;
            delLojinhaItems.Parameters.AddWithValue("@c", characterId);
            delLojinhaItemsRows = delLojinhaItems.ExecuteNonQuery();
        }

        int delLojinhasRows = 0;
        using (var delLojinhas = conn.CreateCommand())
        {
            delLojinhas.Transaction = tx;
            delLojinhas.CommandText = "DELETE FROM lojinhas WHERE owner_character_id = @c";
            delLojinhas.Parameters.AddWithValue("@c", characterId);
            delLojinhasRows = delLojinhas.ExecuteNonQuery();
        }

        int delPetsRows = 0;
        using (var delPets = conn.CreateCommand())
        {
            delPets.Transaction = tx;
            delPets.CommandText = "DELETE FROM character_pets WHERE character_id = @c";
            delPets.Parameters.AddWithValue("@c", characterId);
            delPetsRows = delPets.ExecuteNonQuery();
        }

        using var delItems = conn.CreateCommand();
        delItems.Transaction = tx;
        delItems.CommandText = "DELETE FROM items WHERE character_id = @c";
        delItems.Parameters.AddWithValue("@c", characterId);
        int delItemsRows = delItems.ExecuteNonQuery();

        using var delQuests = conn.CreateCommand();
        delQuests.Transaction = tx;
        delQuests.CommandText = "DELETE FROM player_quests WHERE character_id = @c";
        delQuests.Parameters.AddWithValue("@c", characterId);
        int delQuestsRows = delQuests.ExecuteNonQuery();

        using var delTalents = conn.CreateCommand();
        delTalents.Transaction = tx;
        delTalents.CommandText = "DELETE FROM character_talents WHERE character_id = @c";
        delTalents.Parameters.AddWithValue("@c", characterId);
        int delTalentsRows = delTalents.ExecuteNonQuery();

        using var delSkillSlots = conn.CreateCommand();
        delSkillSlots.Transaction = tx;
        delSkillSlots.CommandText = "DELETE FROM character_skill_slots WHERE character_id = @c";
        delSkillSlots.Parameters.AddWithValue("@c", characterId);
        int delSkillSlotsRows = delSkillSlots.ExecuteNonQuery();

        using var delGuild = conn.CreateCommand();
        delGuild.Transaction = tx;
        delGuild.CommandText = "DELETE FROM guild_members WHERE name = @n";
        delGuild.Parameters.AddWithValue("@n", characterName);
        int delGuildRows = delGuild.ExecuteNonQuery();

        using var delChar = conn.CreateCommand();
        delChar.Transaction = tx;
        delChar.CommandText = "DELETE FROM characters WHERE id = @c";
        delChar.Parameters.AddWithValue("@c", characterId);
        int delCharRows = delChar.ExecuteNonQuery();

        tx.Commit();
        Logger.Info($"[DB] Personagem {characterId} ({characterName}) deletado: char={delCharRows}, itens={delItemsRows}, quests={delQuestsRows}, talentos={delTalentsRows}, skillSlots={delSkillSlotsRows}, pets={delPetsRows}, guild={delGuildRows}, lojinhas={delLojinhasRows}, lojinhaItens={delLojinhaItemsRows}.");
    }

    public List<(int questId, string progress, bool completed, bool claimed)> GetPlayerQuests(int characterId)
    {
        var result = new List<(int, string, bool, bool)>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT quest_id, progress, completed, claimed FROM player_quests WHERE character_id = @c";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2) != 0, reader.GetInt32(3) != 0));
        }
        return result;
    }

    public void SavePlayerQuest(int characterId, int questId, string progress, bool completed, bool claimed)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO player_quests (character_id, quest_id, progress, completed, claimed)
            VALUES (@c, @q, @p, @co, @cl)
            ON CONFLICT (character_id, quest_id) DO UPDATE SET progress = @p, completed = @co, claimed = @cl
            """;
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@q", questId);
        cmd.Parameters.AddWithValue("@p", progress);
        cmd.Parameters.AddWithValue("@co", completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@cl", claimed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public HashSet<string> GetCharacterTalents(int characterId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT node_id FROM character_talents WHERE character_id = @c";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public void SaveCharacterTalent(int characterId, string nodeId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO character_talents (character_id, node_id)
            VALUES (@c, @n)
            ON CONFLICT (character_id, node_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@n", nodeId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCharacterTalents(int characterId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM character_talents WHERE character_id = @c";
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.ExecuteNonQuery();
    }

    public int[] GetCharacterSkillSlots(int characterId, int slotCount = 20)
    {
        var result = new int[Math.Max(1, slotCount)];
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT slot_index, skill_id FROM character_skill_slots WHERE character_id = @c";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int slot = reader.GetInt32(0);
            int skillId = reader.GetInt32(1);
            if (slot >= 0 && slot < result.Length)
                result[slot] = Math.Max(0, skillId);
        }
        return result;
    }

    public void SaveCharacterSkillSlot(int characterId, int slotIndex, int skillId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        if (skillId <= 0)
        {
            cmd.CommandText = "DELETE FROM character_skill_slots WHERE character_id = @c AND slot_index = @s";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", slotIndex);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO character_skill_slots (character_id, slot_index, skill_id)
                VALUES (@c, @s, @k)
                ON CONFLICT (character_id, slot_index) DO UPDATE SET skill_id = @k
                """;
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", slotIndex);
            cmd.Parameters.AddWithValue("@k", skillId);
        }

        cmd.ExecuteNonQuery();
    }

    public void DeleteCharacterSkillSlots(int characterId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM character_skill_slots WHERE character_id = @c";
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.ExecuteNonQuery();
    }

    public void UpsertPlayerQuest(int characterId, int questId, string progress, bool completed, bool claimed)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM player_quests WHERE character_id = @c AND quest_id = @q";
        check.Parameters.AddWithValue("@c", characterId);
        check.Parameters.AddWithValue("@q", questId);
        var existingId = check.ExecuteScalar();

        using var cmd = conn.CreateCommand();
        if (existingId != null)
        {
            cmd.CommandText = "UPDATE player_quests SET progress = @p, completed = @co, claimed = @cl WHERE id = @i";
            cmd.Parameters.AddWithValue("@i", (int)(uint)existingId);
        }
        else
        {
            cmd.CommandText = "INSERT INTO player_quests (character_id, quest_id, progress, completed, claimed) VALUES (@c, @q, @p, @co, @cl)";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@q", questId);
        }
        cmd.Parameters.AddWithValue("@p", progress);
        cmd.Parameters.AddWithValue("@co", completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@cl", claimed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void SaveCharacterFull(int characterId, PlayerEntity player, int? bankGold = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE characters SET
                        pos_x = @x, pos_y = @y,
                        gold = @g,
                        level = @l,
                        xp = @xp,
                        forca = @f, agilidade = @a, destreza = @d, inteligencia = @i,
                        stat_points = @sp
                    WHERE id = @c
                    """;
                cmd.Parameters.AddWithValue("@x", player.X);
                cmd.Parameters.AddWithValue("@y", player.Y);
                cmd.Parameters.AddWithValue("@g", player.Gold);
                cmd.Parameters.AddWithValue("@l", player.Level);
                cmd.Parameters.AddWithValue("@xp", player.Experience);
                cmd.Parameters.AddWithValue("@f", player.BaseForca);
                cmd.Parameters.AddWithValue("@a", player.BaseAgilidade);
                cmd.Parameters.AddWithValue("@d", player.BaseDestreza);
                cmd.Parameters.AddWithValue("@i", player.BaseInteligencia);
                cmd.Parameters.AddWithValue("@sp", player.StatPoints);
                cmd.Parameters.AddWithValue("@c", characterId);
                cmd.ExecuteNonQuery();
            }

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM items WHERE character_id = @c";
                del.Parameters.AddWithValue("@c", characterId);
                del.ExecuteNonQuery();
            }

            foreach (var item in player.Items)
            {
                Mithara.Server.Entities.ItemRoller.EnsureRolled(item);
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO items (character_id, slot, item_id, quantity, refine_level, roll_data) VALUES (@c, @s, @ii, @q, @r, @roll) RETURNING id";
                ins.Parameters.AddWithValue("@c", characterId);
                ins.Parameters.AddWithValue("@s", item.Slot);
                ins.Parameters.AddWithValue("@ii", item.ItemId);
                ins.Parameters.AddWithValue("@q", item.Quantity);
                ins.Parameters.AddWithValue("@r", item.RefineLevel);
                ins.Parameters.AddWithValue("@roll", System.Text.Json.JsonSerializer.Serialize(item.Roll));
                item.DbId = Convert.ToInt32(ins.ExecuteScalar());
            }

            foreach (var kv in player.Equipment)
            {
                var item = kv.Value;
                Mithara.Server.Entities.ItemRoller.EnsureRolled(item);
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO items (character_id, slot, item_id, quantity, refine_level, roll_data) VALUES (@c, @s, @ii, @q, @r, @roll) RETURNING id";
                ins.Parameters.AddWithValue("@c", characterId);
                ins.Parameters.AddWithValue("@s", item.Slot);
                ins.Parameters.AddWithValue("@ii", item.ItemId);
                ins.Parameters.AddWithValue("@q", item.Quantity);
                ins.Parameters.AddWithValue("@r", item.RefineLevel);
                ins.Parameters.AddWithValue("@roll", System.Text.Json.JsonSerializer.Serialize(item.Roll));
                item.DbId = Convert.ToInt32(ins.ExecuteScalar());
            }

            foreach (var kv in player.Quests)
            {
                var quest = kv.Value;
                string progressJson = System.Text.Json.JsonSerializer.Serialize(quest.Progress);
                using var qcmd = conn.CreateCommand();
                qcmd.Transaction = tx;
                qcmd.CommandText = """
                    INSERT INTO player_quests (character_id, quest_id, progress, completed, claimed)
                    VALUES (@c, @q, @p, @co, @cl)
                    ON CONFLICT (character_id, quest_id) DO UPDATE SET progress = @p, completed = @co, claimed = @cl
                    """;
                qcmd.Parameters.AddWithValue("@c", characterId);
                qcmd.Parameters.AddWithValue("@q", quest.QuestId);
                qcmd.Parameters.AddWithValue("@p", progressJson);
                qcmd.Parameters.AddWithValue("@co", quest.Completed ? 1 : 0);
                qcmd.Parameters.AddWithValue("@cl", quest.Claimed ? 1 : 0);
                qcmd.ExecuteNonQuery();
            }

            if (bankGold.HasValue)
            {
                using var bg = conn.CreateCommand();
                bg.Transaction = tx;
                bg.CommandText = "UPDATE characters SET bank_gold = @b WHERE id = @c";
                bg.Parameters.AddWithValue("@b", bankGold.Value);
                bg.Parameters.AddWithValue("@c", characterId);
                bg.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string HashPassword(string password, string salt = "")
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public int GetCharacterGuildId(string characterName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT guild_id FROM guild_members WHERE name = @n";
        cmd.Parameters.AddWithValue("@n", characterName);
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return -1;
        return Convert.ToInt32(result);
    }

    public (int guildId, ulong entityId, int rank) GetCharacterGuildData(string characterName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT gm.guild_id, gm.entity_id, gm.rank
            FROM guild_members gm
            INNER JOIN guilds g ON g.id = gm.guild_id
            WHERE gm.name = @n
            ORDER BY gm.guild_id DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("@n", characterName);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (-1, 0, 0);
        return (reader.GetInt32(0), (ulong)reader.GetInt64(1), reader.GetInt32(2));
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---- Migration helper methods ----

    public int GetCharacterCount()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM characters";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void InsertAccount(int id, string username, string hash, string secQ, string secA, string salt)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO accounts (id, username, password_hash, security_question, security_answer, salt)
            VALUES (@i, @u, @p, @q, @a, @s)
            ON CONFLICT (id) DO UPDATE SET username=@u, password_hash=@p";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.Parameters.AddWithValue("@q", secQ);
        cmd.Parameters.AddWithValue("@a", secA);
        cmd.Parameters.AddWithValue("@s", salt);
        cmd.ExecuteNonQuery();
    }

    public void InsertCharacter(int id, int accountId, int slotIndex, string name, string className, string race,
        int level, long xp, int forca, int agilidade, int destreza, int inteligencia,
        float posX, float posY, int bankGold, int gold, int statPoints)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO characters (id, account_id, slot_index, name, class, race, level, xp,
                forca, agilidade, destreza, inteligencia, pos_x, pos_y, bank_gold, gold, stat_points)
            VALUES (@i, @a, @s, @n, @c, @r, @l, @x,
                @f, @ag, @d, @in, @px, @py, @bg, @g, @sp)
            ON CONFLICT (id) DO UPDATE SET name=@n
            """;
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@a", accountId);
        cmd.Parameters.AddWithValue("@s", slotIndex);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@c", className);
        cmd.Parameters.AddWithValue("@r", race);
        cmd.Parameters.AddWithValue("@l", level);
        cmd.Parameters.AddWithValue("@x", xp);
        cmd.Parameters.AddWithValue("@f", forca);
        cmd.Parameters.AddWithValue("@ag", agilidade);
        cmd.Parameters.AddWithValue("@d", destreza);
        cmd.Parameters.AddWithValue("@in", inteligencia);
        cmd.Parameters.AddWithValue("@px", posX);
        cmd.Parameters.AddWithValue("@py", posY);
        cmd.Parameters.AddWithValue("@bg", bankGold);
        cmd.Parameters.AddWithValue("@g", gold);
        cmd.Parameters.AddWithValue("@sp", statPoints);
        cmd.ExecuteNonQuery();
    }

    public void InsertItem(int id, int characterId, int slot, int itemId, int quantity)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO items (id, character_id, slot, item_id, quantity) VALUES (@i, @c, @s, @ii, @q) ON CONFLICT (id) DO UPDATE SET quantity=@q";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@s", slot);
        cmd.Parameters.AddWithValue("@ii", itemId);
        cmd.Parameters.AddWithValue("@q", quantity);
        cmd.ExecuteNonQuery();
    }

    public void InsertGuild(int id, string name, int level, int xp, int skillPoints)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO guilds (id, name, level, xp, skill_points) VALUES (@i, @n, @l, @x, @s) ON CONFLICT (id) DO UPDATE SET name=@n";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@l", level);
        cmd.Parameters.AddWithValue("@x", xp);
        cmd.Parameters.AddWithValue("@s", skillPoints);
        cmd.ExecuteNonQuery();
    }

    public void InsertGuildMember(int guildId, ulong entityId, string name, int rank)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO guild_members (guild_id, entity_id, name, rank) VALUES (@g, @e, @n, @r) ON CONFLICT (guild_id, entity_id) DO UPDATE SET name=@n, rank=@r";
        cmd.Parameters.AddWithValue("@g", guildId);
        cmd.Parameters.AddWithValue("@e", (long)entityId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@r", rank);
        cmd.ExecuteNonQuery();
    }

    public void InsertGuildSkill(int guildId, string skillId, int level)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO guild_skills (guild_id, skill_id, level) VALUES (@g, @s, @l) ON CONFLICT (guild_id, skill_id) DO UPDATE SET level=@l";
        cmd.Parameters.AddWithValue("@g", guildId);
        cmd.Parameters.AddWithValue("@s", skillId);
        cmd.Parameters.AddWithValue("@l", level);
        cmd.ExecuteNonQuery();
    }

    public void InsertPlayerQuest(int id, int characterId, int questId, string progress, bool completed, bool claimed)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO player_quests (id, character_id, quest_id, progress, completed, claimed) VALUES (@i, @c, @q, @p, @co, @cl) ON CONFLICT (id) DO UPDATE SET progress=@p";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@q", questId);
        cmd.Parameters.AddWithValue("@p", progress);
        cmd.Parameters.AddWithValue("@co", completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@cl", claimed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void InsertCharacterPet(int id, int characterId, int petId, string petName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO character_pets (id, character_id, pet_id, pet_name) VALUES (@i, @c, @p, @n) ON CONFLICT (id) DO UPDATE SET pet_name=@n";
        cmd.Parameters.AddWithValue("@i", id);
        cmd.Parameters.AddWithValue("@c", characterId);
        cmd.Parameters.AddWithValue("@p", petId);
        cmd.Parameters.AddWithValue("@n", petName);
        cmd.ExecuteNonQuery();
    }

    public List<ItemDefinition> LoadItemDefinitions()
    {
        var result = new List<ItemDefinition>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, type, max_stack, is_stackable, is_bag, extra_slots, forca, agilidade, destreza, inteligencia, base_attack, defense, buy_price, affix_pool, magic_defense, hp, mana, evasion, definition_data FROM item_definitions ORDER BY id";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var affixStr = reader.IsDBNull(14) ? "" : reader.GetString(14);
            var affixPool = string.IsNullOrWhiteSpace(affixStr)
                ? new List<string>()
                : affixStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var definition = new ItemDefinition
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = (ItemType)reader.GetInt32(2),
                MaxStack = reader.GetInt32(3),
                IsStackable = reader.GetInt32(4) != 0,
                IsBag = reader.GetInt32(5) != 0,
                ExtraSlots = reader.GetInt32(6),
                Forca = reader.GetInt32(7),
                Agilidade = reader.GetInt32(8),
                Destreza = reader.GetInt32(9),
                Inteligencia = reader.GetInt32(10),
                BaseAttack = reader.GetInt32(11),
                Defense = reader.GetInt32(12),
                BuyPrice = reader.GetInt32(13),
                AffixPool = affixPool,
                MagicDefense = reader.GetInt32(15),
                Hp = reader.GetInt32(16),
                Mana = reader.GetInt32(17),
                Evasion = Convert.ToSingle(reader.GetValue(18)),
            };
            if (!reader.IsDBNull(19) && !string.IsNullOrWhiteSpace(reader.GetString(19)))
            {
                try
                {
                    var stored = System.Text.Json.JsonSerializer.Deserialize<ItemDefinition>(reader.GetString(19));
                    if (stored != null) definition = stored;
                }
                catch (System.Text.Json.JsonException) { }
            }
            result.Add(definition);
        }
        return result;
    }

    public void SaveItemDefinition(ItemDefinition def)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO item_definitions (id, name, type, max_stack, is_stackable, is_bag, extra_slots, forca, agilidade, destreza, inteligencia, base_attack, defense, buy_price, affix_pool, magic_defense, hp, mana, evasion, definition_data)
            VALUES (@id, @name, @type, @max, @stack, @bag, @extra, @forca, @agi, @dex, @int, @atk, @def, @price, @pool, @magic_def, @hp, @mana, @evasion, @definition_data)
            ON CONFLICT (id) DO UPDATE SET
                name = @name, type = @type, max_stack = @max, is_stackable = @stack, is_bag = @bag,
                extra_slots = @extra, forca = @forca, agilidade = @agi, destreza = @dex, inteligencia = @int,
                base_attack = @atk, defense = @def, buy_price = @price, affix_pool = @pool,
                magic_defense = @magic_def, hp = @hp, mana = @mana, evasion = @evasion,
                definition_data = @definition_data";
        cmd.Parameters.AddWithValue("@id", def.Id);
        cmd.Parameters.AddWithValue("@name", def.Name);
        cmd.Parameters.AddWithValue("@type", (int)def.Type);
        cmd.Parameters.AddWithValue("@max", def.MaxStack);
        cmd.Parameters.AddWithValue("@stack", def.IsStackable ? 1 : 0);
        cmd.Parameters.AddWithValue("@bag", def.IsBag ? 1 : 0);
        cmd.Parameters.AddWithValue("@extra", def.ExtraSlots);
        cmd.Parameters.AddWithValue("@forca", def.Forca);
        cmd.Parameters.AddWithValue("@agi", def.Agilidade);
        cmd.Parameters.AddWithValue("@dex", def.Destreza);
        cmd.Parameters.AddWithValue("@int", def.Inteligencia);
        cmd.Parameters.AddWithValue("@atk", def.BaseAttack);
        cmd.Parameters.AddWithValue("@def", def.Defense);
        cmd.Parameters.AddWithValue("@price", def.BuyPrice);
        cmd.Parameters.AddWithValue("@pool", string.Join(",", def.AffixPool));
        cmd.Parameters.AddWithValue("@magic_def", def.MagicDefense);
        cmd.Parameters.AddWithValue("@hp", def.Hp);
        cmd.Parameters.AddWithValue("@mana", def.Mana);
        cmd.Parameters.AddWithValue("@evasion", def.Evasion);
        cmd.Parameters.AddWithValue("@definition_data", System.Text.Json.JsonSerializer.Serialize(def));
        cmd.ExecuteNonQuery();
    }

    public void SeedItemDefinitions()
    {
        var items = new List<ItemDefinition>
        {
            new() { Id = ItemDefinitions.PocaoVida, Name = "Poção de Vida", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, Hp = 50, BuyPrice = 10 },
            new() { Id = ItemDefinitions.PocaoMana, Name = "Poção de Mana", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, Mana = 30, BuyPrice = 10 },
            new() { Id = 100, Name = "Pergaminho do Pet (7 Tentativas)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 50 },
            new() { Id = 101, Name = "Pergaminho de Ressurreicao", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },
            new() { Id = ItemDefinitions.PergaminhoCriacaoCla, Name = "Pergaminho de Criacao de Cla", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },
            new() { Id = ItemDefinitions.PergaminhoVip7Dias, Name = "Pergaminho VIP (7 Dias)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 0 },
            new() { Id = ItemDefinitions.PergaminhoVip15Dias, Name = "Pergaminho VIP (15 Dias)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 0 },
            new() { Id = ItemDefinitions.PergaminhoVip30Dias, Name = "Pergaminho VIP (30 Dias)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 0 },
            new() { Id = ItemDefinitions.PergaminhoVip7DiasTrial, Name = "Pergaminho VIP Trial (7 Dias)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 0 },
            new() { Id = ItemDefinitions.PergaminhoResetTalentos, Name = "Pergaminho de Reset de Talentos", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 100 },
            new() { Id = ItemDefinitions.PergaminhoDoPet5, Name = "Pergaminho do Pet (5 Tentativas)", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 0 },
            new() { Id = 1000, Name = "Arco da Primeira Caçada", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 4, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11000, Name = "Arco da Primeira Caçada", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 5, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1001, Name = "Arco do Vento Verde", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 5, DestrezaMin = 4, DestrezaMax = 6, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 13, BaseAttackMin = 11, BaseAttackMax = 15, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11001, Name = "Arco do Vento Verde", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 6, DestrezaMin = 5, DestrezaMax = 7, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 15, BaseAttackMin = 13, BaseAttackMax = 18, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1002, Name = "Arco de Valebosque", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 7, DestrezaMin = 6, DestrezaMax = 9, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 22, BaseAttackMin = 20, BaseAttackMax = 25, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11002, Name = "Arco de Valebosque", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 9, DestrezaMin = 7, DestrezaMax = 11, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 27, BaseAttackMin = 24, BaseAttackMax = 30, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1003, Name = "Arco dos Vigias da Névoa", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 11, DestrezaMin = 9, DestrezaMax = 13, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 32, BaseAttackMin = 28, BaseAttackMax = 36, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11003, Name = "Arco dos Vigias da Névoa", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 13, DestrezaMin = 11, DestrezaMax = 15, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 37, BaseAttackMin = 33, BaseAttackMax = 42, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1004, Name = "Arco de Carvalho Negro", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 14, DestrezaMin = 12, DestrezaMax = 16, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 41, BaseAttackMin = 37, BaseAttackMax = 46, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11004, Name = "Arco de Carvalho Negro", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 16, DestrezaMin = 14, DestrezaMax = 19, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 49, BaseAttackMin = 44, BaseAttackMax = 54, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1005, Name = "Arco do Predador Silencioso", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 17, DestrezaMin = 14, DestrezaMax = 20, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 51, BaseAttackMin = 46, BaseAttackMax = 57, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11005, Name = "Arco do Predador Silencioso", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 20, DestrezaMin = 17, DestrezaMax = 24, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 60, BaseAttackMin = 54, BaseAttackMax = 67, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1006, Name = "Arco de Chifre de Warg", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 20, DestrezaMin = 17, DestrezaMax = 24, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 61, BaseAttackMin = 55, BaseAttackMax = 68, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11006, Name = "Arco de Chifre de Warg", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 24, DestrezaMin = 20, DestrezaMax = 28, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 72, BaseAttackMin = 65, BaseAttackMax = 80, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1007, Name = "Arco dos Olhos de Prata", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 23, DestrezaMin = 20, DestrezaMax = 27, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 71, BaseAttackMin = 64, BaseAttackMax = 78, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11007, Name = "Arco dos Olhos de Prata", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 28, DestrezaMin = 24, DestrezaMax = 32, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 84, BaseAttackMin = 76, BaseAttackMax = 92, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1008, Name = "Arco Imperial de Mitthara", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 27, DestrezaMin = 23, DestrezaMax = 31, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 80, BaseAttackMin = 72, BaseAttackMax = 89, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11008, Name = "Arco Imperial de Mitthara", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 32, DestrezaMin = 27, DestrezaMax = 37, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 95, BaseAttackMin = 85, BaseAttackMax = 105, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1009, Name = "Arco Ancestral de Aster", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 29, DestrezaMin = 25, DestrezaMax = 34, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 90, BaseAttackMin = 81, BaseAttackMax = 99, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11009, Name = "Arco Ancestral de Aster", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 35, DestrezaMin = 30, DestrezaMax = 40, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 106, BaseAttackMin = 96, BaseAttackMax = 117, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1010, Name = "Arco do Caçador Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 33, DestrezaMin = 28, DestrezaMax = 38, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 100, BaseAttackMin = 90, BaseAttackMax = 110, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11010, Name = "Arco do Caçador Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 39, DestrezaMin = 33, DestrezaMax = 45, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 118, BaseAttackMin = 106, BaseAttackMax = 130, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1011, Name = "Adaga do Primeiro Corte", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 2, BaseAttackMin = 1, BaseAttackMax = 3, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11011, Name = "Adaga do Primeiro Corte", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 2, BaseAttackMin = 1, BaseAttackMax = 4, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1012, Name = "Adaga do Beco Frio", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 4, DestrezaMin = 3, DestrezaMax = 5, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 8, BaseAttackMin = 6, BaseAttackMax = 10, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11012, Name = "Adaga do Beco Frio", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 5, DestrezaMin = 4, DestrezaMax = 6, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 9, BaseAttackMin = 7, BaseAttackMax = 12, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1013, Name = "Adaga de Ferro Sombrio", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 6, DestrezaMin = 5, DestrezaMax = 8, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 14, BaseAttackMin = 12, BaseAttackMax = 16, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11013, Name = "Adaga de Ferro Sombrio", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 7, DestrezaMin = 6, DestrezaMax = 9, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 16, BaseAttackMin = 14, BaseAttackMax = 19, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1014, Name = "Adaga da Máscara Rachada", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 8, DestrezaMin = 7, DestrezaMax = 10, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 20, BaseAttackMin = 17, BaseAttackMax = 23, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11014, Name = "Adaga da Máscara Rachada", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 10, DestrezaMin = 8, DestrezaMax = 12, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 23, BaseAttackMin = 20, BaseAttackMax = 27, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1015, Name = "Adaga da Lua Baixa", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 11, DestrezaMin = 9, DestrezaMax = 13, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 26, BaseAttackMin = 23, BaseAttackMax = 30, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11015, Name = "Adaga da Lua Baixa", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 13, DestrezaMin = 11, DestrezaMax = 15, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 31, BaseAttackMin = 27, BaseAttackMax = 35, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1016, Name = "Adaga do Executor Cinzento", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 14, DestrezaMin = 12, DestrezaMax = 16, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 32, BaseAttackMin = 28, BaseAttackMax = 36, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11016, Name = "Adaga do Executor Cinzento", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 16, DestrezaMin = 14, DestrezaMax = 19, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 37, BaseAttackMin = 33, BaseAttackMax = 42, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1017, Name = "Adaga do Predador Noturno", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 16, DestrezaMin = 14, DestrezaMax = 19, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 38, BaseAttackMin = 33, BaseAttackMax = 43, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11017, Name = "Adaga do Predador Noturno", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 19, DestrezaMin = 17, DestrezaMax = 22, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 45, BaseAttackMin = 39, BaseAttackMax = 51, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1018, Name = "Adaga de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 19, DestrezaMin = 16, DestrezaMax = 22, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 44, BaseAttackMin = 39, BaseAttackMax = 50, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11018, Name = "Adaga de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 22, DestrezaMin = 19, DestrezaMax = 26, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 52, BaseAttackMin = 46, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1019, Name = "Adaga da Meia-Noite", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 21, DestrezaMin = 18, DestrezaMax = 24, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 50, BaseAttackMin = 44, BaseAttackMax = 57, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11019, Name = "Adaga da Meia-Noite", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 24, DestrezaMin = 21, DestrezaMax = 28, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 59, BaseAttackMin = 52, BaseAttackMax = 67, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1020, Name = "Adaga Ancestral das Sombras", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 23, DestrezaMin = 20, DestrezaMax = 27, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 56, BaseAttackMin = 50, BaseAttackMax = 63, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11020, Name = "Adaga Ancestral das Sombras", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 28, DestrezaMin = 24, DestrezaMax = 32, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 66, BaseAttackMin = 59, BaseAttackMax = 74, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1021, Name = "Adaga do Assassino Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 26, DestrezaMin = 22, DestrezaMax = 30, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 62, BaseAttackMin = 55, BaseAttackMax = 70, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11021, Name = "Adaga do Assassino Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 30, DestrezaMin = 26, DestrezaMax = 35, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 74, BaseAttackMin = 65, BaseAttackMax = 83, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,Precisao,Agilidade,RouboVida,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1022, Name = "Lâmina de Apoio", Type = ItemType.Shield, RequiredLevel = 1, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 1, AgilidadeMin = 1, AgilidadeMax = 1, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 1, BaseAttackMin = 1, BaseAttackMax = 2, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11022, Name = "Lâmina de Apoio", Type = ItemType.Shield, RequiredLevel = 1, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 1, AgilidadeMin = 1, AgilidadeMax = 1, Destreza = 1, DestrezaMin = 1, DestrezaMax = 2, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 1, BaseAttackMin = 1, BaseAttackMax = 2, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1023, Name = "Adaga de Parada", Type = ItemType.Shield, RequiredLevel = 10, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 2, AgilidadeMin = 2, AgilidadeMax = 3, Destreza = 3, DestrezaMin = 3, DestrezaMax = 4, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 5, BaseAttackMin = 4, BaseAttackMax = 7, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11023, Name = "Adaga de Parada", Type = ItemType.Shield, RequiredLevel = 10, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 3, AgilidadeMin = 2, AgilidadeMax = 4, Destreza = 4, DestrezaMin = 4, DestrezaMax = 5, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 6, BaseAttackMin = 5, BaseAttackMax = 8, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1024, Name = "Lâmina do Duelista", Type = ItemType.Shield, RequiredLevel = 20, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 4, AgilidadeMin = 4, AgilidadeMax = 5, Destreza = 5, DestrezaMin = 4, DestrezaMax = 7, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 10, BaseAttackMin = 8, BaseAttackMax = 12, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11024, Name = "Lâmina do Duelista", Type = ItemType.Shield, RequiredLevel = 20, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 5, AgilidadeMin = 5, AgilidadeMax = 6, Destreza = 6, DestrezaMin = 5, DestrezaMax = 8, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 11, BaseAttackMin = 9, BaseAttackMax = 14, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1025, Name = "Adaga do Batedor", Type = ItemType.Shield, RequiredLevel = 30, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 6, AgilidadeMin = 5, AgilidadeMax = 7, Destreza = 7, DestrezaMin = 6, DestrezaMax = 9, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 13, BaseAttackMin = 11, BaseAttackMax = 16, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11025, Name = "Adaga do Batedor", Type = ItemType.Shield, RequiredLevel = 30, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 7, AgilidadeMin = 6, AgilidadeMax = 8, Destreza = 9, DestrezaMin = 7, DestrezaMax = 11, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 16, BaseAttackMin = 13, BaseAttackMax = 19, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1026, Name = "Lâmina da Sombra Curta", Type = ItemType.Shield, RequiredLevel = 40, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 7, AgilidadeMin = 6, AgilidadeMax = 9, Destreza = 9, DestrezaMin = 8, DestrezaMax = 11, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 18, BaseAttackMin = 15, BaseAttackMax = 21, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11026, Name = "Lâmina da Sombra Curta", Type = ItemType.Shield, RequiredLevel = 40, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 9, AgilidadeMin = 7, AgilidadeMax = 11, Destreza = 11, DestrezaMin = 9, DestrezaMax = 13, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 21, BaseAttackMin = 18, BaseAttackMax = 25, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1027, Name = "Adaga Defletora", Type = ItemType.Shield, RequiredLevel = 50, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 10, AgilidadeMin = 8, AgilidadeMax = 12, Destreza = 12, DestrezaMin = 10, DestrezaMax = 14, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 22, BaseAttackMin = 18, BaseAttackMax = 26, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11027, Name = "Adaga Defletora", Type = ItemType.Shield, RequiredLevel = 50, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 11, AgilidadeMin = 9, AgilidadeMax = 14, Destreza = 14, DestrezaMin = 12, DestrezaMax = 17, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 26, BaseAttackMin = 21, BaseAttackMax = 31, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1028, Name = "Lâmina do Predador Silencioso", Type = ItemType.Shield, RequiredLevel = 60, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 11, AgilidadeMin = 9, AgilidadeMax = 14, Destreza = 13, DestrezaMin = 11, DestrezaMax = 16, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 26, BaseAttackMin = 21, BaseAttackMax = 31, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11028, Name = "Lâmina do Predador Silencioso", Type = ItemType.Shield, RequiredLevel = 60, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 14, AgilidadeMin = 11, AgilidadeMax = 17, Destreza = 16, DestrezaMin = 13, DestrezaMax = 19, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 31, BaseAttackMin = 25, BaseAttackMax = 37, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1029, Name = "Adaga de Aço Negro Leve", Type = ItemType.Shield, RequiredLevel = 70, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 13, AgilidadeMin = 10, AgilidadeMax = 16, Destreza = 15, DestrezaMin = 13, DestrezaMax = 18, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 30, BaseAttackMin = 25, BaseAttackMax = 36, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11029, Name = "Adaga de Aço Negro Leve", Type = ItemType.Shield, RequiredLevel = 70, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 15, AgilidadeMin = 12, AgilidadeMax = 19, Destreza = 18, DestrezaMin = 15, DestrezaMax = 21, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 36, BaseAttackMin = 30, BaseAttackMax = 42, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1030, Name = "Lâmina da Meia-Noite", Type = ItemType.Shield, RequiredLevel = 80, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 14, AgilidadeMin = 11, AgilidadeMax = 18, Destreza = 17, DestrezaMin = 15, DestrezaMax = 20, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 34, BaseAttackMin = 28, BaseAttackMax = 40, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11030, Name = "Lâmina da Meia-Noite", Type = ItemType.Shield, RequiredLevel = 80, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 17, AgilidadeMin = 13, AgilidadeMax = 21, Destreza = 21, DestrezaMin = 18, DestrezaMax = 24, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 40, BaseAttackMin = 33, BaseAttackMax = 47, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1031, Name = "Lâmina Ancestral Oculta", Type = ItemType.Shield, RequiredLevel = 90, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 16, AgilidadeMin = 13, AgilidadeMax = 20, Destreza = 19, DestrezaMin = 16, DestrezaMax = 23, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 38, BaseAttackMin = 32, BaseAttackMax = 45, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11031, Name = "Lâmina Ancestral Oculta", Type = ItemType.Shield, RequiredLevel = 90, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 19, AgilidadeMin = 15, AgilidadeMax = 24, Destreza = 23, DestrezaMin = 19, DestrezaMax = 27, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 45, BaseAttackMin = 38, BaseAttackMax = 53, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1032, Name = "Lâmina do Lendário Assassino", Type = ItemType.Shield, RequiredLevel = 100, IsElite = false, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 18, AgilidadeMin = 14, AgilidadeMax = 22, Destreza = 21, DestrezaMin = 18, DestrezaMax = 25, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 42, BaseAttackMin = 35, BaseAttackMax = 50, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11032, Name = "Lâmina do Lendário Assassino", Type = ItemType.Shield, RequiredLevel = 100, IsElite = true, AllowedClasses = "Ladino", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 21, AgilidadeMin = 17, AgilidadeMax = 26, Destreza = 25, DestrezaMin = 21, DestrezaMax = 30, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 50, BaseAttackMin = 41, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,VelocidadeAtaque,Precisao,Agilidade,RouboVida,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1033, Name = "Machado Quebra-Tronco", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker", Forca = 1, ForcaMin = 1, ForcaMax = 2, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 4, BaseAttackMin = 3, BaseAttackMax = 5, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11033, Name = "Machado Quebra-Tronco", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker", Forca = 1, ForcaMin = 1, ForcaMax = 2, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 5, BaseAttackMin = 4, BaseAttackMax = 6, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1034, Name = "Machado do Sangue Quente", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker", Forca = 5, ForcaMin = 4, ForcaMax = 6, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 15, BaseAttackMin = 13, BaseAttackMax = 17, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11034, Name = "Machado do Sangue Quente", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker", Forca = 6, ForcaMin = 5, ForcaMax = 7, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 17, BaseAttackMin = 15, BaseAttackMax = 20, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1035, Name = "Machado de Ferro Brutal", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker", Forca = 8, ForcaMin = 7, ForcaMax = 10, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 25, BaseAttackMin = 22, BaseAttackMax = 29, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11035, Name = "Machado de Ferro Brutal", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker", Forca = 10, ForcaMin = 8, ForcaMax = 12, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 30, BaseAttackMin = 26, BaseAttackMax = 34, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1036, Name = "Machado do Clamor", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker", Forca = 11, ForcaMin = 10, ForcaMax = 13, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 36, BaseAttackMin = 32, BaseAttackMax = 41, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11036, Name = "Machado do Clamor", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker", Forca = 13, ForcaMin = 12, ForcaMax = 15, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 43, BaseAttackMin = 38, BaseAttackMax = 48, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1037, Name = "Machado do Conquistador", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker", Forca = 15, ForcaMin = 13, ForcaMax = 17, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 47, BaseAttackMin = 42, BaseAttackMax = 53, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11037, Name = "Machado do Conquistador", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker", Forca = 17, ForcaMin = 15, ForcaMax = 20, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 56, BaseAttackMin = 50, BaseAttackMax = 63, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1038, Name = "Machado Presa-de-Fera", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker", Forca = 18, ForcaMin = 16, ForcaMax = 21, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 58, BaseAttackMin = 52, BaseAttackMax = 65, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11038, Name = "Machado Presa-de-Fera", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker", Forca = 22, ForcaMin = 19, ForcaMax = 25, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 69, BaseAttackMin = 61, BaseAttackMax = 77, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1039, Name = "Machado de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker", Forca = 21, ForcaMin = 18, ForcaMax = 25, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 69, BaseAttackMin = 61, BaseAttackMax = 77, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11039, Name = "Machado de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker", Forca = 25, ForcaMin = 21, ForcaMax = 30, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 81, BaseAttackMin = 72, BaseAttackMax = 91, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1040, Name = "Machado do Campeão Rubro", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker", Forca = 25, ForcaMin = 21, ForcaMax = 29, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 80, BaseAttackMin = 71, BaseAttackMax = 89, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11040, Name = "Machado do Campeão Rubro", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker", Forca = 29, ForcaMin = 25, ForcaMax = 34, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 94, BaseAttackMin = 84, BaseAttackMax = 105, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1041, Name = "Machado Imperial de Guerra", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker", Forca = 28, ForcaMin = 24, ForcaMax = 32, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 91, BaseAttackMin = 81, BaseAttackMax = 101, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11041, Name = "Machado Imperial de Guerra", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker", Forca = 33, ForcaMin = 28, ForcaMax = 38, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 107, BaseAttackMin = 96, BaseAttackMax = 119, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1042, Name = "Machado Ancestral dos Colossos", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker", Forca = 31, ForcaMin = 27, ForcaMax = 36, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 101, BaseAttackMin = 90, BaseAttackMax = 113, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11042, Name = "Machado Ancestral dos Colossos", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker", Forca = 37, ForcaMin = 32, ForcaMax = 42, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 119, BaseAttackMin = 106, BaseAttackMax = 133, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1043, Name = "Machado do Berserker Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker", Forca = 35, ForcaMin = 30, ForcaMax = 40, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 112, BaseAttackMin = 100, BaseAttackMax = 125, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11043, Name = "Machado do Berserker Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker", Forca = 41, ForcaMin = 35, ForcaMax = 47, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 133, BaseAttackMin = 118, BaseAttackMax = 148, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,DanoCriticoBonus,PenetracaoArmadura,RouboVida,Tenacidade,Precisao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1044, Name = "Espada do Juramento", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Guardiao", Forca = 1, ForcaMin = 1, ForcaMax = 2, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 4, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11044, Name = "Espada do Juramento", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Guardiao", Forca = 1, ForcaMin = 1, ForcaMax = 2, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 5, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1045, Name = "Espada de Ferro da Guarda", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Guardiao", Forca = 4, ForcaMin = 3, ForcaMax = 5, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 11, BaseAttackMin = 9, BaseAttackMax = 13, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11045, Name = "Espada de Ferro da Guarda", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Guardiao", Forca = 5, ForcaMin = 4, ForcaMax = 6, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 13, BaseAttackMin = 11, BaseAttackMax = 15, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1046, Name = "Espada do Sentinela", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Guardiao", Forca = 7, ForcaMin = 6, ForcaMax = 9, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 19, BaseAttackMin = 17, BaseAttackMax = 22, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11046, Name = "Espada do Sentinela", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Guardiao", Forca = 9, ForcaMin = 7, ForcaMax = 11, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 23, BaseAttackMin = 20, BaseAttackMax = 26, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1047, Name = "Espada da Muralha", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Guardiao", Forca = 10, ForcaMin = 8, ForcaMax = 12, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 27, BaseAttackMin = 24, BaseAttackMax = 31, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11047, Name = "Espada da Muralha", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Guardiao", Forca = 11, ForcaMin = 9, ForcaMax = 14, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 32, BaseAttackMin = 28, BaseAttackMax = 37, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1048, Name = "Espada do Protetor", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Guardiao", Forca = 13, ForcaMin = 11, ForcaMax = 15, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 35, BaseAttackMin = 31, BaseAttackMax = 40, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11048, Name = "Espada do Protetor", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Guardiao", Forca = 15, ForcaMin = 13, ForcaMax = 18, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 42, BaseAttackMin = 37, BaseAttackMax = 47, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1049, Name = "Espada do Defensor Cinzento", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Guardiao", Forca = 15, ForcaMin = 13, ForcaMax = 18, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 44, BaseAttackMin = 38, BaseAttackMax = 50, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11049, Name = "Espada do Defensor Cinzento", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Guardiao", Forca = 18, ForcaMin = 15, ForcaMax = 21, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 52, BaseAttackMin = 45, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1050, Name = "Espada de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Guardiao", Forca = 18, ForcaMin = 15, ForcaMax = 22, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 52, BaseAttackMin = 46, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11050, Name = "Espada de Aço Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Guardiao", Forca = 22, ForcaMin = 18, ForcaMax = 26, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 62, BaseAttackMin = 54, BaseAttackMax = 70, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1051, Name = "Espada do Campeão da Guarda", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Guardiao", Forca = 21, ForcaMin = 18, ForcaMax = 25, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 60, BaseAttackMin = 53, BaseAttackMax = 68, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11051, Name = "Espada do Campeão da Guarda", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Guardiao", Forca = 25, ForcaMin = 21, ForcaMax = 30, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 71, BaseAttackMin = 63, BaseAttackMax = 80, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1052, Name = "Espada Imperial de Mitthara", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Guardiao", Forca = 24, ForcaMin = 20, ForcaMax = 28, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 68, BaseAttackMin = 60, BaseAttackMax = 77, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11052, Name = "Espada Imperial de Mitthara", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Guardiao", Forca = 28, ForcaMin = 24, ForcaMax = 33, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 81, BaseAttackMin = 71, BaseAttackMax = 91, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1053, Name = "Espada Ancestral do Bastião", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Guardiao", Forca = 27, ForcaMin = 23, ForcaMax = 32, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 77, BaseAttackMin = 68, BaseAttackMax = 86, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11053, Name = "Espada Ancestral do Bastião", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Guardiao", Forca = 32, ForcaMin = 27, ForcaMax = 38, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 90, BaseAttackMin = 80, BaseAttackMax = 101, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1054, Name = "Espada do Guardião Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Guardiao", Forca = 30, ForcaMin = 25, ForcaMax = 35, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 85, BaseAttackMin = 75, BaseAttackMax = 95, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11054, Name = "Espada do Guardião Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Guardiao", Forca = 35, ForcaMin = 30, ForcaMax = 41, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 100, BaseAttackMin = 88, BaseAttackMax = 112, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,Tenacidade,PenetracaoArmadura,Precisao,Forca,RouboVida".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1055, Name = "Escudo de Tábua Ferrada", Type = ItemType.Shield, RequiredLevel = 1, IsElite = false, AllowedClasses = "Guardiao", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 1, AgilidadeMin = 1, AgilidadeMax = 1, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 5, HpMax = 10, BuyPrice = 10, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11055, Name = "Escudo de Tábua Ferrada", Type = ItemType.Shield, RequiredLevel = 1, IsElite = true, AllowedClasses = "Guardiao", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 1, AgilidadeMin = 1, AgilidadeMax = 1, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 6, HpMax = 12, BuyPrice = 10, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1056, Name = "Escudo da Guarda Jovem", Type = ItemType.Shield, RequiredLevel = 10, IsElite = false, AllowedClasses = "Guardiao", Forca = 2, ForcaMin = 2, ForcaMax = 3, Agilidade = 2, AgilidadeMin = 2, AgilidadeMax = 3, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 16, HpMax = 27, BuyPrice = 100, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11056, Name = "Escudo da Guarda Jovem", Type = ItemType.Shield, RequiredLevel = 10, IsElite = true, AllowedClasses = "Guardiao", Forca = 3, ForcaMin = 2, ForcaMax = 4, Agilidade = 3, AgilidadeMin = 2, AgilidadeMax = 4, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 10, DefenseMin = 8, DefenseMax = 12, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 19, HpMax = 32, BuyPrice = 100, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1057, Name = "Escudo do Sentinela", Type = ItemType.Shield, RequiredLevel = 20, IsElite = false, AllowedClasses = "Guardiao", Forca = 4, ForcaMin = 4, ForcaMax = 5, Agilidade = 3, AgilidadeMin = 3, AgilidadeMax = 4, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 28, HpMax = 44, BuyPrice = 200, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11057, Name = "Escudo do Sentinela", Type = ItemType.Shield, RequiredLevel = 20, IsElite = true, AllowedClasses = "Guardiao", Forca = 5, ForcaMin = 5, ForcaMax = 6, Agilidade = 4, AgilidadeMin = 4, AgilidadeMax = 5, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 18, DefenseMin = 15, DefenseMax = 21, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 33, HpMax = 52, BuyPrice = 200, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1058, Name = "Escudo da Muralha Baixa", Type = ItemType.Shield, RequiredLevel = 30, IsElite = false, AllowedClasses = "Guardiao", Forca = 6, ForcaMin = 5, ForcaMax = 7, Agilidade = 5, AgilidadeMin = 4, AgilidadeMax = 6, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 40, HpMax = 61, BuyPrice = 300, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11058, Name = "Escudo da Muralha Baixa", Type = ItemType.Shield, RequiredLevel = 30, IsElite = true, AllowedClasses = "Guardiao", Forca = 7, ForcaMin = 6, ForcaMax = 8, Agilidade = 6, AgilidadeMin = 5, AgilidadeMax = 7, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 47, HpMax = 72, BuyPrice = 300, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1059, Name = "Escudo do Protetor", Type = ItemType.Shield, RequiredLevel = 40, IsElite = false, AllowedClasses = "Guardiao", Forca = 8, ForcaMin = 7, ForcaMax = 9, Agilidade = 6, AgilidadeMin = 5, AgilidadeMax = 8, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 29, DefenseMin = 25, DefenseMax = 33, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 51, HpMax = 78, BuyPrice = 400, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11059, Name = "Escudo do Protetor", Type = ItemType.Shield, RequiredLevel = 40, IsElite = true, AllowedClasses = "Guardiao", Forca = 9, ForcaMin = 8, ForcaMax = 11, Agilidade = 7, AgilidadeMin = 6, AgilidadeMax = 9, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 34, DefenseMin = 30, DefenseMax = 39, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 60, HpMax = 92, BuyPrice = 400, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1060, Name = "Escudo do Defensor Cinzento", Type = ItemType.Shield, RequiredLevel = 50, IsElite = false, AllowedClasses = "Guardiao", Forca = 10, ForcaMin = 8, ForcaMax = 12, Agilidade = 8, AgilidadeMin = 6, AgilidadeMax = 10, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 35, DefenseMin = 30, DefenseMax = 41, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 62, HpMax = 95, BuyPrice = 500, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11060, Name = "Escudo do Defensor Cinzento", Type = ItemType.Shield, RequiredLevel = 50, IsElite = true, AllowedClasses = "Guardiao", Forca = 11, ForcaMin = 9, ForcaMax = 14, Agilidade = 9, AgilidadeMin = 7, AgilidadeMax = 12, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 41, DefenseMin = 35, DefenseMax = 48, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 73, HpMax = 112, BuyPrice = 500, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1061, Name = "Escudo de Aço Negro", Type = ItemType.Shield, RequiredLevel = 60, IsElite = false, AllowedClasses = "Guardiao", Forca = 11, ForcaMin = 9, ForcaMax = 14, Agilidade = 9, AgilidadeMin = 8, AgilidadeMax = 11, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 42, DefenseMin = 36, DefenseMax = 49, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 74, HpMax = 112, BuyPrice = 600, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11061, Name = "Escudo de Aço Negro", Type = ItemType.Shield, RequiredLevel = 60, IsElite = true, AllowedClasses = "Guardiao", Forca = 14, ForcaMin = 11, ForcaMax = 17, Agilidade = 11, AgilidadeMin = 9, AgilidadeMax = 13, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 50, DefenseMin = 42, DefenseMax = 58, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 87, HpMax = 132, BuyPrice = 600, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1062, Name = "Escudo do Campeão da Guarda", Type = ItemType.Shield, RequiredLevel = 70, IsElite = false, AllowedClasses = "Guardiao", Forca = 13, ForcaMin = 11, ForcaMax = 16, Agilidade = 11, AgilidadeMin = 9, AgilidadeMax = 13, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 49, DefenseMin = 42, DefenseMax = 57, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 86, HpMax = 129, BuyPrice = 700, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11062, Name = "Escudo do Campeão da Guarda", Type = ItemType.Shield, RequiredLevel = 70, IsElite = true, AllowedClasses = "Guardiao", Forca = 16, ForcaMin = 13, ForcaMax = 19, Agilidade = 13, AgilidadeMin = 11, AgilidadeMax = 15, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 58, DefenseMin = 50, DefenseMax = 67, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 101, HpMax = 152, BuyPrice = 700, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1063, Name = "Escudo Imperial de Mitthara", Type = ItemType.Shield, RequiredLevel = 80, IsElite = false, AllowedClasses = "Guardiao", Forca = 15, ForcaMin = 12, ForcaMax = 18, Agilidade = 12, AgilidadeMin = 10, AgilidadeMax = 15, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 56, DefenseMin = 48, DefenseMax = 64, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 97, HpMax = 146, BuyPrice = 800, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11063, Name = "Escudo Imperial de Mitthara", Type = ItemType.Shield, RequiredLevel = 80, IsElite = true, AllowedClasses = "Guardiao", Forca = 17, ForcaMin = 14, ForcaMax = 21, Agilidade = 15, AgilidadeMin = 12, AgilidadeMax = 18, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 66, DefenseMin = 57, DefenseMax = 76, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 114, HpMax = 172, BuyPrice = 800, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1064, Name = "Escudo Ancestral do Bastião", Type = ItemType.Shield, RequiredLevel = 90, IsElite = false, AllowedClasses = "Guardiao", Forca = 17, ForcaMin = 14, ForcaMax = 20, Agilidade = 13, AgilidadeMin = 11, AgilidadeMax = 16, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 63, DefenseMin = 54, DefenseMax = 72, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 108, HpMax = 163, BuyPrice = 900, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11064, Name = "Escudo Ancestral do Bastião", Type = ItemType.Shield, RequiredLevel = 90, IsElite = true, AllowedClasses = "Guardiao", Forca = 20, ForcaMin = 17, ForcaMax = 24, Agilidade = 16, AgilidadeMin = 13, AgilidadeMax = 19, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 74, DefenseMin = 64, DefenseMax = 85, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 127, HpMax = 192, BuyPrice = 900, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1065, Name = "Escudo do Guardião Lendário", Type = ItemType.Shield, RequiredLevel = 100, IsElite = false, AllowedClasses = "Guardiao", Forca = 18, ForcaMin = 15, ForcaMax = 22, Agilidade = 15, AgilidadeMin = 12, AgilidadeMax = 18, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 70, DefenseMin = 60, DefenseMax = 80, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 120, HpMax = 180, BuyPrice = 1000, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11065, Name = "Escudo do Guardião Lendário", Type = ItemType.Shield, RequiredLevel = 100, IsElite = true, AllowedClasses = "Guardiao", Forca = 22, ForcaMin = 18, ForcaMax = 26, Agilidade = 17, AgilidadeMin = 14, AgilidadeMax = 21, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 0, InteligenciaMin = 0, InteligenciaMax = 0, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 82, DefenseMin = 71, DefenseMax = 94, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 142, HpMax = 212, BuyPrice = 1000, AffixPool = new List<string>("HP,DefesaFisica,Tenacidade,RegeneracaoVida,Forca,Agilidade".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1066, Name = "Cajado da Faísca Fraca", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 2, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 4, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11066, Name = "Cajado da Faísca Fraca", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 2, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 5, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1067, Name = "Cajado do Aprendiz Arcano", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 5, InteligenciaMin = 4, InteligenciaMax = 6, BaseAttack = 13, BaseAttackMin = 11, BaseAttackMax = 15, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11067, Name = "Cajado do Aprendiz Arcano", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 6, InteligenciaMin = 5, InteligenciaMax = 7, BaseAttack = 15, BaseAttackMin = 13, BaseAttackMax = 18, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1068, Name = "Cajado de Carvalho Rúnico", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 8, InteligenciaMin = 7, InteligenciaMax = 10, BaseAttack = 22, BaseAttackMin = 20, BaseAttackMax = 25, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11068, Name = "Cajado de Carvalho Rúnico", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 10, InteligenciaMin = 8, InteligenciaMax = 12, BaseAttack = 27, BaseAttackMin = 24, BaseAttackMax = 30, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1069, Name = "Cajado da Chama Azul", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 11, InteligenciaMin = 10, InteligenciaMax = 13, BaseAttack = 32, BaseAttackMin = 28, BaseAttackMax = 36, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11069, Name = "Cajado da Chama Azul", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 13, InteligenciaMin = 12, InteligenciaMax = 15, BaseAttack = 37, BaseAttackMin = 33, BaseAttackMax = 42, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1070, Name = "Cajado do Erudito Errante", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 15, InteligenciaMin = 13, InteligenciaMax = 17, BaseAttack = 41, BaseAttackMin = 37, BaseAttackMax = 46, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11070, Name = "Cajado do Erudito Errante", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 17, InteligenciaMin = 15, InteligenciaMax = 20, BaseAttack = 49, BaseAttackMin = 44, BaseAttackMax = 54, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1071, Name = "Cajado do Invocador", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 18, InteligenciaMin = 16, InteligenciaMax = 21, BaseAttack = 51, BaseAttackMin = 46, BaseAttackMax = 57, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11071, Name = "Cajado do Invocador", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 22, InteligenciaMin = 19, InteligenciaMax = 25, BaseAttack = 60, BaseAttackMin = 54, BaseAttackMax = 67, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1072, Name = "Cajado de Cristal Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 21, InteligenciaMin = 18, InteligenciaMax = 25, BaseAttack = 61, BaseAttackMin = 55, BaseAttackMax = 68, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11072, Name = "Cajado de Cristal Negro", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 25, InteligenciaMin = 21, InteligenciaMax = 30, BaseAttack = 72, BaseAttackMin = 65, BaseAttackMax = 80, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1073, Name = "Cajado do Mestre Arcano", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 25, InteligenciaMin = 21, InteligenciaMax = 29, BaseAttack = 71, BaseAttackMin = 64, BaseAttackMax = 78, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11073, Name = "Cajado do Mestre Arcano", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 29, InteligenciaMin = 25, InteligenciaMax = 34, BaseAttack = 84, BaseAttackMin = 76, BaseAttackMax = 92, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1074, Name = "Cajado Imperial das Runas", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 28, InteligenciaMin = 24, InteligenciaMax = 32, BaseAttack = 80, BaseAttackMin = 72, BaseAttackMax = 89, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11074, Name = "Cajado Imperial das Runas", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 33, InteligenciaMin = 28, InteligenciaMax = 38, BaseAttack = 95, BaseAttackMin = 85, BaseAttackMax = 105, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1075, Name = "Cajado Ancestral de Aster", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 31, InteligenciaMin = 27, InteligenciaMax = 36, BaseAttack = 90, BaseAttackMin = 81, BaseAttackMax = 99, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11075, Name = "Cajado Ancestral de Aster", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 37, InteligenciaMin = 32, InteligenciaMax = 42, BaseAttack = 106, BaseAttackMin = 96, BaseAttackMax = 117, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1076, Name = "Cajado do Arquimago Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 35, InteligenciaMin = 30, InteligenciaMax = 40, BaseAttack = 100, BaseAttackMin = 90, BaseAttackMax = 110, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11076, Name = "Cajado do Arquimago Lendário", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago", Forca = 0, ForcaMin = 0, ForcaMax = 0, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 41, InteligenciaMin = 35, InteligenciaMax = 47, BaseAttack = 118, BaseAttackMin = 106, BaseAttackMax = 130, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("DanoMagico,ChanceCritica,DanoCriticoBonus,Precisao,RouboMana,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1077, Name = "Martelo do Primeiro Voto", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = false, AllowedClasses = "Prist", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 1, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 4, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11077, Name = "Martelo do Primeiro Voto", Type = ItemType.Weapon, RequiredLevel = 1, IsElite = true, AllowedClasses = "Prist", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 1, BaseAttack = 3, BaseAttackMin = 2, BaseAttackMax = 5, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 10, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1078, Name = "Martelo do Noviço", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = false, AllowedClasses = "Prist", Forca = 3, ForcaMin = 3, ForcaMax = 4, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 3, InteligenciaMin = 3, InteligenciaMax = 4, BaseAttack = 11, BaseAttackMin = 9, BaseAttackMax = 13, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11078, Name = "Martelo do Noviço", Type = ItemType.Weapon, RequiredLevel = 10, IsElite = true, AllowedClasses = "Prist", Forca = 4, ForcaMin = 4, ForcaMax = 5, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 4, InteligenciaMin = 4, InteligenciaMax = 5, BaseAttack = 13, BaseAttackMin = 11, BaseAttackMax = 15, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 100, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1079, Name = "Martelo Consagrado", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = false, AllowedClasses = "Prist", Forca = 6, ForcaMin = 5, ForcaMax = 7, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 6, InteligenciaMin = 5, InteligenciaMax = 7, BaseAttack = 19, BaseAttackMin = 17, BaseAttackMax = 22, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11079, Name = "Martelo Consagrado", Type = ItemType.Weapon, RequiredLevel = 20, IsElite = true, AllowedClasses = "Prist", Forca = 7, ForcaMin = 6, ForcaMax = 8, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 7, InteligenciaMin = 6, InteligenciaMax = 8, BaseAttack = 23, BaseAttackMin = 20, BaseAttackMax = 26, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 200, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1080, Name = "Martelo da Capela Velha", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = false, AllowedClasses = "Prist", Forca = 8, ForcaMin = 7, ForcaMax = 10, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 8, InteligenciaMin = 7, InteligenciaMax = 10, BaseAttack = 27, BaseAttackMin = 24, BaseAttackMax = 31, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11080, Name = "Martelo da Capela Velha", Type = ItemType.Weapon, RequiredLevel = 30, IsElite = true, AllowedClasses = "Prist", Forca = 10, ForcaMin = 8, ForcaMax = 12, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 10, InteligenciaMin = 8, InteligenciaMax = 12, BaseAttack = 32, BaseAttackMin = 28, BaseAttackMax = 37, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 300, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1081, Name = "Martelo da Luz Serena", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = false, AllowedClasses = "Prist", Forca = 11, ForcaMin = 9, ForcaMax = 13, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 11, InteligenciaMin = 9, InteligenciaMax = 13, BaseAttack = 35, BaseAttackMin = 31, BaseAttackMax = 40, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11081, Name = "Martelo da Luz Serena", Type = ItemType.Weapon, RequiredLevel = 40, IsElite = true, AllowedClasses = "Prist", Forca = 13, ForcaMin = 11, ForcaMax = 15, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 13, InteligenciaMin = 11, InteligenciaMax = 15, BaseAttack = 42, BaseAttackMin = 37, BaseAttackMax = 47, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 400, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1082, Name = "Martelo do Sacerdote Guerreiro", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = false, AllowedClasses = "Prist", Forca = 14, ForcaMin = 12, ForcaMax = 16, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 14, InteligenciaMin = 12, InteligenciaMax = 16, BaseAttack = 44, BaseAttackMin = 38, BaseAttackMax = 50, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11082, Name = "Martelo do Sacerdote Guerreiro", Type = ItemType.Weapon, RequiredLevel = 50, IsElite = true, AllowedClasses = "Prist", Forca = 16, ForcaMin = 14, ForcaMax = 19, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 16, InteligenciaMin = 14, InteligenciaMax = 19, BaseAttack = 52, BaseAttackMin = 45, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 500, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1083, Name = "Martelo de Prata Sagrada", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = false, AllowedClasses = "Prist", Forca = 16, ForcaMin = 14, ForcaMax = 18, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 16, InteligenciaMin = 14, InteligenciaMax = 18, BaseAttack = 52, BaseAttackMin = 46, BaseAttackMax = 59, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11083, Name = "Martelo de Prata Sagrada", Type = ItemType.Weapon, RequiredLevel = 60, IsElite = true, AllowedClasses = "Prist", Forca = 19, ForcaMin = 17, ForcaMax = 21, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 19, InteligenciaMin = 17, InteligenciaMax = 21, BaseAttack = 62, BaseAttackMin = 54, BaseAttackMax = 70, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 600, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1084, Name = "Martelo do Arcebispo", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = false, AllowedClasses = "Prist", Forca = 18, ForcaMin = 16, ForcaMax = 21, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 18, InteligenciaMin = 16, InteligenciaMax = 21, BaseAttack = 60, BaseAttackMin = 53, BaseAttackMax = 68, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11084, Name = "Martelo do Arcebispo", Type = ItemType.Weapon, RequiredLevel = 70, IsElite = true, AllowedClasses = "Prist", Forca = 22, ForcaMin = 19, ForcaMax = 25, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 22, InteligenciaMin = 19, InteligenciaMax = 25, BaseAttack = 71, BaseAttackMin = 63, BaseAttackMax = 80, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 700, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1085, Name = "Martelo Imperial da Fé", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = false, AllowedClasses = "Prist", Forca = 21, ForcaMin = 18, ForcaMax = 24, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 21, InteligenciaMin = 18, InteligenciaMax = 24, BaseAttack = 68, BaseAttackMin = 60, BaseAttackMax = 77, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11085, Name = "Martelo Imperial da Fé", Type = ItemType.Weapon, RequiredLevel = 80, IsElite = true, AllowedClasses = "Prist", Forca = 24, ForcaMin = 21, ForcaMax = 28, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 24, InteligenciaMin = 21, InteligenciaMax = 28, BaseAttack = 81, BaseAttackMin = 71, BaseAttackMax = 91, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 800, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1086, Name = "Martelo Ancestral dos Templos", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = false, AllowedClasses = "Prist", Forca = 23, ForcaMin = 20, ForcaMax = 27, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 23, InteligenciaMin = 20, InteligenciaMax = 27, BaseAttack = 77, BaseAttackMin = 68, BaseAttackMax = 86, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11086, Name = "Martelo Ancestral dos Templos", Type = ItemType.Weapon, RequiredLevel = 90, IsElite = true, AllowedClasses = "Prist", Forca = 28, ForcaMin = 24, ForcaMax = 32, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 28, InteligenciaMin = 24, InteligenciaMax = 32, BaseAttack = 90, BaseAttackMin = 80, BaseAttackMax = 101, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 900, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1087, Name = "Martelo do Sumo Sacerdote", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = false, AllowedClasses = "Prist", Forca = 26, ForcaMin = 22, ForcaMax = 30, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 26, InteligenciaMin = 22, InteligenciaMax = 30, BaseAttack = 85, BaseAttackMin = 75, BaseAttackMax = 95, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11087, Name = "Martelo do Sumo Sacerdote", Type = ItemType.Weapon, RequiredLevel = 100, IsElite = true, AllowedClasses = "Prist", Forca = 30, ForcaMin = 26, ForcaMax = 35, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 30, InteligenciaMin = 26, InteligenciaMax = 35, BaseAttack = 100, BaseAttackMin = 88, BaseAttackMax = 112, Defense = 0, DefenseMin = 0, DefenseMax = 0, MagicDefenseMin = 0, MagicDefenseMax = 0, HpMin = 0, HpMax = 0, BuyPrice = 1000, AffixPool = new List<string>("ChanceCritica,Precisao,Tenacidade,RegeneracaoVida,RegeneracaoMana,RouboVida,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1088, Name = "Escudo da Primeira Prece", Type = ItemType.Shield, RequiredLevel = 1, IsElite = false, AllowedClasses = "Prist", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 1, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, HpMin = 5, HpMax = 10, BuyPrice = 10, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11088, Name = "Escudo da Primeira Prece", Type = ItemType.Shield, RequiredLevel = 1, IsElite = true, AllowedClasses = "Prist", Forca = 1, ForcaMin = 1, ForcaMax = 1, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 1, InteligenciaMin = 1, InteligenciaMax = 1, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefenseMin = 2, MagicDefenseMax = 4, HpMin = 6, HpMax = 12, BuyPrice = 10, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1089, Name = "Escudo do Noviço", Type = ItemType.Shield, RequiredLevel = 10, IsElite = false, AllowedClasses = "Prist", Forca = 2, ForcaMin = 2, ForcaMax = 3, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 2, InteligenciaMin = 2, InteligenciaMax = 3, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefenseMin = 7, MagicDefenseMax = 10, HpMin = 14, HpMax = 24, BuyPrice = 100, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11089, Name = "Escudo do Noviço", Type = ItemType.Shield, RequiredLevel = 10, IsElite = true, AllowedClasses = "Prist", Forca = 3, ForcaMin = 2, ForcaMax = 4, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 3, InteligenciaMin = 2, InteligenciaMax = 4, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 9, DefenseMin = 7, DefenseMax = 11, MagicDefenseMin = 8, MagicDefenseMax = 12, HpMin = 17, HpMax = 28, BuyPrice = 100, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1090, Name = "Escudo Consagrado", Type = ItemType.Shield, RequiredLevel = 20, IsElite = false, AllowedClasses = "Prist", Forca = 4, ForcaMin = 4, ForcaMax = 5, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 4, InteligenciaMin = 4, InteligenciaMax = 5, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 13, DefenseMin = 11, DefenseMax = 16, MagicDefenseMin = 13, MagicDefenseMax = 17, HpMin = 24, HpMax = 38, BuyPrice = 200, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11090, Name = "Escudo Consagrado", Type = ItemType.Shield, RequiredLevel = 20, IsElite = true, AllowedClasses = "Prist", Forca = 5, ForcaMin = 5, ForcaMax = 6, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 5, InteligenciaMin = 5, InteligenciaMax = 6, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 16, DefenseMin = 13, DefenseMax = 19, MagicDefenseMin = 15, MagicDefenseMax = 20, HpMin = 28, HpMax = 45, BuyPrice = 200, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1091, Name = "Escudo da Capela Velha", Type = ItemType.Shield, RequiredLevel = 30, IsElite = false, AllowedClasses = "Prist", Forca = 6, ForcaMin = 5, ForcaMax = 7, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 6, InteligenciaMin = 5, InteligenciaMax = 7, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefenseMin = 18, MagicDefenseMax = 25, HpMin = 34, HpMax = 52, BuyPrice = 300, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11091, Name = "Escudo da Capela Velha", Type = ItemType.Shield, RequiredLevel = 30, IsElite = true, AllowedClasses = "Prist", Forca = 7, ForcaMin = 6, ForcaMax = 8, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 7, InteligenciaMin = 6, InteligenciaMax = 8, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefenseMin = 21, MagicDefenseMax = 30, HpMin = 40, HpMax = 61, BuyPrice = 300, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1092, Name = "Escudo da Luz Serena", Type = ItemType.Shield, RequiredLevel = 40, IsElite = false, AllowedClasses = "Prist", Forca = 8, ForcaMin = 7, ForcaMax = 9, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 8, InteligenciaMin = 7, InteligenciaMax = 9, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 25, DefenseMin = 21, DefenseMax = 29, MagicDefenseMin = 23, MagicDefenseMax = 32, HpMin = 43, HpMax = 66, BuyPrice = 400, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11092, Name = "Escudo da Luz Serena", Type = ItemType.Shield, RequiredLevel = 40, IsElite = true, AllowedClasses = "Prist", Forca = 9, ForcaMin = 8, ForcaMax = 11, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 9, InteligenciaMin = 8, InteligenciaMax = 11, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 29, DefenseMin = 25, DefenseMax = 34, MagicDefenseMin = 27, MagicDefenseMax = 38, HpMin = 51, HpMax = 78, BuyPrice = 400, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1093, Name = "Escudo do Sacerdote Guardião", Type = ItemType.Shield, RequiredLevel = 50, IsElite = false, AllowedClasses = "Prist", Forca = 10, ForcaMin = 8, ForcaMax = 12, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 10, InteligenciaMin = 8, InteligenciaMax = 12, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 31, DefenseMin = 26, DefenseMax = 36, MagicDefenseMin = 28, MagicDefenseMax = 39, HpMin = 52, HpMax = 80, BuyPrice = 500, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11093, Name = "Escudo do Sacerdote Guardião", Type = ItemType.Shield, RequiredLevel = 50, IsElite = true, AllowedClasses = "Prist", Forca = 11, ForcaMin = 9, ForcaMax = 14, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 11, InteligenciaMin = 9, InteligenciaMax = 14, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 36, DefenseMin = 31, DefenseMax = 42, MagicDefenseMin = 33, MagicDefenseMax = 46, HpMin = 61, HpMax = 94, BuyPrice = 500, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1094, Name = "Escudo de Prata Sagrada", Type = ItemType.Shield, RequiredLevel = 60, IsElite = false, AllowedClasses = "Prist", Forca = 11, ForcaMin = 9, ForcaMax = 14, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 11, InteligenciaMin = 9, InteligenciaMax = 14, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 36, DefenseMin = 30, DefenseMax = 43, MagicDefenseMin = 34, MagicDefenseMax = 46, HpMin = 62, HpMax = 94, BuyPrice = 600, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11094, Name = "Escudo de Prata Sagrada", Type = ItemType.Shield, RequiredLevel = 60, IsElite = true, AllowedClasses = "Prist", Forca = 14, ForcaMin = 11, ForcaMax = 17, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 14, InteligenciaMin = 11, InteligenciaMax = 17, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 43, DefenseMin = 35, DefenseMax = 51, MagicDefenseMin = 40, MagicDefenseMax = 54, HpMin = 73, HpMax = 111, BuyPrice = 600, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1095, Name = "Escudo do Arcebispo", Type = ItemType.Shield, RequiredLevel = 70, IsElite = false, AllowedClasses = "Prist", Forca = 13, ForcaMin = 11, ForcaMax = 16, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 13, InteligenciaMin = 11, InteligenciaMax = 16, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 42, DefenseMin = 35, DefenseMax = 50, MagicDefenseMin = 39, MagicDefenseMax = 53, HpMin = 72, HpMax = 108, BuyPrice = 700, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11095, Name = "Escudo do Arcebispo", Type = ItemType.Shield, RequiredLevel = 70, IsElite = true, AllowedClasses = "Prist", Forca = 16, ForcaMin = 13, ForcaMax = 19, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 16, InteligenciaMin = 13, InteligenciaMax = 19, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 50, DefenseMin = 41, DefenseMax = 59, MagicDefenseMin = 46, MagicDefenseMax = 63, HpMin = 85, HpMax = 127, BuyPrice = 700, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1096, Name = "Escudo Imperial da Fé", Type = ItemType.Shield, RequiredLevel = 80, IsElite = false, AllowedClasses = "Prist", Forca = 15, ForcaMin = 12, ForcaMax = 18, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 15, InteligenciaMin = 12, InteligenciaMax = 18, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 48, DefenseMin = 40, DefenseMax = 56, MagicDefenseMin = 44, MagicDefenseMax = 61, HpMin = 81, HpMax = 122, BuyPrice = 800, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11096, Name = "Escudo Imperial da Fé", Type = ItemType.Shield, RequiredLevel = 80, IsElite = true, AllowedClasses = "Prist", Forca = 17, ForcaMin = 14, ForcaMax = 21, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 17, InteligenciaMin = 14, InteligenciaMax = 21, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 56, DefenseMin = 47, DefenseMax = 66, MagicDefenseMin = 52, MagicDefenseMax = 72, HpMin = 96, HpMax = 144, BuyPrice = 800, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1097, Name = "Escudo Ancestral dos Templos", Type = ItemType.Shield, RequiredLevel = 90, IsElite = false, AllowedClasses = "Prist", Forca = 17, ForcaMin = 14, ForcaMax = 20, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 17, InteligenciaMin = 14, InteligenciaMax = 20, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 54, DefenseMin = 45, DefenseMax = 63, MagicDefenseMin = 50, MagicDefenseMax = 68, HpMin = 90, HpMax = 136, BuyPrice = 900, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11097, Name = "Escudo Ancestral dos Templos", Type = ItemType.Shield, RequiredLevel = 90, IsElite = true, AllowedClasses = "Prist", Forca = 20, ForcaMin = 17, ForcaMax = 24, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 20, InteligenciaMin = 17, InteligenciaMax = 24, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 63, DefenseMin = 53, DefenseMax = 74, MagicDefenseMin = 59, MagicDefenseMax = 80, HpMin = 106, HpMax = 160, BuyPrice = 900, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 1098, Name = "Escudo do Sumo Sacerdote", Type = ItemType.Shield, RequiredLevel = 100, IsElite = false, AllowedClasses = "Prist", Forca = 18, ForcaMin = 15, ForcaMax = 22, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 18, InteligenciaMin = 15, InteligenciaMax = 22, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 60, DefenseMin = 50, DefenseMax = 70, MagicDefenseMin = 55, MagicDefenseMax = 75, HpMin = 100, HpMax = 150, BuyPrice = 1000, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 11098, Name = "Escudo do Sumo Sacerdote", Type = ItemType.Shield, RequiredLevel = 100, IsElite = true, AllowedClasses = "Prist", Forca = 22, ForcaMin = 18, ForcaMax = 26, Agilidade = 0, AgilidadeMin = 0, AgilidadeMax = 0, Destreza = 0, DestrezaMin = 0, DestrezaMax = 0, Inteligencia = 22, InteligenciaMin = 18, InteligenciaMax = 26, BaseAttack = 0, BaseAttackMin = 0, BaseAttackMax = 0, Defense = 71, DefenseMin = 59, DefenseMax = 83, MagicDefenseMin = 65, MagicDefenseMax = 88, HpMin = 118, HpMax = 177, BuyPrice = 1000, AffixPool = new List<string>("HP,Mana,DefesaFisica,DefesaMagica,Tenacidade,RegeneracaoVida,RegeneracaoMana,ReducaoCooldown".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
        };

        // ARMOR_CATALOG_START
        var armorItems = new List<ItemDefinition>
        {
            new() { Id = 300000, Name = "Véu do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300001, Name = "Véu do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 1, HpMin = 1, HpMax = 2, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300002, Name = "Manto do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 3, HpMin = 3, HpMax = 4, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300003, Name = "Manto do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 4, MagicDefenseMin = 4, MagicDefenseMax = 5, Hp = 4, HpMin = 4, HpMax = 5, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300004, Name = "Calças do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 2, HpMin = 2, HpMax = 3, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300005, Name = "Calças do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 2, HpMin = 2, HpMax = 3, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300006, Name = "Luvas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 0, DefenseMin = 0, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300007, Name = "Luvas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300008, Name = "Passos do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 0, DefenseMin = 0, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300009, Name = "Passos do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300010, Name = "Faixa do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 0, DefenseMin = 0, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300011, Name = "Faixa do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 1, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300012, Name = "Véu de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 3, Hp = 3, HpMin = 3, HpMax = 3, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300013, Name = "Véu de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 3, HpMin = 3, HpMax = 4, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300014, Name = "Manto de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 8, HpMin = 7, HpMax = 10, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300015, Name = "Manto de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 9, MagicDefenseMax = 13, Hp = 11, HpMin = 9, HpMax = 13, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300016, Name = "Calças de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 6, HpMin = 5, HpMax = 7, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300017, Name = "Calças de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 7, HpMin = 6, HpMax = 8, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300018, Name = "Luvas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 2, HpMin = 2, HpMax = 3, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300019, Name = "Luvas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 3, HpMin = 3, HpMax = 4, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300020, Name = "Passos de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 2, HpMin = 2, HpMax = 3, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300021, Name = "Passos de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 3, HpMin = 3, HpMax = 4, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300022, Name = "Faixa de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 2, HpMin = 2, HpMax = 3, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300023, Name = "Faixa de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 3, HpMin = 3, HpMax = 4, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300024, Name = "Véu de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 5, HpMin = 4, HpMax = 6, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300025, Name = "Véu de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 3, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 6, HpMin = 5, HpMax = 7, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300026, Name = "Manto de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 14, HpMin = 12, HpMax = 16, Mana = 19, ManaMin = 16, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300027, Name = "Manto de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 17, HpMin = 15, HpMax = 20, Mana = 24, ManaMin = 20, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300028, Name = "Calças de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 9, HpMin = 8, HpMax = 11, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300029, Name = "Calças de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 11, HpMin = 10, HpMax = 13, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300030, Name = "Luvas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 4, HpMin = 3, HpMax = 5, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300031, Name = "Luvas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 5, HpMin = 4, HpMax = 6, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300032, Name = "Passos de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 4, HpMin = 3, HpMax = 5, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300033, Name = "Passos de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 5, HpMin = 4, HpMax = 6, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300034, Name = "Faixa de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 4, HpMin = 3, HpMax = 5, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300035, Name = "Faixa de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 5, HpMin = 4, HpMax = 6, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300036, Name = "Véu da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 7, HpMin = 6, HpMax = 8, Mana = 9, ManaMin = 8, ManaMax = 11, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300037, Name = "Véu da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 9, HpMin = 8, HpMax = 10, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300038, Name = "Manto da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 21, HpMin = 18, HpMax = 24, Mana = 28, ManaMin = 24, ManaMax = 32, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300039, Name = "Manto da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 12, DefenseMin = 10, DefenseMax = 14, MagicDefense = 24, MagicDefenseMin = 20, MagicDefenseMax = 28, Hp = 26, HpMin = 22, HpMax = 30, Mana = 35, ManaMin = 30, ManaMax = 40, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300040, Name = "Calças da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 14, HpMin = 12, HpMax = 16, Mana = 18, ManaMin = 16, ManaMax = 21, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300041, Name = "Calças da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 17, HpMin = 15, HpMax = 20, Mana = 23, ManaMin = 20, ManaMax = 26, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300042, Name = "Luvas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 6, HpMin = 5, HpMax = 7, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300043, Name = "Luvas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 7, HpMin = 6, HpMax = 9, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300044, Name = "Passos da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 6, HpMin = 5, HpMax = 7, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300045, Name = "Passos da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 7, HpMin = 6, HpMax = 9, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300046, Name = "Faixa da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 6, HpMin = 5, HpMax = 7, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300047, Name = "Faixa da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 7, HpMin = 6, HpMax = 9, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300048, Name = "Véu de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 10, Hp = 9, HpMin = 8, HpMax = 11, Mana = 12, ManaMin = 11, ManaMax = 14, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300049, Name = "Véu de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 12, HpMin = 10, HpMax = 14, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300050, Name = "Manto de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 26, MagicDefenseMin = 22, MagicDefenseMax = 30, Hp = 28, HpMin = 24, HpMax = 32, Mana = 36, ManaMin = 31, ManaMax = 42, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300051, Name = "Manto de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 33, MagicDefenseMin = 28, MagicDefenseMax = 38, Hp = 35, HpMin = 30, HpMax = 40, Mana = 46, ManaMin = 39, ManaMax = 53, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300052, Name = "Calças de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 18, HpMin = 16, HpMax = 21, Mana = 24, ManaMin = 21, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300053, Name = "Calças de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 25, Hp = 23, HpMin = 20, HpMax = 26, Mana = 30, ManaMin = 26, ManaMax = 35, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300054, Name = "Luvas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 8, HpMin = 7, HpMax = 9, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300055, Name = "Luvas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300056, Name = "Passos de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 8, HpMin = 7, HpMax = 9, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300057, Name = "Passos de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300058, Name = "Faixa de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 8, HpMin = 7, HpMax = 9, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300059, Name = "Faixa de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300060, Name = "Véu da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 12, HpMin = 10, HpMax = 14, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300061, Name = "Véu da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 15, HpMin = 13, HpMax = 17, Mana = 19, ManaMin = 17, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300062, Name = "Manto da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 33, MagicDefenseMin = 28, MagicDefenseMax = 38, Hp = 35, HpMin = 30, HpMax = 40, Mana = 45, ManaMin = 39, ManaMax = 52, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300063, Name = "Manto da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 20, DefenseMin = 17, DefenseMax = 23, MagicDefense = 41, MagicDefenseMin = 35, MagicDefenseMax = 48, Hp = 43, HpMin = 37, HpMax = 50, Mana = 56, ManaMin = 48, ManaMax = 65, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300064, Name = "Calças da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 22, MagicDefenseMin = 19, MagicDefenseMax = 25, Hp = 23, HpMin = 20, HpMax = 26, Mana = 29, ManaMin = 25, ManaMax = 34, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300065, Name = "Calças da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 27, MagicDefenseMin = 23, MagicDefenseMax = 31, Hp = 28, HpMin = 24, HpMax = 33, Mana = 37, ManaMin = 32, ManaMax = 43, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300066, Name = "Luvas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300067, Name = "Luvas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 12, HpMin = 11, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 19, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300068, Name = "Passos da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300069, Name = "Passos da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 12, HpMin = 11, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 19, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300070, Name = "Faixa da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 10, HpMin = 8, HpMax = 12, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300071, Name = "Faixa da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 12, HpMin = 11, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 19, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300072, Name = "Véu de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 14, HpMin = 12, HpMax = 17, Mana = 19, ManaMin = 16, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300073, Name = "Véu de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 18, HpMin = 15, HpMax = 21, Mana = 24, ManaMin = 20, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300074, Name = "Manto de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 40, MagicDefenseMin = 34, MagicDefenseMax = 46, Hp = 42, HpMin = 36, HpMax = 48, Mana = 56, ManaMin = 48, ManaMax = 64, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300075, Name = "Manto de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 24, DefenseMin = 20, DefenseMax = 28, MagicDefense = 50, MagicDefenseMin = 43, MagicDefenseMax = 58, Hp = 52, HpMin = 45, HpMax = 60, Mana = 70, ManaMin = 60, ManaMax = 80, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300076, Name = "Calças de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 26, MagicDefenseMin = 22, MagicDefenseMax = 30, Hp = 27, HpMin = 23, HpMax = 32, Mana = 36, ManaMin = 31, ManaMax = 42, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300077, Name = "Calças de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 33, MagicDefenseMin = 28, MagicDefenseMax = 38, Hp = 34, HpMin = 29, HpMax = 40, Mana = 46, ManaMin = 39, ManaMax = 53, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300078, Name = "Luvas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 12, HpMin = 10, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300079, Name = "Luvas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 17, Mana = 20, ManaMin = 17, ManaMax = 23, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300080, Name = "Passos de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 12, HpMin = 10, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300081, Name = "Passos de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 17, Mana = 20, ManaMin = 17, ManaMax = 23, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300082, Name = "Faixa de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 5, DefenseMin = 5, DefenseMax = 6, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 12, HpMin = 10, HpMax = 14, Mana = 16, ManaMin = 14, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300083, Name = "Faixa de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 17, Mana = 20, ManaMin = 17, ManaMax = 23, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300084, Name = "Véu do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 16, HpMin = 14, HpMax = 19, Mana = 22, ManaMin = 19, ManaMax = 26, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300085, Name = "Véu do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 21, HpMin = 18, HpMax = 24, Mana = 28, ManaMin = 24, ManaMax = 33, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300086, Name = "Manto do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefense = 47, MagicDefenseMin = 40, MagicDefenseMax = 54, Hp = 49, HpMin = 42, HpMax = 56, Mana = 66, ManaMin = 57, ManaMax = 76, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300087, Name = "Manto do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 28, DefenseMin = 24, DefenseMax = 33, MagicDefense = 59, MagicDefenseMin = 50, MagicDefenseMax = 68, Hp = 61, HpMin = 52, HpMax = 70, Mana = 83, ManaMin = 71, ManaMax = 96, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300088, Name = "Calças do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 31, MagicDefenseMin = 26, MagicDefenseMax = 36, Hp = 32, HpMin = 27, HpMax = 37, Mana = 43, ManaMin = 37, ManaMax = 50, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300089, Name = "Calças do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 18, DefenseMin = 16, DefenseMax = 21, MagicDefense = 39, MagicDefenseMin = 33, MagicDefenseMax = 45, Hp = 40, HpMin = 34, HpMax = 46, Mana = 54, ManaMin = 46, ManaMax = 63, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300090, Name = "Luvas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 6, DefenseMin = 6, DefenseMax = 7, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 16, Hp = 14, HpMin = 12, HpMax = 16, Mana = 19, ManaMin = 16, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300091, Name = "Luvas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 17, HpMin = 15, HpMax = 20, Mana = 23, ManaMin = 20, ManaMax = 27, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300092, Name = "Passos do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 6, DefenseMin = 6, DefenseMax = 7, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 16, Hp = 14, HpMin = 12, HpMax = 16, Mana = 19, ManaMin = 16, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300093, Name = "Passos do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 17, HpMin = 15, HpMax = 20, Mana = 23, ManaMin = 20, ManaMax = 27, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300094, Name = "Faixa do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 6, DefenseMin = 6, DefenseMax = 7, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 16, Hp = 14, HpMin = 12, HpMax = 16, Mana = 19, ManaMin = 16, ManaMax = 22, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300095, Name = "Faixa do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 17, HpMin = 15, HpMax = 20, Mana = 23, ManaMin = 20, ManaMax = 27, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300096, Name = "Véu de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 18, MagicDefenseMin = 15, MagicDefenseMax = 21, Hp = 18, HpMin = 16, HpMax = 21, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300097, Name = "Véu de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 22, MagicDefenseMin = 19, MagicDefenseMax = 26, Hp = 23, HpMin = 20, HpMax = 27, Mana = 32, ManaMin = 27, ManaMax = 37, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300098, Name = "Manto de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 25, DefenseMin = 21, DefenseMax = 29, MagicDefense = 52, MagicDefenseMin = 45, MagicDefenseMax = 60, Hp = 54, HpMin = 46, HpMax = 62, Mana = 75, ManaMin = 64, ManaMax = 87, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300099, Name = "Manto de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 31, DefenseMin = 27, DefenseMax = 36, MagicDefense = 65, MagicDefenseMin = 56, MagicDefenseMax = 75, Hp = 68, HpMin = 58, HpMax = 78, Mana = 94, ManaMin = 80, ManaMax = 108, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300100, Name = "Calças de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 16, DefenseMin = 14, DefenseMax = 19, MagicDefense = 34, MagicDefenseMin = 29, MagicDefenseMax = 40, Hp = 35, HpMin = 30, HpMax = 41, Mana = 49, ManaMin = 42, ManaMax = 57, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300101, Name = "Calças de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 21, DefenseMin = 18, DefenseMax = 24, MagicDefense = 43, MagicDefenseMin = 37, MagicDefenseMax = 50, Hp = 44, HpMin = 38, HpMax = 51, Mana = 62, ManaMin = 53, ManaMax = 71, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300102, Name = "Luvas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 18, Mana = 21, ManaMin = 18, ManaMax = 25, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300103, Name = "Luvas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 10, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 19, HpMin = 16, HpMax = 22, Mana = 27, ManaMin = 23, ManaMax = 31, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300104, Name = "Passos de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 18, Mana = 21, ManaMin = 18, ManaMax = 25, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300105, Name = "Passos de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 10, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 19, HpMin = 16, HpMax = 22, Mana = 27, ManaMin = 23, ManaMax = 31, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300106, Name = "Faixa de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 17, Hp = 15, HpMin = 13, HpMax = 18, Mana = 21, ManaMin = 18, ManaMax = 25, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300107, Name = "Faixa de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 10, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 19, HpMin = 16, HpMax = 22, Mana = 27, ManaMin = 23, ManaMax = 31, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300108, Name = "Véu do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 20, HpMin = 17, HpMax = 23, Mana = 28, ManaMin = 24, ManaMax = 33, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300109, Name = "Véu do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 24, MagicDefenseMin = 20, MagicDefenseMax = 28, Hp = 25, HpMin = 22, HpMax = 29, Mana = 36, ManaMin = 31, ManaMax = 41, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300110, Name = "Manto do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 27, DefenseMin = 23, DefenseMax = 31, MagicDefense = 56, MagicDefenseMin = 48, MagicDefenseMax = 64, Hp = 59, HpMin = 51, HpMax = 68, Mana = 84, ManaMin = 71, ManaMax = 97, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300111, Name = "Manto do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 34, DefenseMin = 29, DefenseMax = 39, MagicDefense = 70, MagicDefenseMin = 60, MagicDefenseMax = 80, Hp = 74, HpMin = 63, HpMax = 86, Mana = 105, ManaMin = 89, ManaMax = 121, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300112, Name = "Calças do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 18, DefenseMin = 15, DefenseMax = 21, MagicDefense = 36, MagicDefenseMin = 31, MagicDefenseMax = 42, Hp = 39, HpMin = 33, HpMax = 45, Mana = 55, ManaMin = 47, ManaMax = 63, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300113, Name = "Calças do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefense = 46, MagicDefenseMin = 39, MagicDefenseMax = 53, Hp = 49, HpMin = 42, HpMax = 56, Mana = 69, ManaMin = 59, ManaMax = 79, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300114, Name = "Luvas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 18, Hp = 17, HpMin = 14, HpMax = 20, Mana = 24, ManaMin = 20, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300115, Name = "Luvas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 21, HpMin = 18, HpMax = 24, Mana = 30, ManaMin = 26, ManaMax = 34, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300116, Name = "Passos do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 18, Hp = 17, HpMin = 14, HpMax = 20, Mana = 24, ManaMin = 20, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300117, Name = "Passos do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 21, HpMin = 18, HpMax = 24, Mana = 30, ManaMin = 26, ManaMax = 34, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300118, Name = "Faixa do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 18, Hp = 17, HpMin = 14, HpMax = 20, Mana = 24, ManaMin = 20, ManaMax = 28, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300119, Name = "Faixa do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 21, HpMin = 18, HpMax = 24, Mana = 30, ManaMin = 26, ManaMax = 34, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300120, Name = "Véu da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 21, HpMin = 18, HpMax = 25, Mana = 31, ManaMin = 27, ManaMax = 36, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300121, Name = "Véu da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 25, MagicDefenseMin = 22, MagicDefenseMax = 29, Hp = 27, HpMin = 23, HpMax = 31, Mana = 39, ManaMin = 33, ManaMax = 45, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300122, Name = "Manto da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 29, DefenseMin = 25, DefenseMax = 34, MagicDefense = 59, MagicDefenseMin = 51, MagicDefenseMax = 68, Hp = 63, HpMin = 54, HpMax = 72, Mana = 91, ManaMin = 77, ManaMax = 105, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300123, Name = "Manto da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 37, DefenseMin = 32, DefenseMax = 43, MagicDefense = 74, MagicDefenseMin = 63, MagicDefenseMax = 86, Hp = 79, HpMin = 67, HpMax = 91, Mana = 114, ManaMin = 97, ManaMax = 131, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300124, Name = "Calças da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 19, DefenseMin = 17, DefenseMax = 22, MagicDefense = 39, MagicDefenseMin = 33, MagicDefenseMax = 45, Hp = 41, HpMin = 35, HpMax = 48, Mana = 60, ManaMin = 51, ManaMax = 69, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300125, Name = "Calças da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 24, DefenseMin = 21, DefenseMax = 28, MagicDefense = 49, MagicDefenseMin = 42, MagicDefenseMax = 56, Hp = 52, HpMin = 44, HpMax = 60, Mana = 75, ManaMin = 64, ManaMax = 86, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300126, Name = "Luvas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 17, MagicDefenseMin = 14, MagicDefenseMax = 20, Hp = 18, HpMin = 15, HpMax = 21, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300127, Name = "Luvas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 22, HpMin = 19, HpMax = 26, Mana = 32, ManaMin = 28, ManaMax = 37, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300128, Name = "Passos da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 17, MagicDefenseMin = 14, MagicDefenseMax = 20, Hp = 18, HpMin = 15, HpMax = 21, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300129, Name = "Passos da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 22, HpMin = 19, HpMax = 26, Mana = 32, ManaMin = 28, ManaMax = 37, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300130, Name = "Faixa da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = false, AllowedClasses = "Mago,Prist", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 17, MagicDefenseMin = 14, MagicDefenseMax = 20, Hp = 18, HpMin = 15, HpMax = 21, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300131, Name = "Faixa da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = true, AllowedClasses = "Mago,Prist", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 22, HpMin = 19, HpMax = 26, Mana = 32, ManaMin = 28, ManaMax = 37, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Inteligencia,Mana,DefesaMagica,RegeneracaoMana,ReducaoCooldown,Tenacidade,RouboMana".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300132, Name = "Capuz do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.06f, EvasionMin = 0.05f, EvasionMax = 0.07f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300133, Name = "Capuz do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0.075f, EvasionMin = 0.06f, EvasionMax = 0.09f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300134, Name = "Gibão do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 5, HpMin = 4, HpMax = 6, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0.175f, EvasionMin = 0.15f, EvasionMax = 0.2f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300135, Name = "Gibão do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 7, HpMin = 6, HpMax = 8, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.22f, EvasionMin = 0.19f, EvasionMax = 0.25f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300136, Name = "Calças do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 3, HpMin = 3, HpMax = 4, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.115f, EvasionMin = 0.1f, EvasionMax = 0.13f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300137, Name = "Calças do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 4, HpMin = 4, HpMax = 5, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.14f, EvasionMin = 0.12f, EvasionMax = 0.16f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300138, Name = "Luvas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.05f, EvasionMin = 0.04f, EvasionMax = 0.06f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300139, Name = "Luvas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.06f, EvasionMin = 0.05f, EvasionMax = 0.07f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300140, Name = "Botas do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.05f, EvasionMin = 0.04f, EvasionMax = 0.06f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300141, Name = "Botas do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.06f, EvasionMin = 0.05f, EvasionMax = 0.07f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300142, Name = "Cinto do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 1, HpMin = 1, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.05f, EvasionMin = 0.04f, EvasionMax = 0.06f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300143, Name = "Cinto do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0.06f, EvasionMin = 0.05f, EvasionMax = 0.07f, BuyPrice = 10, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300144, Name = "Capuz de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 4, HpMin = 4, HpMax = 5, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.12f, EvasionMin = 0.1f, EvasionMax = 0.14f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300145, Name = "Capuz de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 5, HpMin = 4, HpMax = 6, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0.15f, EvasionMin = 0.12f, EvasionMax = 0.18f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300146, Name = "Gibão de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 12, HpMin = 10, HpMax = 14, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0.35f, EvasionMin = 0.3f, EvasionMax = 0.4f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300147, Name = "Gibão de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 15, HpMin = 13, HpMax = 18, Mana = 8, ManaMin = 7, ManaMax = 10, Evasion = 0.44f, EvasionMin = 0.38f, EvasionMax = 0.5f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300148, Name = "Calças de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 4, MagicDefenseMin = 4, MagicDefenseMax = 5, Hp = 8, HpMin = 7, HpMax = 9, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.23f, EvasionMin = 0.2f, EvasionMax = 0.26f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300149, Name = "Calças de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 10, HpMin = 9, HpMax = 12, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0.29f, EvasionMin = 0.25f, EvasionMax = 0.33f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300150, Name = "Luvas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 3, HpMin = 3, HpMax = 4, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0.1f, EvasionMin = 0.09f, EvasionMax = 0.11f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300151, Name = "Luvas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 4, HpMin = 4, HpMax = 5, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.125f, EvasionMin = 0.11f, EvasionMax = 0.14f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300152, Name = "Botas de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 3, HpMin = 3, HpMax = 4, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0.1f, EvasionMin = 0.09f, EvasionMax = 0.11f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300153, Name = "Botas de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 4, HpMin = 4, HpMax = 5, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.125f, EvasionMin = 0.11f, EvasionMax = 0.14f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300154, Name = "Cinto de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 3, HpMin = 3, HpMax = 4, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0.1f, EvasionMin = 0.09f, EvasionMax = 0.11f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300155, Name = "Cinto de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 4, HpMin = 4, HpMax = 5, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0.125f, EvasionMin = 0.11f, EvasionMax = 0.14f, BuyPrice = 100, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300156, Name = "Capuz de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 7, HpMin = 6, HpMax = 8, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.18f, EvasionMin = 0.15f, EvasionMax = 0.21f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300157, Name = "Capuz de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 8, HpMin = 7, HpMax = 9, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0.225f, EvasionMin = 0.19f, EvasionMax = 0.26f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300158, Name = "Gibão de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 16, HpMax = 22, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.525f, EvasionMin = 0.45f, EvasionMax = 0.6f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300159, Name = "Gibão de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 16, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 24, HpMin = 20, HpMax = 28, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0.655f, EvasionMin = 0.56f, EvasionMax = 0.75f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300160, Name = "Calças de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.345f, EvasionMin = 0.29f, EvasionMax = 0.4f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300161, Name = "Calças de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0.43f, EvasionMin = 0.36f, EvasionMax = 0.5f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300162, Name = "Luvas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 5, HpMin = 5, HpMax = 6, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0.15f, EvasionMin = 0.13f, EvasionMax = 0.17f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300163, Name = "Luvas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 4, DefenseMin = 3, DefenseMax = 5, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 7, HpMin = 6, HpMax = 8, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.185f, EvasionMin = 0.16f, EvasionMax = 0.21f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300164, Name = "Botas de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 5, HpMin = 5, HpMax = 6, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0.15f, EvasionMin = 0.13f, EvasionMax = 0.17f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300165, Name = "Botas de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 4, DefenseMin = 3, DefenseMax = 5, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 7, HpMin = 6, HpMax = 8, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.185f, EvasionMin = 0.16f, EvasionMax = 0.21f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300166, Name = "Cinto de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 5, HpMin = 5, HpMax = 6, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0.15f, EvasionMin = 0.13f, EvasionMax = 0.17f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300167, Name = "Cinto de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 4, DefenseMin = 3, DefenseMax = 5, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 7, HpMin = 6, HpMax = 8, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0.185f, EvasionMin = 0.16f, EvasionMax = 0.21f, BuyPrice = 200, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300168, Name = "Capuz da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 9, HpMin = 8, HpMax = 11, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0.24f, EvasionMin = 0.2f, EvasionMax = 0.28f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300169, Name = "Capuz da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 12, HpMin = 10, HpMax = 14, Mana = 7, ManaMin = 6, ManaMax = 9, Evasion = 0.3f, EvasionMin = 0.25f, EvasionMax = 0.35f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300170, Name = "Gibão da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 16, DefenseMin = 14, DefenseMax = 19, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 28, HpMin = 24, HpMax = 32, Mana = 17, ManaMin = 15, ManaMax = 20, Evasion = 0.695f, EvasionMin = 0.59f, EvasionMax = 0.8f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300171, Name = "Gibão da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 21, DefenseMin = 18, DefenseMax = 24, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 35, HpMin = 30, HpMax = 40, Mana = 22, ManaMin = 19, ManaMax = 25, Evasion = 0.87f, EvasionMin = 0.74f, EvasionMax = 1f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300172, Name = "Calças da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 9, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 9, MagicDefenseMax = 13, Hp = 18, HpMin = 16, HpMax = 21, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.46f, EvasionMin = 0.39f, EvasionMax = 0.53f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300173, Name = "Calças da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 16, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 23, HpMin = 20, HpMax = 26, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0.575f, EvasionMin = 0.49f, EvasionMax = 0.66f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300174, Name = "Luvas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 8, HpMin = 7, HpMax = 9, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0.2f, EvasionMin = 0.17f, EvasionMax = 0.23f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300175, Name = "Luvas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 8, HpMax = 12, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300176, Name = "Botas da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 8, HpMin = 7, HpMax = 9, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0.2f, EvasionMin = 0.17f, EvasionMax = 0.23f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300177, Name = "Botas da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 8, HpMax = 12, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300178, Name = "Cinto da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 8, HpMin = 7, HpMax = 9, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0.2f, EvasionMin = 0.17f, EvasionMax = 0.23f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300179, Name = "Cinto da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 8, HpMax = 12, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 300, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300180, Name = "Capuz de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 12, HpMin = 11, HpMax = 14, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.3f, EvasionMin = 0.26f, EvasionMax = 0.34f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300181, Name = "Capuz de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 9, ManaMin = 8, ManaMax = 11, Evasion = 0.38f, EvasionMin = 0.33f, EvasionMax = 0.43f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300182, Name = "Gibão de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 21, DefenseMin = 18, DefenseMax = 25, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 25, Hp = 36, HpMin = 31, HpMax = 42, Mana = 22, ManaMin = 19, ManaMax = 26, Evasion = 0.875f, EvasionMin = 0.74f, EvasionMax = 1.01f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300183, Name = "Gibão de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 27, DefenseMin = 23, DefenseMax = 31, MagicDefense = 27, MagicDefenseMin = 23, MagicDefenseMax = 31, Hp = 46, HpMin = 39, HpMax = 53, Mana = 28, ManaMin = 24, ManaMax = 33, Evasion = 1.095f, EvasionMin = 0.93f, EvasionMax = 1.26f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300184, Name = "Calças de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 16, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 24, HpMin = 21, HpMax = 28, Mana = 15, ManaMin = 13, ManaMax = 17, Evasion = 0.575f, EvasionMin = 0.49f, EvasionMax = 0.66f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300185, Name = "Calças de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 30, HpMin = 26, HpMax = 35, Mana = 18, ManaMin = 16, ManaMax = 21, Evasion = 0.72f, EvasionMin = 0.61f, EvasionMax = 0.83f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300186, Name = "Luvas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 9, HpMax = 12, Mana = 6, ManaMin = 6, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300187, Name = "Luvas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.31f, EvasionMin = 0.26f, EvasionMax = 0.36f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300188, Name = "Botas de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 9, HpMax = 12, Mana = 6, ManaMin = 6, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300189, Name = "Botas de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.31f, EvasionMin = 0.26f, EvasionMax = 0.36f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300190, Name = "Cinto de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 10, HpMin = 9, HpMax = 12, Mana = 6, ManaMin = 6, ManaMax = 7, Evasion = 0.25f, EvasionMin = 0.21f, EvasionMax = 0.29f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300191, Name = "Cinto de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.31f, EvasionMin = 0.26f, EvasionMax = 0.36f, BuyPrice = 400, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300192, Name = "Capuz da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 9, ManaMin = 8, ManaMax = 11, Evasion = 0.36f, EvasionMin = 0.31f, EvasionMax = 0.41f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300193, Name = "Capuz da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 17, HpMax = 22, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.45f, EvasionMin = 0.39f, EvasionMax = 0.51f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300194, Name = "Gibão da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 27, DefenseMin = 23, DefenseMax = 31, MagicDefense = 27, MagicDefenseMin = 23, MagicDefenseMax = 31, Hp = 45, HpMin = 39, HpMax = 52, Mana = 28, ManaMin = 24, ManaMax = 32, Evasion = 1.05f, EvasionMin = 0.89f, EvasionMax = 1.21f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300195, Name = "Gibão da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 34, DefenseMin = 29, DefenseMax = 39, MagicDefense = 34, MagicDefenseMin = 29, MagicDefenseMax = 39, Hp = 56, HpMin = 48, HpMax = 65, Mana = 35, ManaMin = 30, ManaMax = 40, Evasion = 1.31f, EvasionMin = 1.11f, EvasionMax = 1.51f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300196, Name = "Calças da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 18, DefenseMin = 15, DefenseMax = 21, MagicDefense = 18, MagicDefenseMin = 15, MagicDefenseMax = 21, Hp = 29, HpMin = 25, HpMax = 34, Mana = 18, ManaMin = 16, ManaMax = 21, Evasion = 0.69f, EvasionMin = 0.59f, EvasionMax = 0.79f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300197, Name = "Calças da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefense = 22, MagicDefenseMin = 19, MagicDefenseMax = 26, Hp = 37, HpMin = 32, HpMax = 43, Mana = 23, ManaMin = 20, ManaMax = 26, Evasion = 0.865f, EvasionMin = 0.74f, EvasionMax = 0.99f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300198, Name = "Luvas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.305f, EvasionMin = 0.26f, EvasionMax = 0.35f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300199, Name = "Luvas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 16, HpMin = 14, HpMax = 19, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.385f, EvasionMin = 0.33f, EvasionMax = 0.44f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300200, Name = "Botas da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.305f, EvasionMin = 0.26f, EvasionMax = 0.35f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300201, Name = "Botas da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 16, HpMin = 14, HpMax = 19, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.385f, EvasionMin = 0.33f, EvasionMax = 0.44f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300202, Name = "Cinto da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 8, DefenseMin = 7, DefenseMax = 9, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 13, HpMin = 11, HpMax = 15, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0.305f, EvasionMin = 0.26f, EvasionMax = 0.35f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300203, Name = "Cinto da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 16, HpMin = 14, HpMax = 19, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.385f, EvasionMin = 0.33f, EvasionMax = 0.44f, BuyPrice = 500, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300204, Name = "Capuz de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 9, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 9, MagicDefenseMax = 13, Hp = 18, HpMin = 16, HpMax = 21, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0.48f, EvasionMin = 0.41f, EvasionMax = 0.55f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300205, Name = "Capuz de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 16, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 23, HpMin = 20, HpMax = 27, Mana = 13, ManaMin = 11, ManaMax = 16, Evasion = 0.6f, EvasionMin = 0.51f, EvasionMax = 0.69f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300206, Name = "Gibão de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 32, DefenseMin = 27, DefenseMax = 37, MagicDefense = 32, MagicDefenseMin = 27, MagicDefenseMax = 37, Hp = 54, HpMin = 46, HpMax = 62, Mana = 31, ManaMin = 27, ManaMax = 36, Evasion = 1.4f, EvasionMin = 1.19f, EvasionMax = 1.61f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300207, Name = "Gibão de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 40, DefenseMin = 34, DefenseMax = 46, MagicDefense = 40, MagicDefenseMin = 34, MagicDefenseMax = 46, Hp = 68, HpMin = 58, HpMax = 78, Mana = 39, ManaMin = 33, ManaMax = 45, Evasion = 1.75f, EvasionMin = 1.49f, EvasionMax = 2.01f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300208, Name = "Calças de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 21, DefenseMin = 18, DefenseMax = 24, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 35, HpMin = 30, HpMax = 41, Mana = 21, ManaMin = 18, ManaMax = 24, Evasion = 0.92f, EvasionMin = 0.78f, EvasionMax = 1.06f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300209, Name = "Calças de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefense = 26, MagicDefenseMin = 22, MagicDefenseMax = 30, Hp = 44, HpMin = 38, HpMax = 51, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 1.155f, EvasionMin = 0.98f, EvasionMax = 1.33f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300210, Name = "Luvas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 9, ManaMin = 8, ManaMax = 10, Evasion = 0.4f, EvasionMin = 0.34f, EvasionMax = 0.46f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300211, Name = "Luvas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 16, HpMax = 22, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.505f, EvasionMin = 0.43f, EvasionMax = 0.58f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300212, Name = "Botas de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 9, ManaMin = 8, ManaMax = 10, Evasion = 0.4f, EvasionMin = 0.34f, EvasionMax = 0.46f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300213, Name = "Botas de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 16, HpMax = 22, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.505f, EvasionMin = 0.43f, EvasionMax = 0.58f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300214, Name = "Cinto de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 15, HpMin = 13, HpMax = 18, Mana = 9, ManaMin = 8, ManaMax = 10, Evasion = 0.4f, EvasionMin = 0.34f, EvasionMax = 0.46f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300215, Name = "Cinto de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 16, HpMax = 22, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.505f, EvasionMin = 0.43f, EvasionMax = 0.58f, BuyPrice = 600, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300216, Name = "Capuz do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 12, MagicDefenseMin = 11, MagicDefenseMax = 14, Hp = 21, HpMin = 18, HpMax = 25, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.54f, EvasionMin = 0.46f, EvasionMax = 0.62f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300217, Name = "Capuz do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 27, HpMin = 23, HpMax = 31, Mana = 15, ManaMin = 13, ManaMax = 17, Evasion = 0.68f, EvasionMin = 0.58f, EvasionMax = 0.78f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300218, Name = "Gibão do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 36, DefenseMin = 31, DefenseMax = 42, MagicDefense = 36, MagicDefenseMin = 31, MagicDefenseMax = 42, Hp = 63, HpMin = 54, HpMax = 72, Mana = 35, ManaMin = 30, ManaMax = 40, Evasion = 1.575f, EvasionMin = 1.34f, EvasionMax = 1.81f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300219, Name = "Gibão do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 46, DefenseMin = 39, DefenseMax = 53, MagicDefense = 46, MagicDefenseMin = 39, MagicDefenseMax = 53, Hp = 79, HpMin = 67, HpMax = 91, Mana = 43, ManaMin = 37, ManaMax = 50, Evasion = 1.97f, EvasionMin = 1.68f, EvasionMax = 2.26f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300220, Name = "Calças do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 24, DefenseMin = 21, DefenseMax = 28, MagicDefense = 24, MagicDefenseMin = 21, MagicDefenseMax = 28, Hp = 41, HpMin = 35, HpMax = 48, Mana = 23, ManaMin = 20, ManaMax = 26, Evasion = 1.035f, EvasionMin = 0.88f, EvasionMax = 1.19f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300221, Name = "Calças do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 30, DefenseMin = 26, DefenseMax = 35, MagicDefense = 30, MagicDefenseMin = 26, MagicDefenseMax = 35, Hp = 52, HpMin = 44, HpMax = 60, Mana = 28, ManaMin = 24, ManaMax = 33, Evasion = 1.295f, EvasionMin = 1.1f, EvasionMax = 1.49f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300222, Name = "Luvas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 10, MagicDefenseMin = 9, MagicDefenseMax = 12, Hp = 18, HpMin = 15, HpMax = 21, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.45f, EvasionMin = 0.38f, EvasionMax = 0.52f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300223, Name = "Luvas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 26, Mana = 12, ManaMin = 11, ManaMax = 14, Evasion = 0.56f, EvasionMin = 0.47f, EvasionMax = 0.65f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300224, Name = "Botas do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 10, MagicDefenseMin = 9, MagicDefenseMax = 12, Hp = 18, HpMin = 15, HpMax = 21, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.45f, EvasionMin = 0.38f, EvasionMax = 0.52f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300225, Name = "Botas do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 26, Mana = 12, ManaMin = 11, ManaMax = 14, Evasion = 0.56f, EvasionMin = 0.47f, EvasionMax = 0.65f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300226, Name = "Cinto do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 10, MagicDefenseMin = 9, MagicDefenseMax = 12, Hp = 18, HpMin = 15, HpMax = 21, Mana = 10, ManaMin = 8, ManaMax = 12, Evasion = 0.45f, EvasionMin = 0.38f, EvasionMax = 0.52f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300227, Name = "Cinto do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 26, Mana = 12, ManaMin = 11, ManaMax = 14, Evasion = 0.56f, EvasionMin = 0.47f, EvasionMax = 0.65f, BuyPrice = 700, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300228, Name = "Capuz de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 16, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 23, HpMin = 20, HpMax = 27, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0.6f, EvasionMin = 0.51f, EvasionMax = 0.69f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300229, Name = "Capuz de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 29, HpMin = 25, HpMax = 34, Mana = 16, ManaMin = 14, ManaMax = 19, Evasion = 0.75f, EvasionMin = 0.64f, EvasionMax = 0.86f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300230, Name = "Gibão de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 40, DefenseMin = 34, DefenseMax = 46, MagicDefense = 40, MagicDefenseMin = 34, MagicDefenseMax = 46, Hp = 68, HpMin = 58, HpMax = 78, Mana = 38, ManaMin = 33, ManaMax = 44, Evasion = 1.75f, EvasionMin = 1.49f, EvasionMax = 2.01f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300231, Name = "Gibão de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 50, DefenseMin = 43, DefenseMax = 58, MagicDefense = 50, MagicDefenseMin = 43, MagicDefenseMax = 58, Hp = 85, HpMin = 73, HpMax = 98, Mana = 48, ManaMin = 41, ManaMax = 55, Evasion = 2.185f, EvasionMin = 1.86f, EvasionMax = 2.51f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300232, Name = "Calças de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefense = 26, MagicDefenseMin = 22, MagicDefenseMax = 30, Hp = 45, HpMin = 38, HpMax = 52, Mana = 25, ManaMin = 22, ManaMax = 29, Evasion = 1.15f, EvasionMin = 0.98f, EvasionMax = 1.32f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300233, Name = "Calças de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 33, DefenseMin = 28, DefenseMax = 38, MagicDefense = 33, MagicDefenseMin = 28, MagicDefenseMax = 38, Hp = 56, HpMin = 48, HpMax = 64, Mana = 31, ManaMin = 27, ManaMax = 36, Evasion = 1.44f, EvasionMin = 1.23f, EvasionMax = 1.65f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300234, Name = "Luvas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 17, HpMax = 22, Mana = 11, ManaMin = 9, ManaMax = 13, Evasion = 0.495f, EvasionMin = 0.42f, EvasionMax = 0.57f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300235, Name = "Luvas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 17, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 24, HpMin = 21, HpMax = 28, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0.62f, EvasionMin = 0.53f, EvasionMax = 0.71f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300236, Name = "Botas de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 17, HpMax = 22, Mana = 11, ManaMin = 9, ManaMax = 13, Evasion = 0.495f, EvasionMin = 0.42f, EvasionMax = 0.57f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300237, Name = "Botas de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 17, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 24, HpMin = 21, HpMax = 28, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0.62f, EvasionMin = 0.53f, EvasionMax = 0.71f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300238, Name = "Cinto de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 19, HpMin = 17, HpMax = 22, Mana = 11, ManaMin = 9, ManaMax = 13, Evasion = 0.495f, EvasionMin = 0.42f, EvasionMax = 0.57f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300239, Name = "Cinto de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 17, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 24, HpMin = 21, HpMax = 28, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0.62f, EvasionMin = 0.53f, EvasionMax = 0.71f, BuyPrice = 800, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300240, Name = "Capuz do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 14, DefenseMin = 12, DefenseMax = 17, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 17, Hp = 25, HpMin = 21, HpMax = 29, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0.66f, EvasionMin = 0.56f, EvasionMax = 0.76f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300241, Name = "Capuz do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 18, DefenseMin = 16, DefenseMax = 21, MagicDefense = 18, MagicDefenseMin = 16, MagicDefenseMax = 21, Hp = 31, HpMin = 27, HpMax = 36, Mana = 17, ManaMin = 15, ManaMax = 20, Evasion = 0.825f, EvasionMin = 0.7f, EvasionMax = 0.95f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300242, Name = "Gibão do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 42, DefenseMin = 36, DefenseMax = 49, MagicDefense = 42, MagicDefenseMin = 36, MagicDefenseMax = 49, Hp = 73, HpMin = 62, HpMax = 85, Mana = 40, ManaMin = 34, ManaMax = 46, Evasion = 1.925f, EvasionMin = 1.64f, EvasionMax = 2.21f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300243, Name = "Gibão do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 53, DefenseMin = 45, DefenseMax = 61, MagicDefense = 53, MagicDefenseMin = 45, MagicDefenseMax = 61, Hp = 92, HpMin = 78, HpMax = 106, Mana = 50, ManaMin = 43, ManaMax = 58, Evasion = 2.405f, EvasionMin = 2.05f, EvasionMax = 2.76f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300244, Name = "Calças do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 28, DefenseMin = 24, DefenseMax = 32, MagicDefense = 28, MagicDefenseMin = 24, MagicDefenseMax = 32, Hp = 48, HpMin = 41, HpMax = 56, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 1.265f, EvasionMin = 1.08f, EvasionMax = 1.45f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300245, Name = "Calças do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 35, DefenseMin = 30, DefenseMax = 40, MagicDefense = 35, MagicDefenseMin = 30, MagicDefenseMax = 40, Hp = 60, HpMin = 51, HpMax = 69, Mana = 33, ManaMin = 28, ManaMax = 38, Evasion = 1.58f, EvasionMin = 1.35f, EvasionMax = 1.81f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300246, Name = "Luvas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 12, DefenseMin = 10, DefenseMax = 14, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 21, HpMin = 18, HpMax = 24, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.55f, EvasionMin = 0.47f, EvasionMax = 0.63f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300247, Name = "Luvas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 26, HpMin = 22, HpMax = 30, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0.69f, EvasionMin = 0.59f, EvasionMax = 0.79f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300248, Name = "Botas do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 12, DefenseMin = 10, DefenseMax = 14, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 21, HpMin = 18, HpMax = 24, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.55f, EvasionMin = 0.47f, EvasionMax = 0.63f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300249, Name = "Botas do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 26, HpMin = 22, HpMax = 30, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0.69f, EvasionMin = 0.59f, EvasionMax = 0.79f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300250, Name = "Cinto do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 12, DefenseMin = 10, DefenseMax = 14, MagicDefense = 12, MagicDefenseMin = 10, MagicDefenseMax = 14, Hp = 21, HpMin = 18, HpMax = 24, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0.55f, EvasionMin = 0.47f, EvasionMax = 0.63f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300251, Name = "Cinto do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 26, HpMin = 22, HpMax = 30, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0.69f, EvasionMin = 0.59f, EvasionMax = 0.79f, BuyPrice = 900, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300252, Name = "Capuz da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 26, HpMin = 22, HpMax = 30, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0.72f, EvasionMin = 0.61f, EvasionMax = 0.83f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300253, Name = "Capuz da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 19, DefenseMin = 17, DefenseMax = 22, MagicDefense = 19, MagicDefenseMin = 17, MagicDefenseMax = 22, Hp = 33, HpMin = 28, HpMax = 38, Mana = 18, ManaMin = 15, ManaMax = 21, Evasion = 0.9f, EvasionMin = 0.76f, EvasionMax = 1.04f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300254, Name = "Gibão da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 45, DefenseMin = 39, DefenseMax = 52, MagicDefense = 45, MagicDefenseMin = 39, MagicDefenseMax = 52, Hp = 77, HpMin = 65, HpMax = 89, Mana = 42, ManaMin = 36, ManaMax = 48, Evasion = 2.095f, EvasionMin = 1.78f, EvasionMax = 2.41f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300255, Name = "Gibão da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 56, DefenseMin = 48, DefenseMax = 65, MagicDefense = 56, MagicDefenseMin = 48, MagicDefenseMax = 65, Hp = 96, HpMin = 82, HpMax = 111, Mana = 52, ManaMin = 45, ManaMax = 60, Evasion = 2.62f, EvasionMin = 2.23f, EvasionMax = 3.01f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300256, Name = "Calças da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 29, DefenseMin = 25, DefenseMax = 34, MagicDefense = 29, MagicDefenseMin = 25, MagicDefenseMax = 34, Hp = 50, HpMin = 43, HpMax = 58, Mana = 27, ManaMin = 23, ManaMax = 32, Evasion = 1.38f, EvasionMin = 1.17f, EvasionMax = 1.59f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300257, Name = "Calças da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 37, DefenseMin = 32, DefenseMax = 43, MagicDefense = 37, MagicDefenseMin = 32, MagicDefenseMax = 43, Hp = 63, HpMin = 54, HpMax = 73, Mana = 34, ManaMin = 29, ManaMax = 40, Evasion = 1.725f, EvasionMin = 1.46f, EvasionMax = 1.99f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300258, Name = "Luvas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 25, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.6f, EvasionMin = 0.51f, EvasionMax = 0.69f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300259, Name = "Luvas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 16, DefenseMin = 14, DefenseMax = 19, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 27, HpMin = 23, HpMax = 32, Mana = 15, ManaMin = 13, ManaMax = 17, Evasion = 0.75f, EvasionMin = 0.64f, EvasionMax = 0.86f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300260, Name = "Botas da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 25, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.6f, EvasionMin = 0.51f, EvasionMax = 0.69f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300261, Name = "Botas da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 16, DefenseMin = 14, DefenseMax = 19, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 27, HpMin = 23, HpMax = 32, Mana = 15, ManaMin = 13, ManaMax = 17, Evasion = 0.75f, EvasionMin = 0.64f, EvasionMax = 0.86f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300262, Name = "Cinto da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = false, AllowedClasses = "Arqueiro,Ladino", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 22, HpMin = 19, HpMax = 25, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0.6f, EvasionMin = 0.51f, EvasionMax = 0.69f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300263, Name = "Cinto da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = true, AllowedClasses = "Arqueiro,Ladino", Defense = 16, DefenseMin = 14, DefenseMax = 19, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 27, HpMin = 23, HpMax = 32, Mana = 15, ManaMin = 13, ManaMax = 17, Evasion = 0.75f, EvasionMin = 0.64f, EvasionMax = 0.86f, BuyPrice = 1000, AffixPool = new List<string>("Destreza,Agilidade,Precisao,ChanceCritica,DanoCriticoBonus,VelocidadeAtaque,VelocidadeMovimento,Evasao".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300264, Name = "Elmo do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300265, Name = "Elmo do Despertar", Type = (ItemType)1, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 2, DefenseMin = 2, DefenseMax = 2, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 3, HpMin = 3, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300266, Name = "Couraça do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 7, HpMin = 6, HpMax = 8, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300267, Name = "Couraça do Despertar", Type = (ItemType)2, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 8, HpMin = 7, HpMax = 10, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300268, Name = "Grevas do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 2, DefenseMin = 2, DefenseMax = 3, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 4, HpMin = 4, HpMax = 5, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300269, Name = "Grevas do Despertar", Type = (ItemType)5, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 6, HpMin = 5, HpMax = 7, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300270, Name = "Manoplas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 0, MagicDefenseMin = 0, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 0, ManaMin = 0, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300271, Name = "Manoplas do Despertar", Type = (ItemType)4, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300272, Name = "Botas do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 0, MagicDefenseMin = 0, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 0, ManaMin = 0, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300273, Name = "Botas do Despertar", Type = (ItemType)6, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300274, Name = "Cinturão do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 1, MagicDefense = 0, MagicDefenseMin = 0, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 2, Mana = 0, ManaMin = 0, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300275, Name = "Cinturão do Despertar", Type = (ItemType)3, RequiredLevel = 1, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 1, DefenseMin = 1, DefenseMax = 2, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 2, HpMin = 2, HpMax = 3, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 10, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300276, Name = "Elmo de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 5, HpMin = 5, HpMax = 6, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300277, Name = "Elmo de Bruma Baixa", Type = (ItemType)1, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 4, DefenseMin = 4, DefenseMax = 5, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 7, HpMin = 6, HpMax = 8, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300278, Name = "Couraça de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 10, DefenseMin = 9, DefenseMax = 12, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 15, HpMin = 13, HpMax = 18, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300279, Name = "Couraça de Bruma Baixa", Type = (ItemType)2, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 13, DefenseMin = 11, DefenseMax = 15, MagicDefense = 4, MagicDefenseMin = 4, MagicDefenseMax = 5, Hp = 20, HpMin = 17, HpMax = 23, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300280, Name = "Grevas de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 7, DefenseMin = 6, DefenseMax = 8, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 10, HpMin = 9, HpMax = 12, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300281, Name = "Grevas de Bruma Baixa", Type = (ItemType)5, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 8, DefenseMin = 7, DefenseMax = 10, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 13, HpMin = 11, HpMax = 15, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300282, Name = "Manoplas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 3, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 4, HpMin = 4, HpMax = 5, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300283, Name = "Manoplas de Bruma Baixa", Type = (ItemType)4, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 5, HpMin = 5, HpMax = 6, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300284, Name = "Botas de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 3, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 4, HpMin = 4, HpMax = 5, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300285, Name = "Botas de Bruma Baixa", Type = (ItemType)6, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 5, HpMin = 5, HpMax = 6, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300286, Name = "Cinturão de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 3, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 4, HpMin = 4, HpMax = 5, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300287, Name = "Cinturão de Bruma Baixa", Type = (ItemType)3, RequiredLevel = 10, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 3, DefenseMin = 3, DefenseMax = 4, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 1, Hp = 5, HpMin = 5, HpMax = 6, Mana = 1, ManaMin = 1, ManaMax = 1, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 100, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300288, Name = "Elmo de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 8, HpMin = 7, HpMax = 10, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300289, Name = "Elmo de Valebosque", Type = (ItemType)1, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 10, HpMin = 9, HpMax = 12, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300290, Name = "Couraça de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 24, HpMin = 21, HpMax = 28, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300291, Name = "Couraça de Valebosque", Type = (ItemType)2, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 30, HpMin = 26, HpMax = 35, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300292, Name = "Grevas de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300293, Name = "Grevas de Valebosque", Type = (ItemType)5, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 14, DefenseMin = 12, DefenseMax = 17, MagicDefense = 4, MagicDefenseMin = 4, MagicDefenseMax = 5, Hp = 20, HpMin = 17, HpMax = 23, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300294, Name = "Manoplas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 7, HpMin = 6, HpMax = 8, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300295, Name = "Manoplas de Valebosque", Type = (ItemType)4, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 8, HpMin = 7, HpMax = 10, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300296, Name = "Botas de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 7, HpMin = 6, HpMax = 8, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300297, Name = "Botas de Valebosque", Type = (ItemType)6, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 8, HpMin = 7, HpMax = 10, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300298, Name = "Cinturão de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 5, DefenseMin = 4, DefenseMax = 6, MagicDefense = 1, MagicDefenseMin = 1, MagicDefenseMax = 2, Hp = 7, HpMin = 6, HpMax = 8, Mana = 1, ManaMin = 1, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300299, Name = "Cinturão de Valebosque", Type = (ItemType)3, RequiredLevel = 20, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 6, DefenseMin = 5, DefenseMax = 7, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 2, Hp = 8, HpMin = 7, HpMax = 10, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 200, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300300, Name = "Elmo da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 9, DefenseMin = 8, DefenseMax = 10, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 12, HpMin = 10, HpMax = 14, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300301, Name = "Elmo da Vigília Cinzenta", Type = (ItemType)1, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 11, DefenseMin = 10, DefenseMax = 13, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 15, HpMin = 13, HpMax = 17, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300302, Name = "Couraça da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 35, HpMin = 30, HpMax = 40, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300303, Name = "Couraça da Vigília Cinzenta", Type = (ItemType)2, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 33, DefenseMin = 28, DefenseMax = 38, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 43, HpMin = 37, HpMax = 50, Mana = 8, ManaMin = 7, ManaMax = 10, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300304, Name = "Grevas da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 23, HpMin = 20, HpMax = 26, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300305, Name = "Grevas da Vigília Cinzenta", Type = (ItemType)5, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 21, DefenseMin = 18, DefenseMax = 25, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 28, HpMin = 24, HpMax = 33, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300306, Name = "Manoplas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 10, HpMin = 8, HpMax = 12, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300307, Name = "Manoplas da Vigília Cinzenta", Type = (ItemType)4, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 12, HpMin = 11, HpMax = 14, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300308, Name = "Botas da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 10, HpMin = 8, HpMax = 12, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300309, Name = "Botas da Vigília Cinzenta", Type = (ItemType)6, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 12, HpMin = 11, HpMax = 14, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300310, Name = "Cinturão da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 7, DefenseMin = 6, DefenseMax = 9, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 10, HpMin = 8, HpMax = 12, Mana = 2, ManaMin = 2, ManaMax = 2, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300311, Name = "Cinturão da Vigília Cinzenta", Type = (ItemType)3, RequiredLevel = 30, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 9, DefenseMin = 8, DefenseMax = 11, MagicDefense = 2, MagicDefenseMin = 2, MagicDefenseMax = 3, Hp = 12, HpMin = 11, HpMax = 14, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 300, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300312, Name = "Elmo de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 10, DefenseMax = 14, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 15, HpMin = 13, HpMax = 18, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300313, Name = "Elmo de Pedraforte", Type = (ItemType)1, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 4, MagicDefenseMin = 4, MagicDefenseMax = 5, Hp = 19, HpMin = 17, HpMax = 22, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300314, Name = "Couraça de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 35, DefenseMin = 30, DefenseMax = 40, MagicDefense = 10, MagicDefenseMin = 9, MagicDefenseMax = 12, Hp = 45, HpMin = 39, HpMax = 52, Mana = 8, ManaMin = 7, ManaMax = 10, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300315, Name = "Couraça de Pedraforte", Type = (ItemType)2, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 43, DefenseMin = 37, DefenseMax = 50, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 56, HpMin = 48, HpMax = 65, Mana = 11, ManaMin = 9, ManaMax = 13, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300316, Name = "Grevas de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 23, DefenseMin = 20, DefenseMax = 26, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 29, HpMin = 25, HpMax = 34, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300317, Name = "Grevas de Pedraforte", Type = (ItemType)5, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 28, DefenseMin = 24, DefenseMax = 33, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 37, HpMin = 32, HpMax = 43, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300318, Name = "Manoplas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 10, DefenseMin = 8, DefenseMax = 12, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 3, Hp = 13, HpMin = 11, HpMax = 15, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300319, Name = "Manoplas de Pedraforte", Type = (ItemType)4, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300320, Name = "Botas de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 10, DefenseMin = 8, DefenseMax = 12, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 3, Hp = 13, HpMin = 11, HpMax = 15, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300321, Name = "Botas de Pedraforte", Type = (ItemType)6, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300322, Name = "Cinturão de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 10, DefenseMin = 8, DefenseMax = 12, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 3, Hp = 13, HpMin = 11, HpMax = 15, Mana = 2, ManaMin = 2, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300323, Name = "Cinturão de Pedraforte", Type = (ItemType)3, RequiredLevel = 40, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 3, MagicDefenseMin = 3, MagicDefenseMax = 4, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 400, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300324, Name = "Elmo da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 20, HpMin = 17, HpMax = 23, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300325, Name = "Elmo da Aurora Partida", Type = (ItemType)1, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 24, HpMin = 21, HpMax = 28, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300326, Name = "Couraça da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 43, DefenseMin = 37, DefenseMax = 50, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 57, HpMin = 49, HpMax = 66, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300327, Name = "Couraça da Aurora Partida", Type = (ItemType)2, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 54, DefenseMin = 46, DefenseMax = 63, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 72, HpMin = 61, HpMax = 83, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300328, Name = "Grevas da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 28, DefenseMin = 24, DefenseMax = 33, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 38, HpMin = 32, HpMax = 44, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300329, Name = "Grevas da Aurora Partida", Type = (ItemType)5, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 36, DefenseMin = 31, DefenseMax = 41, MagicDefense = 11, MagicDefenseMin = 10, MagicDefenseMax = 13, Hp = 47, HpMin = 40, HpMax = 55, Mana = 8, ManaMin = 7, ManaMax = 10, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300330, Name = "Manoplas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300331, Name = "Manoplas da Aurora Partida", Type = (ItemType)4, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 21, HpMin = 18, HpMax = 24, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300332, Name = "Botas da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300333, Name = "Botas da Aurora Partida", Type = (ItemType)6, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 21, HpMin = 18, HpMax = 24, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300334, Name = "Cinturão da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 12, DefenseMin = 11, DefenseMax = 14, MagicDefense = 4, MagicDefenseMin = 3, MagicDefenseMax = 5, Hp = 16, HpMin = 14, HpMax = 19, Mana = 3, ManaMin = 3, ManaMax = 3, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300335, Name = "Cinturão da Aurora Partida", Type = (ItemType)3, RequiredLevel = 50, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 18, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 21, HpMin = 18, HpMax = 24, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 500, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300336, Name = "Elmo de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 18, DefenseMin = 15, DefenseMax = 21, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 24, HpMin = 20, HpMax = 28, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300337, Name = "Elmo de Umbra Alta", Type = (ItemType)1, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 30, HpMin = 26, HpMax = 34, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300338, Name = "Couraça de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 52, DefenseMin = 45, DefenseMax = 60, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 70, HpMin = 60, HpMax = 80, Mana = 12, ManaMin = 10, ManaMax = 14, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300339, Name = "Couraça de Umbra Alta", Type = (ItemType)2, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 65, DefenseMin = 56, DefenseMax = 75, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 87, HpMin = 74, HpMax = 101, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300340, Name = "Grevas de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 34, DefenseMin = 29, DefenseMax = 40, MagicDefense = 11, MagicDefenseMin = 9, MagicDefenseMax = 13, Hp = 46, HpMin = 39, HpMax = 53, Mana = 8, ManaMin = 7, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300341, Name = "Grevas de Umbra Alta", Type = (ItemType)5, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 43, DefenseMin = 37, DefenseMax = 50, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 57, HpMin = 49, HpMax = 66, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300342, Name = "Manoplas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 20, HpMin = 17, HpMax = 23, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300343, Name = "Manoplas de Umbra Alta", Type = (ItemType)4, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 21, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300344, Name = "Botas de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 20, HpMin = 17, HpMax = 23, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300345, Name = "Botas de Umbra Alta", Type = (ItemType)6, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 21, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300346, Name = "Cinturão de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 15, DefenseMin = 13, DefenseMax = 17, MagicDefense = 5, MagicDefenseMin = 4, MagicDefenseMax = 6, Hp = 20, HpMin = 17, HpMax = 23, Mana = 3, ManaMin = 3, ManaMax = 4, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300347, Name = "Cinturão de Umbra Alta", Type = (ItemType)3, RequiredLevel = 60, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 21, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 600, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300348, Name = "Elmo do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 21, DefenseMin = 18, DefenseMax = 24, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 27, HpMin = 23, HpMax = 32, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300349, Name = "Elmo do Juramento Antigo", Type = (ItemType)1, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 34, HpMin = 29, HpMax = 40, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300350, Name = "Couraça do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 61, DefenseMin = 52, DefenseMax = 70, MagicDefense = 19, MagicDefenseMin = 16, MagicDefenseMax = 22, Hp = 80, HpMin = 68, HpMax = 93, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300351, Name = "Couraça do Juramento Antigo", Type = (ItemType)2, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 76, DefenseMin = 65, DefenseMax = 88, MagicDefense = 24, MagicDefenseMin = 20, MagicDefenseMax = 28, Hp = 101, HpMin = 86, HpMax = 116, Mana = 17, ManaMin = 15, ManaMax = 20, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300352, Name = "Grevas do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 40, DefenseMin = 34, DefenseMax = 46, MagicDefense = 13, MagicDefenseMin = 11, MagicDefenseMax = 15, Hp = 53, HpMin = 45, HpMax = 61, Mana = 9, ManaMin = 8, ManaMax = 11, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300353, Name = "Grevas do Juramento Antigo", Type = (ItemType)5, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 50, DefenseMin = 43, DefenseMax = 58, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 18, Hp = 66, HpMin = 56, HpMax = 76, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300354, Name = "Manoplas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 23, HpMin = 20, HpMax = 26, Mana = 4, ManaMin = 3, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300355, Name = "Manoplas do Juramento Antigo", Type = (ItemType)4, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 28, HpMin = 24, HpMax = 33, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300356, Name = "Botas do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 23, HpMin = 20, HpMax = 26, Mana = 4, ManaMin = 3, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300357, Name = "Botas do Juramento Antigo", Type = (ItemType)6, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 28, HpMin = 24, HpMax = 33, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300358, Name = "Cinturão do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 17, DefenseMin = 15, DefenseMax = 20, MagicDefense = 5, MagicDefenseMin = 5, MagicDefenseMax = 6, Hp = 23, HpMin = 20, HpMax = 26, Mana = 4, ManaMin = 3, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300359, Name = "Cinturão do Juramento Antigo", Type = (ItemType)3, RequiredLevel = 70, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 28, HpMin = 24, HpMax = 33, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 700, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300360, Name = "Elmo de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 26, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 30, HpMin = 26, HpMax = 35, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300361, Name = "Elmo de Aço Estelar", Type = (ItemType)1, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 28, DefenseMin = 24, DefenseMax = 33, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 10, Hp = 38, HpMin = 33, HpMax = 44, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300362, Name = "Couraça de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 66, DefenseMin = 57, DefenseMax = 76, MagicDefense = 21, MagicDefenseMin = 18, MagicDefenseMax = 24, Hp = 89, HpMin = 76, HpMax = 103, Mana = 15, ManaMin = 13, ManaMax = 18, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300363, Name = "Couraça de Aço Estelar", Type = (ItemType)2, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 83, DefenseMin = 71, DefenseMax = 96, MagicDefense = 26, MagicDefenseMin = 22, MagicDefenseMax = 30, Hp = 111, HpMin = 95, HpMax = 128, Mana = 20, ManaMin = 17, ManaMax = 23, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300364, Name = "Grevas de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 43, DefenseMin = 37, DefenseMax = 50, MagicDefense = 14, MagicDefenseMin = 12, MagicDefenseMax = 16, Hp = 58, HpMin = 50, HpMax = 67, Mana = 10, ManaMin = 9, ManaMax = 12, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300365, Name = "Grevas de Aço Estelar", Type = (ItemType)5, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 54, DefenseMin = 46, DefenseMax = 63, MagicDefense = 17, MagicDefenseMin = 15, MagicDefenseMax = 20, Hp = 73, HpMin = 62, HpMax = 84, Mana = 13, ManaMin = 11, ManaMax = 15, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300366, Name = "Manoplas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 22, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300367, Name = "Manoplas de Aço Estelar", Type = (ItemType)4, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 23, DefenseMin = 20, DefenseMax = 27, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 32, HpMin = 27, HpMax = 37, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300368, Name = "Botas de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 22, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300369, Name = "Botas de Aço Estelar", Type = (ItemType)6, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 23, DefenseMin = 20, DefenseMax = 27, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 32, HpMin = 27, HpMax = 37, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300370, Name = "Cinturão de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 19, DefenseMin = 16, DefenseMax = 22, MagicDefense = 6, MagicDefenseMin = 5, MagicDefenseMax = 7, Hp = 25, HpMin = 22, HpMax = 29, Mana = 4, ManaMin = 4, ManaMax = 5, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300371, Name = "Cinturão de Aço Estelar", Type = (ItemType)3, RequiredLevel = 80, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 23, DefenseMin = 20, DefenseMax = 27, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 9, Hp = 32, HpMin = 27, HpMax = 37, Mana = 5, ManaMin = 5, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 800, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300372, Name = "Elmo do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 24, DefenseMin = 21, DefenseMax = 28, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 33, HpMin = 28, HpMax = 38, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300373, Name = "Elmo do Eclipse de Mitthara", Type = (ItemType)1, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 30, DefenseMin = 26, DefenseMax = 35, MagicDefense = 9, MagicDefenseMin = 8, MagicDefenseMax = 11, Hp = 41, HpMin = 35, HpMax = 47, Mana = 7, ManaMin = 6, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300374, Name = "Couraça do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 72, DefenseMin = 61, DefenseMax = 83, MagicDefense = 22, MagicDefenseMin = 19, MagicDefenseMax = 26, Hp = 96, HpMin = 82, HpMax = 111, Mana = 17, ManaMin = 15, ManaMax = 20, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300375, Name = "Couraça do Eclipse de Mitthara", Type = (ItemType)2, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 89, DefenseMin = 76, DefenseMax = 103, MagicDefense = 28, MagicDefenseMin = 24, MagicDefenseMax = 33, Hp = 120, HpMin = 102, HpMax = 138, Mana = 22, ManaMin = 19, ManaMax = 25, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300376, Name = "Grevas do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 47, DefenseMin = 40, DefenseMax = 54, MagicDefense = 15, MagicDefenseMin = 13, MagicDefenseMax = 17, Hp = 63, HpMin = 54, HpMax = 73, Mana = 11, ManaMin = 10, ManaMax = 13, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300377, Name = "Grevas do Eclipse de Mitthara", Type = (ItemType)5, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 59, DefenseMin = 50, DefenseMax = 68, MagicDefense = 18, MagicDefenseMin = 16, MagicDefenseMax = 21, Hp = 79, HpMin = 67, HpMax = 91, Mana = 14, ManaMin = 12, ManaMax = 17, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300378, Name = "Manoplas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 20, DefenseMin = 17, DefenseMax = 24, MagicDefense = 6, MagicDefenseMin = 6, MagicDefenseMax = 7, Hp = 27, HpMin = 23, HpMax = 32, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300379, Name = "Manoplas do Eclipse de Mitthara", Type = (ItemType)4, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 25, DefenseMin = 22, DefenseMax = 29, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 34, HpMin = 29, HpMax = 40, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300380, Name = "Botas do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 20, DefenseMin = 17, DefenseMax = 24, MagicDefense = 6, MagicDefenseMin = 6, MagicDefenseMax = 7, Hp = 27, HpMin = 23, HpMax = 32, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300381, Name = "Botas do Eclipse de Mitthara", Type = (ItemType)6, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 25, DefenseMin = 22, DefenseMax = 29, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 34, HpMin = 29, HpMax = 40, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300382, Name = "Cinturão do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 20, DefenseMin = 17, DefenseMax = 24, MagicDefense = 6, MagicDefenseMin = 6, MagicDefenseMax = 7, Hp = 27, HpMin = 23, HpMax = 32, Mana = 5, ManaMin = 4, ManaMax = 6, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300383, Name = "Cinturão do Eclipse de Mitthara", Type = (ItemType)3, RequiredLevel = 90, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 25, DefenseMin = 22, DefenseMax = 29, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 9, Hp = 34, HpMin = 29, HpMax = 40, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 900, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300384, Name = "Elmo da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 26, DefenseMin = 22, DefenseMax = 30, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 35, HpMin = 30, HpMax = 40, Mana = 7, ManaMin = 6, ManaMax = 8, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300385, Name = "Elmo da Ascensão", Type = (ItemType)1, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 33, DefenseMin = 28, DefenseMax = 38, MagicDefense = 10, MagicDefenseMin = 9, MagicDefenseMax = 12, Hp = 43, HpMin = 37, HpMax = 50, Mana = 9, ManaMin = 8, ManaMax = 10, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300386, Name = "Couraça da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 77, DefenseMin = 65, DefenseMax = 89, MagicDefense = 24, MagicDefenseMin = 21, MagicDefenseMax = 28, Hp = 101, HpMin = 86, HpMax = 117, Mana = 21, ManaMin = 18, ManaMax = 24, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300387, Name = "Couraça da Ascensão", Type = (ItemType)2, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 96, DefenseMin = 82, DefenseMax = 111, MagicDefense = 30, MagicDefenseMin = 26, MagicDefenseMax = 35, Hp = 127, HpMin = 108, HpMax = 146, Mana = 26, ManaMin = 22, ManaMax = 30, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300388, Name = "Grevas da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 50, DefenseMin = 43, DefenseMax = 58, MagicDefense = 16, MagicDefenseMin = 14, MagicDefenseMax = 19, Hp = 67, HpMin = 57, HpMax = 77, Mana = 14, ManaMin = 12, ManaMax = 16, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300389, Name = "Grevas da Ascensão", Type = (ItemType)5, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 63, DefenseMin = 54, DefenseMax = 73, MagicDefense = 20, MagicDefenseMin = 17, MagicDefenseMax = 23, Hp = 83, HpMin = 71, HpMax = 96, Mana = 17, ManaMin = 15, ManaMax = 20, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300390, Name = "Manoplas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 29, HpMin = 25, HpMax = 33, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300391, Name = "Manoplas da Ascensão", Type = (ItemType)4, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 27, DefenseMin = 23, DefenseMax = 32, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 36, HpMin = 31, HpMax = 42, Mana = 7, ManaMin = 6, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300392, Name = "Botas da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 29, HpMin = 25, HpMax = 33, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300393, Name = "Botas da Ascensão", Type = (ItemType)6, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 27, DefenseMin = 23, DefenseMax = 32, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 36, HpMin = 31, HpMax = 42, Mana = 7, ManaMin = 6, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300394, Name = "Cinturão da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = false, AllowedClasses = "Berseker,Guardiao", Defense = 22, DefenseMin = 19, DefenseMax = 25, MagicDefense = 7, MagicDefenseMin = 6, MagicDefenseMax = 8, Hp = 29, HpMin = 25, HpMax = 33, Mana = 6, ManaMin = 5, ManaMax = 7, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
            new() { Id = 300395, Name = "Cinturão da Ascensão", Type = (ItemType)3, RequiredLevel = 100, IsElite = true, AllowedClasses = "Berseker,Guardiao", Defense = 27, DefenseMin = 23, DefenseMax = 32, MagicDefense = 8, MagicDefenseMin = 7, MagicDefenseMax = 10, Hp = 36, HpMin = 31, HpMax = 42, Mana = 7, ManaMin = 6, ManaMax = 9, Evasion = 0f, EvasionMin = 0f, EvasionMax = 0f, BuyPrice = 1000, AffixPool = new List<string>("Forca,Hp,DefesaFisica,Tenacidade,RegeneracaoVida,Reflexao,PenetracaoArmadura".Split(',', StringSplitOptions.RemoveEmptyEntries)) },
        };
        // ARMOR_CATALOG_END
        var definitionsWithRanges = new HashSet<int>();
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM item_definitions WHERE definition_data <> ''";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) definitionsWithRanges.Add(reader.GetInt32(0));
        }

        foreach (var item in items.Concat(armorItems))
            if (item.Id <= 102 || !definitionsWithRanges.Contains(item.Id))
                SaveItemDefinition(item);

        Logger.Info($"{items.Count} definicoes de item sincronizadas com o catalogo oficial.");
    }
}

public class CharacterRow
{
    public int Id { get; set; }
    public int SlotIndex { get; set; }
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public string Race { get; set; } = "";
    public int Level { get; set; } = 1;
    public long Xp { get; set; }
    public int Forca { get; set; }
    public int Agilidade { get; set; }
    public int Destreza { get; set; }
    public int Inteligencia { get; set; }
    public float PosX { get; set; } = 1000f;
    public float PosY { get; set; } = 1000f;
    public int BankGold { get; set; }
    public int Gold { get; set; }
    public int StatPoints { get; set; }
    public string CurrentMap { get; set; } = "main";
}


