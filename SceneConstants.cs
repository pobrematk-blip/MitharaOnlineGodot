/// <summary>
/// Constantes centralizadas para todos os caminhos de cenas do projeto.
/// Usar esta classe em vez de hardcoding strings de caminhos para evitar bugs de digitação.
/// </summary>
public static class SceneConstants
{
    // Cenas Principais
    public const string MAIN = "res://scenes/Main.tscn";
    public const string MENU_INICIAL = "res://scenes/MenuInicial.tscn";
    public const string CRIACAO_PERSONAGEM = "res://scenes/CriacaoPersonagem.tscn";
    public const string SELECAO_PERSONAGEM = "res://scenes/SelecaoPersonagem.tscn";
    public const string INTRO_HISTORIA = "res://scenes/IntroHistoria.tscn";

    // Lojas e Contas
    public const string LOJA_CASH_UI = "res://SistemaContas/LojaCashUI.tscn";

    // Componentes UI
    public const string PLAYER_HUD = "res://characterSlots/PlayerHud.tscn";
    public const string BANCO_UI = "res://Banco/BancoUI.tscn";
    public const string CHARACTER_UI = "res://characterSlots/CharacterUI.tscn";
    public const string INVENTARIO_UI = "res://resources/Inventario/InventarioUI.tscn";

    // Settings, Amigos, Guild, Grupo e Missões
    public const string SETTINGS_UI = "res://ui/SettingsUI.tscn";
    public const string FRIENDS_UI = "res://ui/FriendsUI.tscn";
    public const string GUILD_UI = "res://ui/GuildUI.tscn";
    public const string PARTY_UI = "res://ui/PartyUI.tscn";
    public const string QUEST_UI = "res://ui/QuestUI.tscn";

    // Prefabs de Entidades
    public const string PLAYER = "res://characters/Player/player.tscn";
    public const string PROJETIL = "res://resources/Projetil/Projetil.tscn";
}
