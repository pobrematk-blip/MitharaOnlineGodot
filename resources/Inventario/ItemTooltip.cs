using Godot;
using System.Collections.Generic;

public partial class ItemTooltip : Panel
{
	private VBoxContainer _container;
	private Control _seletorCor;

	private static readonly Dictionary<Raridade, Color> RarityColors = new()
	{
		[Raridade.Comum] = Color.FromHtml("#ffffff"),
		[Raridade.Incomum] = Color.FromHtml("#1eff00"),
		[Raridade.Raro] = Color.FromHtml("#0070dd"),
		[Raridade.Epico] = Color.FromHtml("#a335ee"),
		[Raridade.Lendario] = Color.FromHtml("#ffcc00"),
		[Raridade.Mistico] = Color.FromHtml("#ff4444"),
	};

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 4096;
		Visible = false;

		AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.06f, 0.10f, 0.97f),
			BorderColor = new Color(0.25f, 0.25f, 0.35f, 1),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
		});

		var outerMargin = new MarginContainer();
		outerMargin.AddThemeConstantOverride("margin_left", 8);
		outerMargin.AddThemeConstantOverride("margin_right", 8);
		outerMargin.AddThemeConstantOverride("margin_top", 6);
		outerMargin.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(outerMargin);

		_container = new VBoxContainer();
		_container.AddThemeConstantOverride("separation", 0);
		_container.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		outerMargin.AddChild(_container);

		ProcessPriority = int.MaxValue;

		// Ensure tooltip draws on top of all other UI
		if (GetParent() is Control parent)
		{
			parent.MoveChild(this, parent.GetChildCount() - 1);
		}
	}

	public void Mostrar(ItemResource item, Vector2 posicaoGlobal, int refinoNivel = 0)
	{
		if (item == null) { Esconder(); return; }

		foreach (var child in _container.GetChildren())
			child.QueueFree();

		Color cor = RarityColors.GetValueOrDefault(item.Raridade, Colors.White);
		_seletorCor = new Control();
		_seletorCor.CustomMinimumSize = new Vector2(0, 3);
		_seletorCor.AddThemeColorOverride("theme_modulate", cor);
		var topBar = new StyleBoxFlat();
		topBar.BgColor = cor;
		_seletorCor.AddThemeStyleboxOverride("panel", topBar);
		_seletorCor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_container.AddChild(_seletorCor);

		AddHeader(item, cor);
		AddRefino(refinoNivel);
		AddSeparator();
		AddStatus(item);
		if (!string.IsNullOrEmpty(item.Descricao))
			AddDescricao(item);
		AddSeparator();
		AddClasses(item);
		AddSeparator();
		AddValor(item);

		_seletorCor = new Control();
		_seletorCor.CustomMinimumSize = new Vector2(0, 3);
		_seletorCor.AddThemeColorOverride("theme_modulate", cor);
		var botBar = new StyleBoxFlat();
		botBar.BgColor = cor;
		_seletorCor.AddThemeStyleboxOverride("panel", botBar);
		_seletorCor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_container.AddChild(_seletorCor);

		Visible = true;

		if (GetParent() is Control parentTooltip)
			parentTooltip.MoveChild(this, -1);

		var window = GetWindow();
		if (window == null) return;

		float cw = CustomMinimumSize.X > 0 ? Mathf.Max(CustomMinimumSize.X, 320) : 320;
		float ch = Mathf.Min(_container.GetCombinedMinimumSize().Y + 4, window.Size.Y * 0.65f);
		float mx = posicaoGlobal.X + 16;
		float my = posicaoGlobal.Y - ch - 16;

		if (my < 0)
			my = posicaoGlobal.Y + 16;
		if (mx + cw > window.Size.X)
			mx = posicaoGlobal.X - cw - 16;
		if (my + ch > window.Size.Y)
			my = window.Size.Y - ch - 8;
		if (mx < 0) mx = 0;
		if (my < 0) my = 0;

		Position = new Vector2(mx, my);
		CustomMinimumSize = new Vector2(cw, ch);
		Size = new Vector2(cw, ch);
	}

	public void MostrarComparacao(ItemResource itemNovo, ItemResource itemAntigo, Vector2 posicaoGlobal, int refinoNovo = 0, int refinoAntigo = 0)
	{
		if (itemNovo == null || itemAntigo == null) { Esconder(); return; }

		foreach (var child in _container.GetChildren())
			child.QueueFree();

		double multNovo = GetRefineMultiplier(refinoNovo);
		double multAntigo = GetRefineMultiplier(refinoAntigo);

		Color corNovo = RarityColors.GetValueOrDefault(itemNovo.Raridade, Colors.White);
		Color corAntigo = RarityColors.GetValueOrDefault(itemAntigo.Raridade, Colors.White);

		_seletorCor = new Control();
		_seletorCor.CustomMinimumSize = new Vector2(0, 3);
		_seletorCor.AddThemeColorOverride("theme_modulate", corNovo);
		var topBar = new StyleBoxFlat();
		topBar.BgColor = corNovo;
		_seletorCor.AddThemeStyleboxOverride("panel", topBar);
		_seletorCor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_container.AddChild(_seletorCor);

		AddCompareHeader("NOVO", itemNovo, corNovo, refinoNovo);
		AddCompareHeader("EQUIPADO", itemAntigo, corAntigo, refinoAntigo);
		AddSeparator();
		AddStatusComparado(itemNovo, itemAntigo, multNovo, multAntigo);
		AddSeparator();

		var hboxValor = new HBoxContainer();
		hboxValor.AddThemeConstantOverride("separation", 16);
		hboxValor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddValorNoContainer(itemNovo, hboxValor, true);
		AddValorNoContainer(itemAntigo, hboxValor, false);
		AddMargined(hboxValor, 8, 2);

		_seletorCor = new Control();
		_seletorCor.CustomMinimumSize = new Vector2(0, 3);
		_seletorCor.AddThemeColorOverride("theme_modulate", corNovo);
		var botBar2 = new StyleBoxFlat();
		botBar2.BgColor = corNovo;
		_seletorCor.AddThemeStyleboxOverride("panel", botBar2);
		_seletorCor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_container.AddChild(_seletorCor);

		Visible = true;

		if (GetParent() is Control parentTooltipComp)
			parentTooltipComp.MoveChild(this, -1);

		var window = GetWindow();
		if (window == null) return;

		float cw = Mathf.Max(CustomMinimumSize.X, 480);
		float ch = Mathf.Min(_container.GetCombinedMinimumSize().Y + 4, window.Size.Y * 0.65f);
		float mx = posicaoGlobal.X + 16;
		float my = posicaoGlobal.Y - ch - 16;

		if (my < 0)
			my = posicaoGlobal.Y + 16;
		if (mx + cw > window.Size.X)
			mx = posicaoGlobal.X - cw - 16;
		if (my + ch > window.Size.Y)
			my = window.Size.Y - ch - 8;
		if (mx < 0) mx = 0;
		if (my < 0) my = 0;

		Position = new Vector2(mx, my);
		CustomMinimumSize = new Vector2(cw, ch);
		Size = new Vector2(cw, ch);
	}

	private static double GetRefineMultiplier(int nivel)
	{
		return nivel switch
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
	}

	private void AddCompareHeader(string rotulo, ItemResource item, Color cor, int refinoNivel)
	{
		AddEspaco(2);
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 1);
		margin.AddThemeConstantOverride("margin_bottom", 1);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 6);

		if (item.Icone != null)
		{
			var icon = new TextureRect();
			icon.Texture = item.Icone;
			icon.CustomMinimumSize = new Vector2(32, 32);
			icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
			var iconBg = new StyleBoxFlat();
			iconBg.BgColor = new Color(0, 0, 0, 0.4f);
			iconBg.CornerRadiusTopLeft = 3;
			iconBg.CornerRadiusTopRight = 3;
			iconBg.CornerRadiusBottomRight = 3;
			iconBg.CornerRadiusBottomLeft = 3;
			icon.AddThemeStyleboxOverride("panel", iconBg);
			hbox.AddChild(icon);
		}

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);

		var labelRotulo = new Label();
		labelRotulo.Text = $"[{rotulo}]";
		labelRotulo.AddThemeColorOverride("font_color", new Color(0.5f, 0.55f, 0.7f));
		labelRotulo.AddThemeFontSizeOverride("font_size", 9);
		vbox.AddChild(labelRotulo);

		var nome = new Label();
		nome.Text = item.Nome.ToUpper();
		nome.AddThemeColorOverride("font_color", cor);
		nome.AddThemeFontSizeOverride("font_size", 12);
		nome.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nome.MaxLinesVisible = 2;
		vbox.AddChild(nome);

		if (refinoNivel > 0)
		{
			var refinoLb = new Label();
			refinoLb.Text = $"+{refinoNivel} Refinado";
			refinoLb.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0f));
			refinoLb.AddThemeFontSizeOverride("font_size", 10);
			vbox.AddChild(refinoLb);
		}

		hbox.AddChild(vbox);
		margin.AddChild(hbox);
		_container.AddChild(margin);
	}

	private struct StatComparavel
	{
		public string Nome;
		public float ValorNovo;
		public float ValorAntigo;
	}

	private List<StatComparavel> ObterStatsComparaveis(ItemResource itemNovo, ItemResource itemAntigo, double multNovo, double multAntigo)
	{
		var list = new List<StatComparavel>();

		void AddInt(string nome, int valNovo, int valAntigo)
		{
			float vn = (float)(valNovo * multNovo);
			float va = (float)(valAntigo * multAntigo);
			if (vn == 0 && va == 0) return;
			list.Add(new() { Nome = nome, ValorNovo = vn, ValorAntigo = va });
		}

		void AddFloat(string nome, float valNovo, float valAntigo)
		{
			float vn = (float)(valNovo * multNovo);
			float va = (float)(valAntigo * multAntigo);
			if (Mathf.Abs(vn) < 0.01f && Mathf.Abs(va) < 0.01f) return;
			list.Add(new() { Nome = nome, ValorNovo = vn, ValorAntigo = va });
		}

		AddInt("Força", itemNovo.Forca, itemAntigo.Forca);
		AddInt("Agilidade", itemNovo.Agilidade, itemAntigo.Agilidade);
		AddInt("Destreza", itemNovo.Destreza, itemAntigo.Destreza);
		AddInt("Inteligência", itemNovo.Inteligencia, itemAntigo.Inteligencia);
		AddInt("Proteção", itemNovo.DefesaFisica, itemAntigo.DefesaFisica);
		AddInt("Resist. Mágica", itemNovo.DefesaMagica, itemAntigo.DefesaMagica);
		AddInt("Dano Físico", itemNovo.DanoFisico, itemAntigo.DanoFisico);
		AddInt("Dano Mágico", itemNovo.DanoMagico, itemAntigo.DanoMagico);
		AddFloat("Crítico", itemNovo.ChanceCritica, itemAntigo.ChanceCritica);
		AddFloat("Evasão", itemNovo.Evasao, itemAntigo.Evasao);
		AddFloat("Dano Crítico", itemNovo.DanoCriticoBonus, itemAntigo.DanoCriticoBonus);
		AddFloat("Roubo de Vida", itemNovo.RouboVida, itemAntigo.RouboVida);
		AddFloat("Roubo de Mana", itemNovo.RouboMana, itemAntigo.RouboMana);
		AddFloat("Regen. Vida", itemNovo.RegeneracaoVida, itemAntigo.RegeneracaoVida);
		AddFloat("Regen. Mana", itemNovo.RegeneracaoMana, itemAntigo.RegeneracaoMana);
		AddInt("Vida", itemNovo.Hp, itemAntigo.Hp);
		AddInt("Mana", itemNovo.Mana, itemAntigo.Mana);
		AddInt("Stamina", itemNovo.Stamina, itemAntigo.Stamina);
		AddFloat("Vel. Movimento", itemNovo.VelocidadeMovimento, itemAntigo.VelocidadeMovimento);
		AddFloat("Vel. Ataque", itemNovo.VelocidadeAtaque, itemAntigo.VelocidadeAtaque);
		AddFloat("Precisão", itemNovo.Precisao, itemAntigo.Precisao);
		AddFloat("Tenacidade", itemNovo.Tenacidade, itemAntigo.Tenacidade);
		AddInt("Dano PvP", itemNovo.DanoPvp, itemAntigo.DanoPvp);
		AddInt("Defesa PvP", itemNovo.DefesaPvp, itemAntigo.DefesaPvp);
		AddInt("Pen. Armadura", itemNovo.PenetracaoArmadura, itemAntigo.PenetracaoArmadura);
		AddFloat("Red. Cooldown", itemNovo.ReducaoCooldown, itemAntigo.ReducaoCooldown);
		AddFloat("Bônus XP", itemNovo.BonusExperiencia, itemAntigo.BonusExperiencia);
		AddFloat("Chance Drop", itemNovo.ChanceDropAumentada, itemAntigo.ChanceDropAumentada);

		return list;
	}

	private void AddStatusComparado(ItemResource itemNovo, ItemResource itemAntigo, double multNovo, double multAntigo)
	{
		AddSecaoTitulo("COMPARAÇÃO DE ESTATÍSTICAS");

		var stats = ObterStatsComparaveis(itemNovo, itemAntigo, multNovo, multAntigo);
		if (stats.Count == 0)
		{
			var vazio = new Label();
			vazio.Text = "Nenhum atributo para comparar";
			vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
			vazio.AddThemeFontSizeOverride("font_size", 11);
			AddMargined(vazio, 8, 2);
			return;
		}

		// Col header
		var colHeader = new HBoxContainer();
		colHeader.AddThemeConstantOverride("separation", 8);

		var lbNovoHeader = new Label();
		lbNovoHeader.Text = "NOVO";
		lbNovoHeader.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.3f));
		lbNovoHeader.AddThemeFontSizeOverride("font_size", 9);
		colHeader.AddChild(lbNovoHeader);

		var lbNomeHeader = new Label();
		lbNomeHeader.Text = "ATRIBUTO";
		lbNomeHeader.AddThemeColorOverride("font_color", new Color(0.5f, 0.55f, 0.7f));
		lbNomeHeader.AddThemeFontSizeOverride("font_size", 9);
		lbNomeHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		colHeader.AddChild(lbNomeHeader);

		var lbEquipadoHeader = new Label();
		lbEquipadoHeader.Text = "EQUIPADO";
		lbEquipadoHeader.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.3f));
		lbEquipadoHeader.AddThemeFontSizeOverride("font_size", 9);
		colHeader.AddChild(lbEquipadoHeader);

		AddMargined(colHeader, 8, 1);

		foreach (var s in stats)
		{
			bool novoMelhor = s.ValorNovo > s.ValorAntigo;
			bool antigoMelhor = s.ValorAntigo > s.ValorNovo;

			Color corNovoStat = novoMelhor ? new Color(0.3f, 1.0f, 0.3f) : antigoMelhor ? new Color(1.0f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.8f);
			Color corAntigoStat = antigoMelhor ? new Color(0.3f, 1.0f, 0.3f) : novoMelhor ? new Color(1.0f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.8f);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			string txtNovo = s.ValorNovo == (int)s.ValorNovo ? $"+{(int)s.ValorNovo}" : $"+{s.ValorNovo:F1}";
			string txtAntigo = s.ValorAntigo == (int)s.ValorAntigo ? $"+{(int)s.ValorAntigo}" : $"+{s.ValorAntigo:F1}";

			var lbValNovo = new Label();
			lbValNovo.Text = txtNovo;
			lbValNovo.AddThemeColorOverride("font_color", corNovoStat);
			lbValNovo.AddThemeFontSizeOverride("font_size", 11);
			lbValNovo.CustomMinimumSize = new Vector2(70, 0);
			row.AddChild(lbValNovo);

			var lbNome = new Label();
			lbNome.Text = s.Nome;
			lbNome.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
			lbNome.AddThemeFontSizeOverride("font_size", 11);
			lbNome.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(lbNome);

			var lbValAntigo = new Label();
			lbValAntigo.Text = txtAntigo;
			lbValAntigo.AddThemeColorOverride("font_color", corAntigoStat);
			lbValAntigo.AddThemeFontSizeOverride("font_size", 11);
			lbValAntigo.CustomMinimumSize = new Vector2(70, 0);
			lbValAntigo.HorizontalAlignment = HorizontalAlignment.Right;
			row.AddChild(lbValAntigo);

			AddMargined(row, 8, 1);
		}
	}

	private void AddValorNoContainer(ItemResource item, HBoxContainer hbox, bool esquerda)
	{
		var label = new Label();
		label.Text = item.Valor > 0 ? $"VENDA: {item.Valor:N0} GOLD" : "Sem valor";
		label.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
		label.AddThemeFontSizeOverride("font_size", 11);
		if (!esquerda)
			label.HorizontalAlignment = HorizontalAlignment.Right;
		hbox.AddChild(label);
	}

	public void Esconder()
	{
		Visible = false;
	}

	private void AddHeader(ItemResource item, Color cor)
	{
		AddEspaco(4);
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 2);
		margin.AddThemeConstantOverride("margin_bottom", 2);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 8);

		if (item.Icone != null)
		{
			var icon = new TextureRect();
			icon.Texture = item.Icone;
			icon.CustomMinimumSize = new Vector2(48, 48);
			icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
			var iconBg = new StyleBoxFlat();
			iconBg.BgColor = new Color(0, 0, 0, 0.4f);
			iconBg.CornerRadiusTopLeft = 4;
			iconBg.CornerRadiusTopRight = 4;
			iconBg.CornerRadiusBottomRight = 4;
			iconBg.CornerRadiusBottomLeft = 4;
			icon.AddThemeStyleboxOverride("panel", iconBg);
			hbox.AddChild(icon);
		}

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 1);

		var nome = new Label();
		nome.Text = item.Nome.ToUpper();
		nome.AddThemeColorOverride("font_color", cor);
		nome.AddThemeFontSizeOverride("font_size", 15);
		nome.AddThemeConstantOverride("outline_size", 1);
		nome.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nome.MaxLinesVisible = 3;
		nome.CustomMinimumSize = new Vector2(140, 0);
		hbox.AddChild(vbox);

		var raridadeLabel = new Label();
		string raridadeNome = item.Raridade switch
		{
			Raridade.Comum => "Comum",
			Raridade.Incomum => "Incomum",
			Raridade.Raro => "Raro",
			Raridade.Epico => "Épico",
			Raridade.Lendario => "Lendário",
			Raridade.Mistico => "Místico",
			_ => "",
		};
		string categoriaNome = item.TipoItem == TipoItem.Elite ? "Elite" : "Normal";
		raridadeLabel.Text = $"{raridadeNome} ({categoriaNome}) | Nível {item.NivelRequerido}";
		raridadeLabel.AddThemeColorOverride("font_color", cor);
		raridadeLabel.AddThemeFontSizeOverride("font_size", 11);
		vbox.AddChild(nome);
		vbox.AddChild(raridadeLabel);

		string tipoTraduzido = TraduzirTipo(item.Tipo);
		if (!string.IsNullOrEmpty(tipoTraduzido))
		{
			string slotTraduzido = TraduzirSlot(item.Tipo);
			var tipoLabel = new Label();
			tipoLabel.Text = $"{tipoTraduzido} | {slotTraduzido}";
			tipoLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
			tipoLabel.AddThemeFontSizeOverride("font_size", 11);
			vbox.AddChild(tipoLabel);
		}

		string pesoNome = item.CategoriaPeso switch
		{
			PesoItem.Leve => "LEVE",
			PesoItem.Medio => "MÉDIO",
			PesoItem.Pesado => "PESADO",
			_ => "",
		};
		if (item.UsaCategoriaPeso() && !string.IsNullOrEmpty(pesoNome))
		{
			var pesoLabel = new Label();
			pesoLabel.Text = $"Categoria da Armadura: {pesoNome}";
			pesoLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
			pesoLabel.AddThemeFontSizeOverride("font_size", 10);
			vbox.AddChild(pesoLabel);
		}

		margin.AddChild(hbox);
		_container.AddChild(margin);
	}

	private void AddStatus(ItemResource item)
	{
		AddSecaoTitulo("ESTATÍSTICAS");

		bool temAlgo = false;

		temAlgo |= AddStat(item.Forca, "Força", "+{0}", new Color(0.9f, 0.6f, 0.3f));
		temAlgo |= AddStat(item.Agilidade, "Agilidade", "+{0}", new Color(0.3f, 0.8f, 0.3f));
		temAlgo |= AddStat(item.Destreza, "Destreza", "+{0}", new Color(0.3f, 0.6f, 0.9f));
		temAlgo |= AddStat(item.Inteligencia, "Inteligência", "+{0}", new Color(0.5f, 0.4f, 1.0f));

		temAlgo |= AddStat(item.DefesaFisica, "Proteção", "+{0}", new Color(0.6f, 0.9f, 0.6f));
		temAlgo |= AddStat(item.DefesaMagica, "Resist. Mágica", "+{0}", new Color(0.4f, 0.6f, 1.0f));
		temAlgo |= AddStat(item.DanoFisico, "Dano Físico", "+{0}", new Color(1.0f, 0.4f, 0.4f));
		temAlgo |= AddStat(item.DanoMagico, "Dano Mágico", "+{0}", new Color(0.4f, 0.4f, 1.0f));

		temAlgo |= AddStatF(item.ChanceCritica, "Crítico", "+{0:F1}", new Color(1.0f, 0.5f, 0.2f));
		temAlgo |= AddStatF(item.Evasao, "Evasão", "+{0:F1}", new Color(0.3f, 0.9f, 0.6f));
		temAlgo |= AddStatF(item.DanoCriticoBonus, "Dano Crítico", "x{0:F2}", new Color(1.0f, 0.6f, 0.0f));
		temAlgo |= AddStatF(item.RouboVida, "Roubo de Vida", "+{0:F1}%", new Color(1.0f, 0.3f, 0.3f));
		temAlgo |= AddStatF(item.RouboMana, "Roubo de Mana", "+{0:F1}%", new Color(0.3f, 0.3f, 1.0f));
		temAlgo |= AddStatF(item.RegeneracaoVida, "Regen. Vida", "+{0:F1}/s", new Color(0.4f, 1.0f, 0.4f));
		temAlgo |= AddStatF(item.RegeneracaoMana, "Regen. Mana", "+{0:F1}/s", new Color(0.4f, 0.4f, 1.0f));
		temAlgo |= AddStat(item.Hp, "Vida", "+{0}", new Color(0.5f, 1.0f, 0.5f));
		temAlgo |= AddStat(item.Mana, "Mana", "+{0}", new Color(0.4f, 0.4f, 1.0f));
		temAlgo |= AddStat(item.Stamina, "Stamina", "+{0}", new Color(0.6f, 1.0f, 0.6f));
		temAlgo |= AddStatF(item.VelocidadeMovimento, "Vel. Movimento", "x{0:F2}", new Color(0.5f, 1.0f, 0.5f));
		temAlgo |= AddStatF(item.VelocidadeAtaque, "Vel. Ataque", "x{0:F2}", new Color(1.0f, 0.5f, 0.5f));
		temAlgo |= AddStatF(item.Precisao, "Precisão", "+{0:F1}", new Color(0.5f, 0.8f, 1.0f));
		temAlgo |= AddStatF(item.Tenacidade, "Tenacidade", "+{0:F1}", new Color(0.8f, 0.5f, 1.0f));
		temAlgo |= AddStat(item.DanoPvp, "Dano PvP", "+{0}", new Color(1.0f, 0.3f, 0.3f));
		temAlgo |= AddStat(item.DefesaPvp, "Defesa PvP", "+{0}", new Color(0.3f, 0.8f, 0.8f));
		temAlgo |= AddStat(item.PenetracaoArmadura, "Pen. Armadura", "+{0}", new Color(1.0f, 0.5f, 0.2f));
		temAlgo |= AddStatF(item.ReducaoCooldown, "Red. Cooldown", "+{0:F1}%", new Color(0.4f, 0.7f, 1.0f));
		temAlgo |= AddStatF(item.BonusExperiencia, "Bônus XP", "+{0:F1}%", new Color(1.0f, 0.85f, 0.3f));
		temAlgo |= AddStatF(item.ChanceDropAumentada, "Chance Drop", "+{0:F1}%", new Color(1.0f, 0.8f, 0.3f));

		if (!temAlgo)
		{
			var vazio = new Label();
			vazio.Text = "Nenhum atributo adicional";
			vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
			vazio.AddThemeFontSizeOverride("font_size", 11);
			AddMargined(vazio, 8, 2);
		}
	}

	private void AddDescricao(ItemResource item)
	{
		if (string.IsNullOrEmpty(item.Descricao)) return;
		AddSecaoTitulo("DESCRIÇÃO");
		var desc = new Label();
		desc.Text = item.Descricao;
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.85f));
		desc.AddThemeFontSizeOverride("font_size", 11);
		AddMargined(desc, 8, 2);
	}

	private void AddCartas()
	{
	}

	private void AddRefino(int nivel)
	{
		if (nivel <= 0) return;

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 2);
		margin.AddThemeConstantOverride("margin_bottom", 2);

		var label = new Label();
		label.Text = $"+{nivel} Refinado";
		label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0f));
		label.AddThemeFontSizeOverride("font_size", 13);
		margin.AddChild(label);
		_container.AddChild(margin);
	}

	private void AddClasses(ItemResource item)
	{
		if (string.IsNullOrEmpty(item.ClassesPermitidas)) return;
		AddSecaoTitulo("CLASSES QUE PODEM USAR");
		var classes = new Label();
		classes.Text = item.ClassesPermitidas.Replace(",", "\n");
		classes.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
		classes.AddThemeFontSizeOverride("font_size", 12);
		AddMargined(classes, 8, 2);
	}

	private void AddValor(ItemResource item)
	{
		AddEspaco(2);
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 2);
		margin.AddThemeConstantOverride("margin_bottom", 4);

		var hbox = new HBoxContainer();
		var label = new Label();
		label.Text = item.Valor > 0 ? $"VENDA: {item.Valor:N0} GOLD" : "Sem valor de venda";
		label.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
		label.AddThemeFontSizeOverride("font_size", 11);
		hbox.AddChild(label);
		hbox.AddSpacer(true);
		margin.AddChild(hbox);
		_container.AddChild(margin);
	}

	private static string TraduzirTipo(TipoEquipamento tipo)
	{
		return tipo switch
		{
			TipoEquipamento.Capacete => "Capacete",
			TipoEquipamento.Peitoral => "Armadura",
			TipoEquipamento.Cinto => "Cinto",
			TipoEquipamento.Luvas => "Luvas",
			TipoEquipamento.Calca => "Calças",
			TipoEquipamento.Botas => "Botas",
			TipoEquipamento.Arma => "Arma",
			TipoEquipamento.Escudo => "Escudo",
			TipoEquipamento.Colar => "Colar",
			TipoEquipamento.Anel => "Anel",
			TipoEquipamento.Brinco => "Brinco",
			TipoEquipamento.Runa => "Runa",
			TipoEquipamento.Asa => "Asa",
			TipoEquipamento.Montaria => "Montaria",
			TipoEquipamento.Pet => "Pet",
			TipoEquipamento.Skin => "Skin",
			TipoEquipamento.Consumivel => "Consumível",
			TipoEquipamento.Moeda => "Moeda",
			TipoEquipamento.Feitico => "Feitiço",
			_ => "",
		};
	}

	private static string TraduzirSlot(TipoEquipamento tipo)
	{
		return tipo switch
		{
			TipoEquipamento.Capacete => "Cabeça",
			TipoEquipamento.Peitoral => "Torso",
			TipoEquipamento.Cinto => "Cintura",
			TipoEquipamento.Luvas => "Mãos",
			TipoEquipamento.Calca => "Pernas",
			TipoEquipamento.Botas => "Pés",
			TipoEquipamento.Arma => "Mão",
			TipoEquipamento.Escudo => "Mão Secundária",
			TipoEquipamento.Colar => "Pescoço",
			TipoEquipamento.Anel => "Dedos",
			TipoEquipamento.Brinco => "Orelhas",
			TipoEquipamento.Runa => "Runa",
			TipoEquipamento.Asa => "Costas",
			TipoEquipamento.Montaria => "Montaria",
			TipoEquipamento.Pet => "Pet",
			TipoEquipamento.Skin => "Skin",
			_ => "",
		};
	}

	private bool AddStat(int valor, string nome, string formato, Color cor)
	{
		if (valor == 0) return false;
		var label = new Label();
		label.Text = $"+{valor} {nome}";
		label.AddThemeColorOverride("font_color", cor);
		label.AddThemeFontSizeOverride("font_size", 11);
		AddMargined(label, 12, 1);
		return true;
	}

	private bool AddStatF(float valor, string nome, string formato, Color cor)
	{
		if (valor == 0) return false;
		var label = new Label();
		label.Text = $"+{valor:F1} {nome}";
		label.AddThemeColorOverride("font_color", cor);
		label.AddThemeFontSizeOverride("font_size", 11);
		AddMargined(label, 12, 1);
		return true;
	}

	private void AddSecaoTitulo(string titulo)
	{
		var label = new Label();
		label.Text = titulo;
		label.AddThemeColorOverride("font_color", new Color(0.5f, 0.55f, 0.7f));
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeConstantOverride("outline_size", 0);
		AddMargined(label, 8, 2);
	}

	private void AddSeparator()
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.2f, 0.2f, 0.3f, 1));
		sep.CustomMinimumSize = new Vector2(0, 1);
		_container.AddChild(sep);
	}

	private void AddMargined(Control child, int leftRight, int topBottom)
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", leftRight);
		margin.AddThemeConstantOverride("margin_right", leftRight);
		margin.AddThemeConstantOverride("margin_top", topBottom);
		margin.AddThemeConstantOverride("margin_bottom", topBottom);
		margin.AddChild(child);
		_container.AddChild(margin);
	}

	private void AddEspaco(int px)
	{
		var esp = new Control();
		esp.CustomMinimumSize = new Vector2(0, px);
		_container.AddChild(esp);
	}
}
