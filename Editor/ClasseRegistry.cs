using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ClasseRegistry : Node
{
    public const string PastaClasses = "res://Classes/";
    public const string PastaRacas = "res://Racas/";
    public const string PastaFaccoes = "res://Faccoes/";
    public const string PastaSprites = "res://Editor/SpritePresets/";

    public List<ClasseCustomResource> Classes { get; private set; } = new();
    public List<RacaResource> Racas { get; private set; } = new();
    public List<FaccaoResource> Faccoes { get; private set; } = new();
    public List<SpritePresetResource> Sprites { get; private set; } = new();
    public List<string> PrefixosAnimacaoAtaque { get; private set; } = new();

    public static readonly string[] ClassesOficiais =
    {
        "Mago", "Arqueiro", "Ladino", "Berseker", "Prist", "Guardiao"
    };

    public static readonly string[] RacasOficiais =
    {
        "Elfo", "Dark Elfo", "Morto Vivo", "Humano", "Orc", "Troll"
    };

    public static readonly string[] FaccoesOficiais = { "Solari", "Noctori" };

    private static readonly Dictionary<string, string[]> RacasPorFaccaoId = new()
    {
        [FaccaoUtil.IdSolari] = new[] { "Humano", "Elfo", "Troll" },
        [FaccaoUtil.IdNoctori] = new[] { "Dark Elfo", "Morto Vivo", "Orc" },
    };

    private static readonly Dictionary<string, string> RotulosAtaquePadrao = new()
    {
        ["mago"] = "Mago / Prist — magia ou sagrado",
        ["arqueiro"] = "Arqueiro — arco / flecha",
        ["guerreiro"] = "Berseker / Guardião — corpo a corpo",
        ["ladino"] = "Ladino — adagas rápidas",
    };

    public override void _Ready()
    {
    }

    public void RecarregarTudo()
    {
        Classes = CarregarRecursos<ClasseCustomResource>(PastaClasses)
            .OrderBy(c => c.NomeClasse)
            .ToList();
        Racas = CarregarRecursos<RacaResource>(PastaRacas)
            .OrderBy(r => r.NomeRaca)
            .ToList();
        Faccoes = CarregarRecursos<FaccaoResource>(PastaFaccoes)
            .OrderBy(f => f.NomeFaccao)
            .ToList();
        Sprites = CarregarRecursos<SpritePresetResource>(PastaSprites);

        if (Sprites.Count == 0)
            CriarSpritesPadraoDoPlayer();

        CarregarPrefixosAnimacaoAtaque();

        if (Racas.Count == 0)
            GD.Print("[CLASSE REGISTRY] ✘ Nenhuma raça em res://Racas/. Use o editor para criar.");

        ValidarConteudoPadrao();

        ValidarFaccoes();

        if (Faccoes.Count == 0)
        {
            GD.PrintErr("[CLASSE REGISTRY] ✘ Nenhuma facção carregada dos arquivos. Criando fallback hardcoded.");
            Faccoes = CriarFaccoesFallback();
        }

        GD.Print($"[CLASSE REGISTRY] {Classes.Count} classes, {Racas.Count} raças, {Faccoes.Count} facções, {Sprites.Count} sprites.");
    }

    private static List<FaccaoResource> CriarFaccoesFallback()
    {
        var solari = new FaccaoResource
        {
            IdFaccao = "solari",
            NomeFaccao = "Solari",
            Descricao = "Seguidores da luz de Aethor. Humanos, elfos e trolls defendem a honra, a esperança e Mithara sob o sol dourado. Sua capital é Helion, a Cidade do Sol Eterno.",
            CorTema = new Color(0.95f, 0.82f, 0.35f, 1f),
        };
        var noctori = new FaccaoResource
        {
            IdFaccao = "noctori",
            NomeFaccao = "Noctori",
            Descricao = "Filhos da sombra de Nyzareth. Dark elfos, mortos-vivos e orcs juraram lealdade ao Senhor do Abismo em Umbrath, o Reino da Lua Negra.",
            CorTema = new Color(0.55f, 0.32f, 0.78f, 1f),
        };
        return new List<FaccaoResource> { solari, noctori };
    }

    public List<RacaResource> ObterRacasDaFaccao(FaccaoResource faccao)
    {
        if (faccao == null) return new List<RacaResource>();
        return Racas.Where(r => r.Faccao != null && r.Faccao.MesmaFaccao(faccao)).ToList();
    }

    public FaccaoResource ObterFaccaoPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim().ToLowerInvariant();
        return Faccoes.FirstOrDefault(f => f.IdFaccao.Trim().ToLowerInvariant() == id);
    }

    public bool RacaPermitidaNaFaccao(RacaResource raca, FaccaoResource faccao)
    {
        if (raca == null || faccao == null) return false;
        if (raca.Faccao != null) return raca.Faccao.MesmaFaccao(faccao);

        if (!RacasPorFaccaoId.TryGetValue(faccao.IdFaccao.Trim().ToLowerInvariant(), out var nomes))
            return false;
        return System.Array.Exists(nomes, n => n == raca.NomeRaca);
    }

    private void ValidarFaccoes()
    {
        if (Faccoes.Count != FaccoesOficiais.Length)
            GD.Print($"[CLASSE REGISTRY] ✘ Esperado {FaccoesOficiais.Length} facções; encontrado {Faccoes.Count}.");

        foreach (var par in RacasPorFaccaoId)
        {
            var faccao = ObterFaccaoPorId(par.Key);
            if (faccao == null) continue;

            foreach (string nomeRaca in par.Value)
            {
                var raca = Racas.FirstOrDefault(r => r.NomeRaca == nomeRaca);
                if (raca == null)
                    GD.Print($"[CLASSE REGISTRY] ✘ Raça '{nomeRaca}' da facção {faccao.NomeFaccao} não encontrada.");
                else if (!RacaPermitidaNaFaccao(raca, faccao))
                    GD.Print($"[CLASSE REGISTRY] ✘ Raça '{nomeRaca}' não está vinculada à facção {faccao.NomeFaccao}.");
            }
        }
    }

    public void ValidarConteudoPadrao()
    {
        if (Classes.Count != ClassesOficiais.Length)
            GD.Print($"[CLASSE REGISTRY] ✘ Esperado {ClassesOficiais.Length} classes; encontrado {Classes.Count} em {PastaClasses}");
        if (Racas.Count != RacasOficiais.Length)
            GD.Print($"[CLASSE REGISTRY] ✘ Esperado {RacasOficiais.Length} raças; encontrado {Racas.Count} em {PastaRacas}");

        foreach (var raca in Racas)
        {
            if (raca.ObterSpritesheet() == null)
                GD.Print($"[CLASSE REGISTRY] ✘ Raça '{raca.NomeRaca}' sem spritesheet LPC.");
        }
    }

    /// <summary>Prefixo de animação de ataque recomendado para cada classe oficial.</summary>
    public static string ObterPrefixoAtaqueRecomendado(string nomeClasse)
    {
        return nomeClasse?.Trim().ToLowerInvariant() switch
        {
            "mago" => "mago",
            "arqueiro" => "arqueiro",
            "ladino" => "ladino",
            "berseker" or "berserker" => "guerreiro",
            "prist" or "priest" or "sacerdote" => "mago",
            "guardiao" or "guradiao" or "guardião" => "guerreiro",
            _ => "mago"
        };
    }

    /// <summary>Prefixo visual (walk/idle) recomendado para cada classe oficial.</summary>
    public static string ObterPrefixoSpriteRecomendado(string nomeClasse)
    {
        return nomeClasse?.Trim().ToLowerInvariant() switch
        {
            "mago" or "prist" or "priest" or "sacerdote" => "mago",
            "arqueiro" or "ladino" => "arqueiro",
            "berseker" or "berserker" or "guardiao" or "guradiao" or "guardião" => "guerreiro",
            _ => "mago"
        };
    }

    public string ObterRotuloAtaque(string prefixo)
    {
        if (string.IsNullOrWhiteSpace(prefixo)) return "(vazio)";
        prefixo = prefixo.Trim().ToLower();
        return RotulosAtaquePadrao.TryGetValue(prefixo, out var rotulo)
            ? rotulo
            : $"{char.ToUpper(prefixo[0])}{prefixo[1..]} — {prefixo}_attack_*";
    }

    public bool AnimacaoAtaqueExiste(string prefixo, SpriteFrames frames = null)
    {
        if (string.IsNullOrWhiteSpace(prefixo)) return false;
        frames ??= ObterSpriteFramesDoPlayer();
        if (frames == null) return false;

        prefixo = prefixo.Trim().ToLower();
        foreach (string dir in new[] { "down", "left", "right", "up" })
        {
            if (frames.HasAnimation($"{prefixo}_attack_{dir}"))
                return true;
            if (frames.HasAnimation($"{prefixo}_attack_{dir}_"))
                return true;
        }
        return false;
    }

    public SpriteFrames ObterSpriteFramesDoPlayer()
    {
        var playerScene = ResourceLoader.Load<PackedScene>("res://characters/Player/player.tscn");
        if (playerScene == null) return null;

        var instancia = playerScene.Instantiate<Node2D>();
        var sprite = instancia.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        SpriteFrames frames = sprite?.SpriteFrames;
        instancia.QueueFree();
        return frames;
    }

    private void CarregarPrefixosAnimacaoAtaque()
    {
        var encontrados = new HashSet<string>();
        foreach (var p in RotulosAtaquePadrao.Keys)
            encontrados.Add(p);

        var frames = ObterSpriteFramesDoPlayer();
        if (frames != null)
        {
            foreach (StringName anim in frames.GetAnimationNames())
            {
                string nome = anim.ToString();
                int idx = nome.IndexOf("_attack_");
                if (idx > 0)
                    encontrados.Add(nome.Substring(0, idx).ToLower());
            }
        }

        PrefixosAnimacaoAtaque = encontrados.OrderBy(p => p).ToList();
    }

    private static List<T> CarregarRecursos<T>(string pasta) where T : Resource
    {
        var lista = new List<T>();
        var dir = DirAccess.Open(pasta);
        if (dir == null)
        {
            GD.PrintErr($"[CLASSE REGISTRY] ✘ Pasta não encontrada: {pasta}");
            return lista;
        }

        dir.ListDirBegin();
        string nome = dir.GetNext();
        while (!string.IsNullOrEmpty(nome))
        {
            if (!dir.CurrentIsDir() && nome.EndsWith(".tres"))
            {
                string caminho = pasta + nome;
                if (!ResourceLoader.Exists(caminho))
                {
                    GD.PrintErr($"[CLASSE REGISTRY] Arquivo não encontrado: {caminho}");
                    nome = dir.GetNext();
                    continue;
                }

                var res = ResourceLoader.Load<T>(caminho);
                if (res != null)
                    lista.Add(res);
                else
                    GD.PrintErr($"[CLASSE REGISTRY] Falha ao carregar: {caminho} (dependência quebrada?)");
            }
            nome = dir.GetNext();
        }
        dir.ListDirEnd();
        return lista;
    }

    private void CriarSpritesPadraoDoPlayer()
    {
        string caminhoSheet = LpcSpriteFramesBuilder.PastaSpritesRaca + "Humano.png";
        if (!ResourceLoader.Exists(caminhoSheet)) return;

        var sheet = ResourceLoader.Load<Texture2D>(caminhoSheet);
        if (sheet == null) return;

        AdicionarSpritePreset("Mago", "mago", LpcSpriteFramesBuilder.Construir(sheet, "mago"), "Classe Mago");
        AdicionarSpritePreset("Arqueiro", "arqueiro", LpcSpriteFramesBuilder.Construir(sheet, "arqueiro"), "Classe Arqueiro");
        AdicionarSpritePreset("Ladino", "arqueiro", LpcSpriteFramesBuilder.Construir(sheet, "ladino"), "Visual arqueiro; ataque ladino no editor");
        AdicionarSpritePreset("Berseker", "guerreiro", LpcSpriteFramesBuilder.Construir(sheet, "guerreiro"), "Classe Berseker (corpo a corpo)");
        AdicionarSpritePreset("Prist", "mago", LpcSpriteFramesBuilder.Construir(sheet, "mago"), "Visual mago; ataque sagrado");
        AdicionarSpritePreset("Guardiao", "guerreiro", LpcSpriteFramesBuilder.Construir(sheet, "guerreiro"), "Classe Guardião (tanque)");
    }

    private void AdicionarSpritePreset(string nome, string prefixo, SpriteFrames frames, string descricao = null)
    {
        var preset = new SpritePresetResource
        {
            NomePreset = nome,
            PrefixoAnimacao = prefixo,
            SpriteFramesRecurso = frames,
            Descricao = descricao ?? $"Visual com animações walk/idle; ataques configurados no editor"
        };
        Sprites.Add(preset);
    }

    public bool SalvarClasse(ClasseCustomResource classe, string nomeArquivo = null)
    {
        if (classe == null) return false;

        nomeArquivo ??= SanitizarNomeArquivo(classe.NomeClasse) + ".tres";
        if (!nomeArquivo.EndsWith(".tres")) nomeArquivo += ".tres";

        string caminho = PastaClasses + nomeArquivo;
        var err = ResourceSaver.Save(classe, caminho);
        if (err == Error.Ok)
        {
            RecarregarTudo();
            GD.Print($"[CLASSE REGISTRY] ✓ Classe salva: {caminho}");
            return true;
        }

        GD.PrintErr($"[CLASSE REGISTRY] Erro ao salvar: {err}");
        return false;
    }

    public bool ExcluirClasse(ClasseCustomResource classe)
    {
        if (classe == null) return false;
        string caminho = classe.ResourcePath;
        if (string.IsNullOrEmpty(caminho)) return false;

        var err = DirAccess.RemoveAbsolute(caminho);
        if (err == Error.Ok)
        {
            RecarregarTudo();
            return true;
        }
        return false;
    }

    public static string SanitizarNomeArquivo(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "NovaClasse";
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            nome = nome.Replace(c, '_');
        return nome.Replace(" ", "");
    }
}
