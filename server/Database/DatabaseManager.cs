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

            CREATE TABLE IF NOT EXISTS character_pets (
                id SERIAL PRIMARY KEY,
                character_id INT NOT NULL,
                pet_id INT NOT NULL,
                pet_name VARCHAR(255) NOT NULL DEFAULT '',
                FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE
            );

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
                buy_price INT NOT NULL DEFAULT 0,
                affix_pool TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();

        TryAddColumns(conn);
        Logger.Info("Banco de dados PostgreSQL inicializado.");
    }

    private void TryAddColumns(NpgsqlConnection conn)
    {
        var columns = new (string table, string column, string type)[]
        {
            ("accounts", "security_question", "VARCHAR(255) NOT NULL DEFAULT ''"),
            ("accounts", "security_answer", "VARCHAR(255) NOT NULL DEFAULT ''"),
            ("accounts", "salt", "VARCHAR(255) NOT NULL DEFAULT ''"),
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
                Logger.Info($"Nao foi possivel adicionar coluna '{column}' em '{table}': {ex.Message}");
            }
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
        cmd.CommandText = "SELECT id, slot_index, name, class, race, level, xp, forca, agilidade, destreza, inteligencia, pos_x, pos_y, bank_gold, gold, stat_points FROM characters WHERE account_id = @a ORDER BY slot_index";
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
                Xp = reader.GetInt32(6),
                Forca = reader.GetInt32(7),
                Agilidade = reader.GetInt32(8),
                Destreza = reader.GetInt32(9),
                Inteligencia = reader.GetInt32(10),
                PosX = (float)reader.GetDouble(11),
                PosY = (float)reader.GetDouble(12),
                BankGold = reader.GetInt32(13),
                Gold = reader.GetInt32(14),
                StatPoints = reader.GetInt32(15),
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

    public void SaveCharacterPosition(int characterId, float x, float y)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE characters SET pos_x = @x, pos_y = @y WHERE id = @i";
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y);
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

    public List<Mithara.Server.Entities.ItemInstance> LoadItems(int characterId)
    {
        var result = new List<Mithara.Server.Entities.ItemInstance>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, item_id, slot, quantity, refine_level FROM items WHERE character_id = @c ORDER BY slot";
        cmd.Parameters.AddWithValue("@c", characterId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Mithara.Server.Entities.ItemInstance
            {
                DbId = reader.GetInt32(0),
                ItemId = reader.GetInt32(1),
                Slot = reader.GetInt32(2),
                Quantity = reader.GetInt32(3),
                RefineLevel = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            });
        }
        return result;
    }

    public void SaveItem(int characterId, Mithara.Server.Entities.ItemInstance item)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        if (item.DbId > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET slot = @s, quantity = @q, refine_level = @r WHERE id = @i AND character_id = @c";
            cmd.Parameters.AddWithValue("@s", item.Slot);
            cmd.Parameters.AddWithValue("@q", item.Quantity);
            cmd.Parameters.AddWithValue("@r", item.RefineLevel);
            cmd.Parameters.AddWithValue("@i", item.DbId);
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.ExecuteNonQuery();
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO items (character_id, slot, item_id, quantity, refine_level) VALUES (@c, @s, @ii, @q, @r) RETURNING id";
            cmd.Parameters.AddWithValue("@c", characterId);
            cmd.Parameters.AddWithValue("@s", item.Slot);
            cmd.Parameters.AddWithValue("@ii", item.ItemId);
            cmd.Parameters.AddWithValue("@q", item.Quantity);
            cmd.Parameters.AddWithValue("@r", item.RefineLevel);
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
        using var cmd = conn.CreateCommand();
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
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM guilds WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", guildId);
        cmd.ExecuteNonQuery();
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
        mcmd.CommandText = "SELECT guild_id, entity_id, name, rank FROM guild_members";
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

        using var delItems = conn.CreateCommand();
        delItems.CommandText = "DELETE FROM items WHERE character_id = @c";
        delItems.Parameters.AddWithValue("@c", characterId);
        delItems.ExecuteNonQuery();

        using var delQuests = conn.CreateCommand();
        delQuests.CommandText = "DELETE FROM player_quests WHERE character_id = @c";
        delQuests.Parameters.AddWithValue("@c", characterId);
        delQuests.ExecuteNonQuery();

        using var delGuild = conn.CreateCommand();
        delGuild.CommandText = "DELETE FROM guild_members WHERE name = @n";
        delGuild.Parameters.AddWithValue("@n", characterName);
        delGuild.ExecuteNonQuery();

        using var delChar = conn.CreateCommand();
        delChar.CommandText = "DELETE FROM characters WHERE id = @c";
        delChar.Parameters.AddWithValue("@c", characterId);
        delChar.ExecuteNonQuery();

        tx.Commit();
        Logger.Info($"[DB] Personagem {characterId} ({characterName}) deletado (itens+quests+guild+char).");
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
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO items (character_id, slot, item_id, quantity, refine_level) VALUES (@c, @s, @ii, @q, @r) RETURNING id";
                ins.Parameters.AddWithValue("@c", characterId);
                ins.Parameters.AddWithValue("@s", item.Slot);
                ins.Parameters.AddWithValue("@ii", item.ItemId);
                ins.Parameters.AddWithValue("@q", item.Quantity);
                ins.Parameters.AddWithValue("@r", item.RefineLevel);
                item.DbId = Convert.ToInt32(ins.ExecuteScalar());
            }

            foreach (var kv in player.Equipment)
            {
                var item = kv.Value;
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO items (character_id, slot, item_id, quantity, refine_level) VALUES (@c, @s, @ii, @q, @r) RETURNING id";
                ins.Parameters.AddWithValue("@c", characterId);
                ins.Parameters.AddWithValue("@s", item.Slot);
                ins.Parameters.AddWithValue("@ii", item.ItemId);
                ins.Parameters.AddWithValue("@q", item.Quantity);
                ins.Parameters.AddWithValue("@r", item.RefineLevel);
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
        cmd.CommandText = "SELECT guild_id, entity_id, rank FROM guild_members WHERE name = @n";
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
        cmd.CommandText = "SELECT id, name, type, max_stack, is_stackable, is_bag, extra_slots, forca, agilidade, destreza, inteligencia, base_attack, defense, buy_price, affix_pool FROM item_definitions ORDER BY id";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var affixStr = reader.IsDBNull(14) ? "" : reader.GetString(14);
            var affixPool = string.IsNullOrWhiteSpace(affixStr)
                ? new List<string>()
                : affixStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            result.Add(new ItemDefinition
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
            });
        }
        return result;
    }

    public void SaveItemDefinition(ItemDefinition def)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO item_definitions (id, name, type, max_stack, is_stackable, is_bag, extra_slots, forca, agilidade, destreza, inteligencia, base_attack, defense, buy_price, affix_pool)
            VALUES (@id, @name, @type, @max, @stack, @bag, @extra, @forca, @agi, @dex, @int, @atk, @def, @price, @pool)
            ON CONFLICT (id) DO UPDATE SET
                name = @name, type = @type, max_stack = @max, is_stackable = @stack, is_bag = @bag,
                extra_slots = @extra, forca = @forca, agilidade = @agi, destreza = @dex, inteligencia = @int,
                base_attack = @atk, defense = @def, buy_price = @price, affix_pool = @pool";
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
        cmd.ExecuteNonQuery();
    }

    public void SeedItemDefinitions()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM item_definitions";
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());
        if (count > 0)
        {
            Logger.Info($"Item definitions ja existem no banco ({count} registros). Pulando seed.");
            return;
        }

        Logger.Info("Populando item_definitions com dados iniciais...");

        var bowPool = "ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Agilidade,PenetracaoArmadura,Evasao";
        var aljavaPool = "ChanceCritica,DanoCriticoBonus,Precisao,VelocidadeAtaque,Evasao,VelocidadeMovimento,PenetracaoArmadura";

        var items = new List<ItemDefinition>
        {
            // ARCOS (1001-1021)
            new() { Id = 1001, Name = "Arco de Madeira", Type = ItemType.Weapon, BaseAttack = 2, Destreza = 2, BuyPrice = 18, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1002, Name = "Arco do Aprendiz", Type = ItemType.Weapon, BaseAttack = 4, Destreza = 3, BuyPrice = 50, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1003, Name = "Arco Curto", Type = ItemType.Weapon, BaseAttack = 6, Destreza = 4, BuyPrice = 90, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1004, Name = "Arco de Caca", Type = ItemType.Weapon, BaseAttack = 9, Destreza = 5, BuyPrice = 130, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1005, Name = "Arco Reforcado", Type = ItemType.Weapon, BaseAttack = 12, Destreza = 6, BuyPrice = 170, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1006, Name = "Arco de Carvalho", Type = ItemType.Weapon, BaseAttack = 15, Destreza = 7, BuyPrice = 210, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1007, Name = "Arco Longo", Type = ItemType.Weapon, BaseAttack = 18, Destreza = 9, BuyPrice = 250, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1008, Name = "Arco Laminado", Type = ItemType.Weapon, BaseAttack = 22, Destreza = 11, BuyPrice = 290, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1009, Name = "Arco do Patrulheiro", Type = ItemType.Weapon, BaseAttack = 26, Destreza = 12, BuyPrice = 330, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1010, Name = "Arco de Guerra", Type = ItemType.Weapon, BaseAttack = 31, Destreza = 14, BuyPrice = 370, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1011, Name = "Arco Composto", Type = ItemType.Weapon, BaseAttack = 36, Destreza = 16, BuyPrice = 410, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1012, Name = "Arco de Precisao", Type = ItemType.Weapon, BaseAttack = 41, Destreza = 19, BuyPrice = 450, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1013, Name = "Arco de Aco", Type = ItemType.Weapon, BaseAttack = 46, Destreza = 21, BuyPrice = 490, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1014, Name = "Arco do Vigia", Type = ItemType.Weapon, BaseAttack = 52, Destreza = 24, BuyPrice = 530, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1015, Name = "Arco Nobre", Type = ItemType.Weapon, BaseAttack = 59, Destreza = 27, BuyPrice = 570, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1016, Name = "Arco Real", Type = ItemType.Weapon, BaseAttack = 66, Destreza = 30, BuyPrice = 610, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1017, Name = "Arco Imperial", Type = ItemType.Weapon, BaseAttack = 73, Destreza = 33, BuyPrice = 650, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1018, Name = "Arco Dragonico", Type = ItemType.Weapon, BaseAttack = 81, Destreza = 37, BuyPrice = 690, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1019, Name = "Arco Ancestral", Type = ItemType.Weapon, BaseAttack = 90, Destreza = 40, BuyPrice = 730, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1020, Name = "Arco Mitico", Type = ItemType.Weapon, BaseAttack = 99, Destreza = 44, BuyPrice = 770, AffixPool = new List<string>(bowPool.Split(',')) },
            new() { Id = 1021, Name = "Arco do Lendario Cacador", Type = ItemType.Weapon, BaseAttack = 109, Destreza = 49, BuyPrice = 810, AffixPool = new List<string>(bowPool.Split(',')) },

            // ALJAVAS (1051-1071)
            new() { Id = 1051, Name = "Aljava Simples", Type = ItemType.Shield, Destreza = 2, Agilidade = 1, BuyPrice = 14, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1052, Name = "Aljava de Couro", Type = ItemType.Shield, Destreza = 3, Agilidade = 2, BuyPrice = 38, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1053, Name = "Aljava Reforcada", Type = ItemType.Shield, Destreza = 4, Agilidade = 3, BuyPrice = 68, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1054, Name = "Aljava de Caca", Type = ItemType.Shield, Destreza = 5, Agilidade = 3, BuyPrice = 98, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1055, Name = "Aljava do Rastreador", Type = ItemType.Shield, Destreza = 6, Agilidade = 4, BuyPrice = 128, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1056, Name = "Aljava Balanceada", Type = ItemType.Shield, Destreza = 7, Agilidade = 5, BuyPrice = 158, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1057, Name = "Aljava Longa", Type = ItemType.Shield, Destreza = 9, Agilidade = 6, BuyPrice = 188, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1058, Name = "Aljava de Patrulha", Type = ItemType.Shield, Destreza = 11, Agilidade = 7, BuyPrice = 218, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1059, Name = "Aljava Militar", Type = ItemType.Shield, Destreza = 12, Agilidade = 9, BuyPrice = 248, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1060, Name = "Aljava de Guerra", Type = ItemType.Shield, Destreza = 14, Agilidade = 10, BuyPrice = 278, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1061, Name = "Aljava Composta", Type = ItemType.Shield, Destreza = 16, Agilidade = 11, BuyPrice = 308, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1062, Name = "Aljava de Precisao", Type = ItemType.Shield, Destreza = 19, Agilidade = 13, BuyPrice = 338, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1063, Name = "Aljava de Aco", Type = ItemType.Shield, Destreza = 21, Agilidade = 15, BuyPrice = 368, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1064, Name = "Aljava do Vigia", Type = ItemType.Shield, Destreza = 24, Agilidade = 17, BuyPrice = 398, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1065, Name = "Aljava Nobre", Type = ItemType.Shield, Destreza = 27, Agilidade = 19, BuyPrice = 428, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1066, Name = "Aljava Real", Type = ItemType.Shield, Destreza = 30, Agilidade = 21, BuyPrice = 458, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1067, Name = "Aljava Imperial", Type = ItemType.Shield, Destreza = 33, Agilidade = 24, BuyPrice = 488, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1068, Name = "Aljava Dragonica", Type = ItemType.Shield, Destreza = 37, Agilidade = 26, BuyPrice = 518, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1069, Name = "Aljava Ancestral", Type = ItemType.Shield, Destreza = 40, Agilidade = 29, BuyPrice = 548, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1070, Name = "Aljava Mitica", Type = ItemType.Shield, Destreza = 44, Agilidade = 32, BuyPrice = 578, AffixPool = new List<string>(aljavaPool.Split(',')) },
            new() { Id = 1071, Name = "Aljava do Lendario Cacador", Type = ItemType.Shield, Destreza = 49, Agilidade = 35, BuyPrice = 608, AffixPool = new List<string>(aljavaPool.Split(',')) },
        };

        foreach (var item in items)
            SaveItemDefinition(item);

        Logger.Info($"{items.Count} definições de item inseridas no banco.");
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
    public int Xp { get; set; }
    public int Forca { get; set; }
    public int Agilidade { get; set; }
    public int Destreza { get; set; }
    public int Inteligencia { get; set; }
    public float PosX { get; set; } = 1000f;
    public float PosY { get; set; } = 1000f;
    public int BankGold { get; set; }
    public int Gold { get; set; }
    public int StatPoints { get; set; }
}
