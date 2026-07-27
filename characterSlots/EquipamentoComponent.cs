using Godot;
using System;
using System.Collections.Generic;

public partial class EquipamentoComponent : Node
{
    [Signal] public delegate void EquipamentoAtualizadoEventHandler();

    // Dicionário que guarda qual item está em cada slot
    // Ex: <TipoEquipamento.Capacete, SlotLogico>
    public Dictionary<TipoEquipamento, SlotInventario> ItensEquipados = new Dictionary<TipoEquipamento, SlotInventario>();

    // Atributos Base
    private int _pontosDisponiveis = 10;
    private int _forca = 10;
    private int _agilidade = 10;
    private int _destreza = 10;
    private int _inteligencia = 10;
    private int _vitalidade = 5;
    private int _sorte = 5;

    // Bônus de equipamentos
    private int _bonusForca;
    private int _bonusAgilidade;
    private int _bonusDestreza;
    private int _bonusInteligencia;
    private int _bonusVitalidade;
    private int _bonusSorte;
    private int _bonusDanoFisico;
    private int _bonusDefesaFisica;
    private int _bonusHp;
    private int _bonusMana;
    private float _bonusVelocidadeMovimento;
    private float _bonusVelocidadeAtaque;
    private float _bonusTemporarioVelocidadeAtaque;
    private readonly Dictionary<string, float> _bonusTemporarioVelocidadeAtaquePorEfeito = new();
    private readonly Dictionary<string, float> _bonusTemporarioDanoFisicoPorEfeito = new();
    private readonly Dictionary<string, float> _bonusTemporarioRouboVidaPorEfeito = new();
    private readonly Dictionary<string, Timer> _bonusTemporarioTimers = new();
    private float _bonusChanceCritica;
    private float _bonusTemporarioChanceCritica;
    private float _bonusEvasao;
    private int _bonusDanoFisicoMin;
    private int _bonusDanoFisicoMax;
    private int _bonusDanoMagico;
    private int _bonusDanoMagicoMin;
    private int _bonusDanoMagicoMax;
    private int _bonusDefesaMagica;
    private float _bonusPrecisao;
    private float _bonusTemporarioPrecisao;
    private float _bonusTenacidade;
    private int _bonusDanoPvp;
    private int _bonusDefesaPvp;
    private int _bonusPenetracaoArmadura;
    private float _bonusRegeneracaoVida;
    private float _bonusRegeneracaoMana;
    private float _bonusRouboVida;
    private float _bonusRouboMana;
    private float _bonusReducaoCooldown;
    private float _bonusBonusExperiencia;
    private int _bonusStamina;
    private int _bonusReflexaoDano;
    private float _bonusResistenciaControle;
    private bool _temTotaisServidor;
    private int _forcaServidor;
    private int _agilidadeServidor;
    private int _destrezaServidor;
    private int _inteligenciaServidor;
    private int _vitalidadeServidor = 5;
    private int _sorteServidor = 5;
    private int _defesaFisicaServidor;
    private int _defesaMagicaServidor;
    private float _chanceCriticaServidor;
    private float _danoCriticoServidor = 1.5f;
    private float _evasaoServidor;
    private float _velocidadeMovimentoServidor = 1f;
    private float _velocidadeAtaqueServidor = 1f;
    private float _precisaoServidor = 75f;
    private float _tenacidadeServidor;
    private float _penetracaoArmaduraServidor;
    private float _regeneracaoVidaServidor;
    private float _regeneracaoManaServidor;
    private float _rouboVidaServidor;
    private float _rouboManaServidor;
    private float _reducaoCooldownServidor;
    private int _danoPvpServidor;
    private int _defesaPvpServidor;
    private float _bonusExperienciaServidor;
    private float _reflexaoDanoServidor;
    private float _resistenciaControleServidor;
    private int _danoFisicoMinServidor;
    private int _danoFisicoMaxServidor;
    private int _danoMagicoMinServidor;
    private int _danoMagicoMaxServidor;

    public int PontosDisponiveis => _pontosDisponiveis;
    public int Forca => _temTotaisServidor ? _forcaServidor : _forca + _bonusForca;
    public int Agilidade => _temTotaisServidor ? _agilidadeServidor : _agilidade + _bonusAgilidade;
    public int Destreza => _temTotaisServidor ? _destrezaServidor : _destreza + _bonusDestreza;
    public int Inteligencia => _temTotaisServidor ? _inteligenciaServidor : _inteligencia + _bonusInteligencia;
    public int Vitalidade => _temTotaisServidor ? _vitalidadeServidor : _vitalidade + _bonusVitalidade;
    public int Sorte => _temTotaisServidor ? _sorteServidor : _sorte + _bonusSorte;

    // ============ STATUS DERIVADOS ============
    // Dano Físico com variação (por Força) - Base 8-12, aumenta com Força
    private int AtributoOfensivoFisico
    {
        get
        {
            var player = GetParent() as Player;
            string classe = player?.NomeDaClasse?.ToLowerInvariant() ?? "";
            return classe is "arqueiro" or "ladino" or "assassino" ? Destreza : Forca;
        }
    }
    private float BonusTemporarioDanoFisicoPercentual => SomarBonusTemporarios(_bonusTemporarioDanoFisicoPorEfeito);
    private float BonusTemporarioVelocidadeAtaquePercentual => MathF.Max(_bonusTemporarioVelocidadeAtaque, SomarBonusTemporarios(_bonusTemporarioVelocidadeAtaquePorEfeito));
    private float BonusTemporarioRouboVida => SomarBonusTemporarios(_bonusTemporarioRouboVidaPorEfeito);
    public int DanoFisicoMin => _temTotaisServidor && _danoFisicoMinServidor > 0 ? _danoFisicoMinServidor : AplicarBonusPercentual(8 + (AtributoOfensivoFisico / 2) + (Sorte / 10) + _bonusDanoFisico + _bonusDanoFisicoMin, BonusTemporarioDanoFisicoPercentual);
    public int DanoFisicoMax => _temTotaisServidor && _danoFisicoMaxServidor > 0 ? _danoFisicoMaxServidor : AplicarBonusPercentual(12 + (AtributoOfensivoFisico / 2) + (Sorte / 10) + _bonusDanoFisico + _bonusDanoFisicoMax, BonusTemporarioDanoFisicoPercentual);
    public string DanoFisico => $"{DanoFisicoMin}-{DanoFisicoMax}";
    
    // Dano Mágico com variação (por Inteligência) - Base 8-12, aumenta com Inteligência
    public int DanoMagicoMin => _temTotaisServidor && _danoMagicoMinServidor > 0 ? _danoMagicoMinServidor : 8 + (Inteligencia / 2) + (Sorte / 10) + _bonusDanoMagico + _bonusDanoMagicoMin;
    public int DanoMagicoMax => _temTotaisServidor && _danoMagicoMaxServidor > 0 ? _danoMagicoMaxServidor : 12 + (Inteligencia / 2) + (Sorte / 10) + _bonusDanoMagico + _bonusDanoMagicoMax;
    public string DanoMagico => $"{DanoMagicoMin}-{DanoMagicoMax}";
    
    // HP (por Vitalidade + bonus de itens)
    public int Hp => 80 + (Vitalidade * 8) + _bonusHp;
    
    // Mana (por Inteligência + bônus de itens)
    public int Mana => 30 + (Inteligencia * 5) + _bonusMana;
    
    public float ChanceCritica => _temTotaisServidor ? _chanceCriticaServidor : MathF.Min(60f, (Sorte * 0.20f) + _bonusChanceCritica + _bonusTemporarioChanceCritica);
    public float Evasao => _temTotaisServidor ? _evasaoServidor : MathF.Min(45f, (Agilidade * 0.22f) + (Sorte * 0.03f) + _bonusEvasao);
    
    // Dano Crítico - Base 1.5x, bônus máximo +100% (cap 1.0f)
    private float _bonusDanoCritico = 0f;
    public float DanoCritico => _temTotaisServidor ? _danoCriticoServidor : 1.5f + MathF.Min(1.0f, _bonusDanoCritico / 100f);
    
    // Velocidades (por Agilidade + bônus de itens)
    public float VelocidadeMovimento => _temTotaisServidor ? _velocidadeMovimentoServidor : MathF.Min(1.3f, 1.0f + (_bonusVelocidadeMovimento / 100f));
    private float VelocidadeAtaqueBase => MathF.Min(2.0f, 1.0f + (Agilidade * 0.012f) + (Destreza * 0.006f) + (_bonusVelocidadeAtaque / 100f));
    public float VelocidadeAtaque => MathF.Min(2.5f, (_temTotaisServidor ? _velocidadeAtaqueServidor : VelocidadeAtaqueBase) * (1.0f + BonusTemporarioVelocidadeAtaquePercentual));
    
    // Defesas
    public int DefesaFisica => _temTotaisServidor ? _defesaFisicaServidor : (Vitalidade / 2) + _bonusDefesaFisica;
    public int DefesaMagica => _temTotaisServidor ? _defesaMagicaServidor : (Vitalidade / 4) + (Inteligencia / 3) + _bonusDefesaMagica;
    
    // Limites globais definidos no catálogo de armaduras.
    public float Precisao => _temTotaisServidor ? _precisaoServidor : MathF.Min(98f, 75f + (Destreza * 0.25f) + (Sorte * 0.02f) + _bonusPrecisao + _bonusTemporarioPrecisao);
    public float Tenacidade => _temTotaisServidor ? _tenacidadeServidor : MathF.Min(75f, (Vitalidade * 0.06f) + _bonusTenacidade);
    
    // PvP
    public int DanoPvp => _temTotaisServidor ? _danoPvpServidor : _bonusDanoPvp;
    public int DefesaPvp => _temTotaisServidor ? _defesaPvpServidor : _bonusDefesaPvp;
    
    // Penetração cap 60, Reflexão cap 25
    public float PenetracaoArmadura => _temTotaisServidor ? _penetracaoArmaduraServidor : MathF.Min(60f, _bonusPenetracaoArmadura);
    public float ReflexaoDano => _temTotaisServidor ? _reflexaoDanoServidor : MathF.Min(25f, _bonusReflexaoDano);
    
    // Regeneração
    public float RegeneracaoVida => _temTotaisServidor ? _regeneracaoVidaServidor : _bonusRegeneracaoVida;
    public float RegeneracaoMana => _temTotaisServidor ? _regeneracaoManaServidor : _bonusRegeneracaoMana;
    
    // Roubo
    public float RouboVida => (_temTotaisServidor ? _rouboVidaServidor : MathF.Min(15f, _bonusRouboVida)) + BonusTemporarioRouboVida;
    public float RouboMana => _temTotaisServidor ? _rouboManaServidor : MathF.Min(15f, _bonusRouboMana);
    
    // Redução e Bônus
    public float ReducaoCooldown => _temTotaisServidor ? _reducaoCooldownServidor : MathF.Min(40f, _bonusReducaoCooldown);
    public float BonusExperiencia => _temTotaisServidor ? _bonusExperienciaServidor : _bonusBonusExperiencia;
    public int Stamina => 100 + (Agilidade * 2) + _bonusStamina;
    public float ResistenciaControle => _temTotaisServidor ? _resistenciaControleServidor : MathF.Min(50f, _bonusResistenciaControle);

    public override void _Ready()
    {
        // Inicializamos todos os slots possíveis como vazios
        foreach (TipoEquipamento tipo in Enum.GetValues(typeof(TipoEquipamento)))
        {
            if (tipo == TipoEquipamento.Nenhum) continue;
            ItensEquipados[tipo] = new SlotInventario();
        }

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnStatUpdate += OnStatUpdate;
            net.OnStatusEffect += OnStatusEffect;
        }
    }

    public override void _ExitTree()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnStatUpdate -= OnStatUpdate;
            net.OnStatusEffect -= OnStatusEffect;
        }

        foreach (var timer in _bonusTemporarioTimers.Values)
        {
            if (IsInstanceValid(timer))
                timer.QueueFree();
        }
        _bonusTemporarioTimers.Clear();
    }

    private void OnStatUpdate(int baseForca, int baseAgilidade, int baseDestreza, int baseInteligencia,
        int statPoints, int totalForca, int totalAgilidade, int totalDestreza, int totalInteligencia,
        int maxHealth, int maxMana, int defesaFisica, int defesaMagica, float chanceCritica, float danoCritico,
        float evasao, float velocidadeMovimento, float velocidadeAtaque, float precisao, float tenacidade, float penetracaoArmadura,
        float regeneracaoVida, float regeneracaoMana, float rouboVida, float rouboMana, float reducaoCooldown,
        int danoPvp, int defesaPvp, float bonusExperiencia, float reflexaoDano, float resistenciaControle,
        int baseVitalidade, int baseSorte, int totalVitalidade, int totalSorte,
        int danoFisicoMin, int danoFisicoMax, int danoMagicoMin, int danoMagicoMax)
    {
        _forca = baseForca;
        _agilidade = baseAgilidade;
        _destreza = baseDestreza;
        _inteligencia = baseInteligencia;
        _vitalidade = baseVitalidade;
        _sorte = baseSorte;
        _pontosDisponiveis = statPoints;
        _forcaServidor = totalForca;
        _agilidadeServidor = totalAgilidade;
        _destrezaServidor = totalDestreza;
        _inteligenciaServidor = totalInteligencia;
        _vitalidadeServidor = totalVitalidade;
        _sorteServidor = totalSorte;
        _defesaFisicaServidor = defesaFisica;
        _defesaMagicaServidor = defesaMagica;
        _chanceCriticaServidor = chanceCritica;
        _danoCriticoServidor = danoCritico;
        _evasaoServidor = evasao;
        _velocidadeMovimentoServidor = velocidadeMovimento;
        _velocidadeAtaqueServidor = velocidadeAtaque;
        _precisaoServidor = precisao;
        _tenacidadeServidor = tenacidade;
        _penetracaoArmaduraServidor = penetracaoArmadura;
        _regeneracaoVidaServidor = regeneracaoVida;
        _regeneracaoManaServidor = regeneracaoMana;
        _rouboVidaServidor = rouboVida;
        _rouboManaServidor = rouboMana;
        _reducaoCooldownServidor = reducaoCooldown;
        _danoPvpServidor = danoPvp;
        _defesaPvpServidor = defesaPvp;
        _bonusExperienciaServidor = bonusExperiencia;
        _reflexaoDanoServidor = reflexaoDano;
        _resistenciaControleServidor = resistenciaControle;
        _danoFisicoMinServidor = danoFisicoMin;
        _danoFisicoMaxServidor = danoFisicoMax;
        _danoMagicoMinServidor = danoMagicoMin;
        _danoMagicoMaxServidor = danoMagicoMax;
        _temTotaisServidor = true;

        var player = GetParent() as Player ?? GetTree().CurrentScene?.FindChild("Player", true, false) as Player;
        if (player != null)
        {
            player.SetHealthFromServer(player.CurrentHealth, maxHealth);
            player.SetManaFromServer(player.CurrentMana, maxMana);
        }

        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void Equipar(TipoEquipamento slot, SlotInventario slotVindoDoInventario)
    {
        ItemResource itemParaEquipar = slotVindoDoInventario.Item;
        ItemResource itemAntigo = ItensEquipados[slot].Item;

        ItensEquipados[slot].Item = itemParaEquipar;
        ItensEquipados[slot].Quantidade = 1;

        slotVindoDoInventario.Item = itemAntigo;
        slotVindoDoInventario.Quantidade = (itemAntigo != null) ? 1 : 0;

        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
        GD.Print($"Equipado {itemParaEquipar.Nome} no slot {slot}");
    }

    public void Desequipar(TipoEquipamento slot, InventarioComponent inventario)
    {
        ItemResource itemRemovido = ItensEquipados[slot].Item;
        if (itemRemovido == null) return;

        if (inventario.AdicionarItem(itemRemovido, 1))
        {
            ItensEquipados[slot].Item = null;
            ItensEquipados[slot].Quantidade = 0;
            RecalcularBonusEquipamentos();
            EmitSignal(SignalName.EquipamentoAtualizado);
        }
    }

    // Método para obter um slot específico
    public SlotInventario ObterSlot(TipoEquipamento tipo)
    {
        if (ItensEquipados.ContainsKey(tipo))
        {
            return ItensEquipados[tipo];
        }
        return null;
    }

    public int CalcularDanoFisicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoFisicoMax - DanoFisicoMin + 1)) + DanoFisicoMin;
    }

    public int CalcularDanoMagicoAleatorio()
    {
        return (int)(GD.Randi() % (DanoMagicoMax - DanoMagicoMin + 1)) + DanoMagicoMin;
    }

    public void RecalcularBonusEquipamentos()
    {
        _bonusForca = 0;
        _bonusAgilidade = 0;
        _bonusDestreza = 0;
        _bonusInteligencia = 0;
        _bonusVitalidade = 0;
        _bonusSorte = 0;
        _bonusDanoFisico = 0;
        _bonusDanoFisicoMin = 0;
        _bonusDanoFisicoMax = 0;
        _bonusDanoMagico = 0;
        _bonusDanoMagicoMin = 0;
        _bonusDanoMagicoMax = 0;
        _bonusDefesaFisica = 0;
        _bonusDefesaMagica = 0;
        _bonusHp = 0;
        _bonusMana = 0;
        _bonusStamina = 0;
        _bonusVelocidadeMovimento = 0f;
        _bonusVelocidadeAtaque = 0f;
        _bonusChanceCritica = 0f;
        _bonusEvasao = 0f;
        _bonusDanoCritico = 0f;
        _bonusPrecisao = 0f;
        _bonusTenacidade = 0f;
        _bonusDanoPvp = 0;
        _bonusDefesaPvp = 0;
        _bonusPenetracaoArmadura = 0;
        _bonusRegeneracaoVida = 0f;
        _bonusRegeneracaoMana = 0f;
        _bonusRouboVida = 0f;
        _bonusRouboMana = 0f;
        _bonusReducaoCooldown = 0f;
        _bonusBonusExperiencia = 0f;
        _bonusReflexaoDano = 0;
        _bonusResistenciaControle = 0f;

        foreach (var kv in ItensEquipados)
        {
            var item = kv.Value?.Item;
            if (item == null) continue;

            double refineMult = kv.Value.RefinoNivel switch
            {
                1 => 1.02,
                2 => 1.04,
                3 => 1.06,
                4 => 1.08,
                5 => 1.10,
                6 => 1.13,
                7 => 1.16,
                8 => 1.20,
                9 => 1.25,
                10 => 1.30,
                _ => 1.0,
            };

            int refineLevel = kv.Value.RefinoNivel;
            _bonusForca += RefinedInt(item.Forca, refineLevel, refineMult);
            _bonusAgilidade += RefinedInt(item.Agilidade, refineLevel, refineMult);
            _bonusDestreza += RefinedInt(item.Destreza, refineLevel, refineMult);
            _bonusInteligencia += RefinedInt(item.Inteligencia, refineLevel, refineMult);
            _bonusDanoFisico += RefinedInt(item.DanoFisico, refineLevel, refineMult);
            _bonusDanoFisicoMin += RefinedInt(item.DanoFisicoMin, refineLevel, refineMult);
            _bonusDanoFisicoMax += RefinedInt(item.DanoFisicoMax, refineLevel, refineMult);
            _bonusDanoMagico += RefinedInt(item.DanoMagico, refineLevel, refineMult);
            _bonusDanoMagicoMin += RefinedInt(item.DanoMagicoMin, refineLevel, refineMult);
            _bonusDanoMagicoMax += RefinedInt(item.DanoMagicoMax, refineLevel, refineMult);
            _bonusDefesaFisica += RefinedInt(item.DefesaFisica, refineLevel, refineMult);
            _bonusDefesaMagica += RefinedInt(item.DefesaMagica, refineLevel, refineMult);
            _bonusHp += RefinedInt(item.Hp, refineLevel, refineMult);
            _bonusMana += RefinedInt(item.Mana, refineLevel, refineMult);
            _bonusStamina += RefinedInt(item.Stamina, refineLevel, refineMult);
            _bonusVelocidadeMovimento += (float)(item.VelocidadeMovimento * refineMult);
            _bonusVelocidadeAtaque += (float)(item.VelocidadeAtaque * refineMult);
            _bonusChanceCritica += (float)(item.ChanceCritica * refineMult);
            _bonusEvasao += (float)(item.Evasao * refineMult);
            _bonusDanoCritico += (float)(item.DanoCriticoBonus * refineMult);
            _bonusPrecisao += (float)(item.Precisao * refineMult);
            _bonusTenacidade += (float)(item.Tenacidade * refineMult);
            _bonusDanoPvp += RefinedInt(item.DanoPvp, refineLevel, refineMult);
            _bonusDefesaPvp += RefinedInt(item.DefesaPvp, refineLevel, refineMult);
            _bonusPenetracaoArmadura += RefinedInt(item.PenetracaoArmadura, refineLevel, refineMult);
            _bonusRegeneracaoVida += (float)(item.RegeneracaoVida * refineMult);
            _bonusRegeneracaoMana += (float)(item.RegeneracaoMana * refineMult);
            _bonusRouboVida += (float)(item.RouboVida * refineMult);
            _bonusRouboMana += (float)(item.RouboMana * refineMult);
            _bonusReducaoCooldown += (float)(item.ReducaoCooldown * refineMult);
            _bonusBonusExperiencia += (float)(item.BonusExperiencia * refineMult);
            _bonusReflexaoDano += RefinedInt(item.ReflexaoDano, refineLevel, refineMult);
            _bonusResistenciaControle += (float)(item.ResistenciaControle * refineMult);
        }
    }

    private static int RefinedInt(int value, int refineLevel, double multiplier)
    {
        if (value <= 0)
            return 0;

        if (refineLevel <= 0)
            return value;

        return value + refineLevel;
    }

    public void AdicionarBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico += bonusPercentual;
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void RemoverBonusDanoCritico(float bonusPercentual)
    {
        _bonusDanoCritico -= bonusPercentual;
        _bonusDanoCritico = Mathf.Max(_bonusDanoCritico, 0f);
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void SetBonusTemporarioMiraApurada(float precisao, float chanceCritica)
    {
        _bonusTemporarioPrecisao = MathF.Max(0f, precisao);
        _bonusTemporarioChanceCritica = MathF.Max(0f, chanceCritica);
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void SetBonusTemporarioVelocidadeAtaque(float velocidadeAtaque)
    {
        _bonusTemporarioVelocidadeAtaque = MathF.Max(0f, velocidadeAtaque);
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    private void OnStatusEffect(string effectId, string displayName, bool isDebuff, float duration, int power, string iconPath)
    {
        if (isDebuff || duration <= 0f || string.IsNullOrWhiteSpace(effectId))
            return;

        switch (effectId.ToLowerInvariant())
        {
            case "furia_berserker":
                AplicarBonusTemporario(_bonusTemporarioDanoFisicoPorEfeito, effectId, 0.25f, duration);
                break;
            case "frenesi_berserker":
                AplicarBonusTemporario(_bonusTemporarioVelocidadeAtaquePorEfeito, effectId, 0.35f, duration);
                break;
            case "forca_brutal":
                AplicarBonusTemporario(_bonusTemporarioDanoFisicoPorEfeito, effectId, 0.35f, duration);
                break;
            case "berserk":
                AplicarBonusTemporario(_bonusTemporarioDanoFisicoPorEfeito, effectId, 0.30f, duration);
                AplicarBonusTemporario(_bonusTemporarioVelocidadeAtaquePorEfeito, effectId, 0.30f, duration);
                break;
            case "deus_da_guerra":
                AplicarBonusTemporario(_bonusTemporarioDanoFisicoPorEfeito, effectId, 0.50f, duration);
                AplicarBonusTemporario(_bonusTemporarioVelocidadeAtaquePorEfeito, effectId, 0.50f, duration);
                AplicarBonusTemporario(_bonusTemporarioRouboVidaPorEfeito, effectId, 15f, duration);
                break;
            case "sede_de_sangue":
                AplicarBonusTemporario(_bonusTemporarioRouboVidaPorEfeito, effectId, 15f, duration);
                break;
        }
    }

    private void AplicarBonusTemporario(Dictionary<string, float> bonusPorEfeito, string effectId, float valor, float duration)
    {
        bonusPorEfeito[effectId] = MathF.Max(0f, valor);

        if (_bonusTemporarioTimers.TryGetValue(effectId, out var timerAntigo) && IsInstanceValid(timerAntigo))
            timerAntigo.QueueFree();

        var timer = new Timer { OneShot = true, WaitTime = duration };
        _bonusTemporarioTimers[effectId] = timer;
        AddChild(timer);
        timer.Timeout += () =>
        {
            _bonusTemporarioDanoFisicoPorEfeito.Remove(effectId);
            _bonusTemporarioVelocidadeAtaquePorEfeito.Remove(effectId);
            _bonusTemporarioRouboVidaPorEfeito.Remove(effectId);
            _bonusTemporarioTimers.Remove(effectId);
            EmitSignal(SignalName.EquipamentoAtualizado);
            if (IsInstanceValid(timer))
                timer.QueueFree();
        };
        timer.Start();

        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    private static float SomarBonusTemporarios(Dictionary<string, float> bonusPorEfeito)
    {
        float total = 0f;
        foreach (float value in bonusPorEfeito.Values)
            total += value;
        return total;
    }

    private static int AplicarBonusPercentual(int valorBase, float percentual)
    {
        if (percentual <= 0f)
            return valorBase;
        return Math.Max(1, (int)MathF.Round(valorBase * (1f + percentual)));
    }

    public void AdicionarPontoForca()
    {
        RequestAllocateStat("forca");
    }

    public void AdicionarPontoAgilidade()
    {
        RequestAllocateStat("agilidade");
    }

    public void AdicionarPontoDestreza()
    {
        RequestAllocateStat("destreza");
    }

    public void AdicionarPontoInteligencia()
    {
        RequestAllocateStat("inteligencia");
    }

    public void AdicionarPontoVitalidade()
    {
        RequestAllocateStat("vitalidade");
    }

    public void AdicionarPontoSorte()
    {
        RequestAllocateStat("sorte");
    }

    private void RequestAllocateStat(string statName)
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            gameNet.SendAllocateStat(statName);
            return;
        }

        GD.PrintErr("[EQUIPAMENTO] Alocacao de atributo local bloqueada. Use o servidor.");
    }

    public void AdicionarPontosDisponiveis(int qtd)
    {
        _pontosDisponiveis += qtd;
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public static bool PodeEquipar(ItemResource item, string nomeClasse)
    {
        if (item == null) return true;
        if (string.IsNullOrWhiteSpace(item.ClassesPermitidas)) return true;
        var classes = item.ClassesPermitidas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var c in classes)
        {
            if (string.Equals(c, nomeClasse, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void ImportarEstado(int forca, int agilidade, int destreza, int inteligencia, int pontosDisponiveis, int vitalidade = 5, int sorte = 5)
    {
        _forca = forca;
        _agilidade = agilidade;
        _destreza = destreza;
        _inteligencia = inteligencia;
        _vitalidade = vitalidade;
        _sorte = sorte;
        _pontosDisponiveis = pontosDisponiveis;
        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public void ImportarEquipamentos(System.Collections.Generic.Dictionary<TipoEquipamento, int> equipados, ItemDatabase itemDB)
    {
        if (equipados == null || itemDB == null) return;
        foreach (var kv in equipados)
        {
            if (kv.Value <= 0) continue;
            var item = itemDB.GetItem(kv.Value);
            if (item == null) continue;

            ItensEquipados[kv.Key].Item = item;
            ItensEquipados[kv.Key].Quantidade = 1;
        }
        RecalcularBonusEquipamentos();
        EmitSignal(SignalName.EquipamentoAtualizado);
    }

    public static void PreencherPreview(ClasseCustomResource classe, out int forca, out int agilidade, out int destreza, out int inteligencia)
    {
        forca = classe.Forca;
        agilidade = classe.Agilidade;
        destreza = classe.Destreza;
        inteligencia = classe.Inteligencia;

        if (classe.Raca != null)
        {
            forca += classe.Raca.BonusForca;
            agilidade += classe.Raca.BonusAgilidade;
            destreza += classe.Raca.BonusDestreza;
            inteligencia += classe.Raca.BonusInteligencia;
        }
    }
}
