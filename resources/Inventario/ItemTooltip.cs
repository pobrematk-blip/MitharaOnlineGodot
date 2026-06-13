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

		_container = new VBoxContainer();
		_container.AddThemeConstantOverride("separation", 0);
		AddChild(_container);

		ProcessPriority = int.MaxValue;
	}

	public void Mostrar(ItemResource item, Vector2 posicaoGlobal)
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
		AddSeparator();
		AddStatus(item);
		AddSeparator();
		AddDescricao(item);
		AddSeparator();
		AddCartas();
		AddSeparator();
		AddRefino();
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

		var window = GetWindow();
		if (window == null) return;

		float cw = CustomMinimumSize.X > 0 ? CustomMinimumSize.X : 240;
		float ch = _container.Size.Y + 16;
		float mx = posicaoGlobal.X + 16;
		float my = posicaoGlobal.Y;

		if (mx + cw > window.Size.X)
			mx = posicaoGlobal.X - cw - 16;
		if (my + ch > window.Size.Y)
			my = window.Size.Y - ch - 8;
		if (mx < 0) mx = 0;
		if (my < 0) my = 0;

		Position = new Vector2(mx, my);
		CustomMinimumSize = new Vector2(cw, 0);
		Size = new Vector2(cw, 0);
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
		string eliteTag = item.TipoItem == TipoItem.Elite ? " (Elite)" : "";
		raridadeLabel.Text = $"{raridadeNome}{eliteTag} | Nível {item.NivelRequerido}";
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
		if (!string.IsNullOrEmpty(pesoNome))
		{
			var pesoLabel = new Label();
			pesoLabel.Text = $"Classe do Item: {pesoNome}";
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
		AddSecaoTitulo("CARTAS");
		var vazio = new Label();
		vazio.Text = "Nenhuma carta equipada";
		vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
		vazio.AddThemeFontSizeOverride("font_size", 11);
		AddMargined(vazio, 8, 2);
	}

	private void AddRefino()
	{
		AddSecaoTitulo("REFINO");
		var vazio = new Label();
		vazio.Text = "Item sem refino";
		vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
		vazio.AddThemeFontSizeOverride("font_size", 11);
		AddMargined(vazio, 8, 2);
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
		label.AddThemeFontSizeOverride("font_size", 12);
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
		label.AddThemeFontSizeOverride("font_size", 12);
		AddMargined(label, 12, 1);
		return true;
	}

	private bool AddStatF(float valor, string nome, string formato, Color cor)
	{
		if (valor == 0) return false;
		var label = new Label();
		label.Text = $"+{valor:F1} {nome}";
		label.AddThemeColorOverride("font_color", cor);
		label.AddThemeFontSizeOverride("font_size", 12);
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
