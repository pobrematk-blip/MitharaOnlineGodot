using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const float DuelArenaTileSize = 32f;
    private const float DuelArenaSizeTiles = 100f;
    private const float DuelArenaHalfSize = DuelArenaTileSize * DuelArenaSizeTiles * 0.5f;
    private const double DuelMaxDurationSeconds = 300.0;

    private readonly Dictionary<ulong, DuelInvite> _duelInvites = new();
    private readonly Dictionary<ulong, ActiveDuel> _activeDuels = new();

    private void HandleDuelRequestPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player)
            return;

        string targetName = reader.GetString();
        int goldWager = reader.AvailableBytes >= 4 ? Math.Max(0, reader.GetInt()) : 0;

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || targetPeer == null)
        {
            SendSystemMessage(peer, $"Jogador '{targetName}' nao encontrado.");
            return;
        }

        if (target.Id == player.Id)
        {
            SendSystemMessage(peer, "Voce nao pode duelar consigo mesmo.");
            return;
        }

        if (IsPlayerInActiveDuel(player.Id) || IsPlayerInActiveDuel(target.Id))
        {
            SendSystemMessage(peer, "Um dos jogadores ja esta em duelo.");
            return;
        }

        if (goldWager > player.Gold)
        {
            SendSystemMessage(peer, "Voce nao tem ouro suficiente para essa aposta.");
            return;
        }

        _duelInvites[target.Id] = new DuelInvite
        {
            ChallengerId = player.Id,
            ChallengerName = player.Name,
            TargetId = target.Id,
            GoldWager = goldWager,
            CreatedAt = _gameTime,
        };

        var w = PacketSerializer.WritePacket(PacketId.S2C_DuelRequested);
        w.Put(player.Name);
        w.Put(goldWager);
        targetPeer.Send(w, DeliveryMethod.ReliableOrdered);

        string aposta = goldWager > 0 ? $" com aposta de {goldWager} ouro" : "";
        SendSystemMessage(peer, $"Voce desafiou {target.Name} para um duelo{aposta}!");
    }

    private void HandleDuelAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player)
            return;

        if (!_duelInvites.TryGetValue(player.Id, out var invite))
        {
            SendSystemMessage(peer, "Voce nao tem um convite de duelo pendente.");
            return;
        }
        _duelInvites.Remove(player.Id);

        var challenger = FindEntityById(invite.ChallengerId, out var challengerPeer, out _) as PlayerEntity;
        if (challenger == null || challengerPeer == null)
        {
            SendSystemMessage(peer, "O desafiante esta offline.");
            return;
        }

        if (IsPlayerInActiveDuel(player.Id) || IsPlayerInActiveDuel(challenger.Id))
        {
            SendSystemMessage(peer, "Um dos jogadores ja esta em duelo.");
            SendSystemMessage(challengerPeer, "Duelo cancelado: um dos jogadores ja esta em duelo.");
            return;
        }

        if (invite.GoldWager > challenger.Gold || invite.GoldWager > player.Gold)
        {
            SendSystemMessage(peer, "Duelo cancelado: ouro insuficiente para a aposta.");
            SendSystemMessage(challengerPeer, "Duelo cancelado: ouro insuficiente para a aposta.");
            return;
        }

        string mapName = GetPlayerCurrentMap(player.Id);
        float centerX = (player.X + challenger.X) * 0.5f;
        float centerY = (player.Y + challenger.Y) * 0.5f;
        var duel = new ActiveDuel
        {
            PlayerA = challenger.Id,
            PlayerB = player.Id,
            PlayerAName = challenger.Name,
            PlayerBName = player.Name,
            GoldWager = invite.GoldWager,
            MapName = mapName,
            CenterX = centerX,
            CenterY = centerY,
            StartedAt = _gameTime,
            PlayerALastHealth = challenger.Health,
            PlayerBLastHealth = player.Health,
        };
        _activeDuels[challenger.Id] = duel;
        _activeDuels[player.Id] = duel;

        var w1 = PacketSerializer.WritePacket(PacketId.S2C_DuelStart);
        w1.Put(player.Id);
        w1.Put(player.Name);
        w1.Put(centerX);
        w1.Put(centerY);
        w1.Put(DuelArenaHalfSize);
        w1.Put((float)DuelMaxDurationSeconds);
        challengerPeer.Send(w1, DeliveryMethod.ReliableOrdered);

        var w2 = PacketSerializer.WritePacket(PacketId.S2C_DuelStart);
        w2.Put(challenger.Id);
        w2.Put(challenger.Name);
        w2.Put(centerX);
        w2.Put(centerY);
        w2.Put(DuelArenaHalfSize);
        w2.Put((float)DuelMaxDurationSeconds);
        peer.Send(w2, DeliveryMethod.ReliableOrdered);

        string aposta = invite.GoldWager > 0 ? $" Aposta: {invite.GoldWager} ouro." : "";
        SendSystemMessage(peer, $"Duelo iniciado! Area PVP temporaria: 100x100 tiles.{aposta}");
        SendSystemMessage(challengerPeer, $"Duelo iniciado! Area PVP temporaria: 100x100 tiles.{aposta}");
        Logger.Info($"[DUEL] {player.Name} vs {challenger.Name} iniciado. Wager={invite.GoldWager}");
    }

    private void HandleDuelDeclinePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player)
            return;

        if (!_duelInvites.TryGetValue(player.Id, out var invite))
            return;
        _duelInvites.Remove(player.Id);

        var challengerPeer = FindPeerByEntityId(invite.ChallengerId);
        if (challengerPeer != null)
            SendSystemMessage(challengerPeer, $"{sender.Name} recusou seu duelo.");
    }

    private bool IsPlayerInActiveDuel(ulong playerId)
    {
        return _activeDuels.ContainsKey(playerId);
    }

    private bool CanDuelistsDamage(PlayerEntity attacker, PlayerEntity target)
    {
        if (!_activeDuels.TryGetValue(attacker.Id, out var duel))
            return false;
        if (!_activeDuels.TryGetValue(target.Id, out var targetDuel) || !ReferenceEquals(duel, targetDuel))
            return false;
        if (!duel.Contains(attacker.Id) || !duel.Contains(target.Id))
            return false;

        string attackerMap = GetPlayerCurrentMap(attacker.Id);
        string targetMap = GetPlayerCurrentMap(target.Id);
        return duel.IsInside(attacker.Id, attackerMap, attacker.X, attacker.Y)
            && duel.IsInside(target.Id, targetMap, target.X, target.Y);
    }

    private void ProcessActiveDuels()
    {
        if (_activeDuels.Count == 0)
            return;

        var duels = _activeDuels.Values.Distinct().ToList();
        foreach (var duel in duels)
        {
            var playerA = FindEntityById(duel.PlayerA, out var peerA, out _) as PlayerEntity;
            var playerB = FindEntityById(duel.PlayerB, out var peerB, out _) as PlayerEntity;

            if (playerA == null || playerB == null || peerA == null || peerB == null)
            {
                EndDuel(duel, null, "Duelo encerrado: jogador desconectado.");
                continue;
            }

            if (playerA.Health <= 0)
            {
                EndDuel(duel, playerB.Id, $"{playerB.Name} venceu o duelo!");
                continue;
            }

            if (playerB.Health <= 0)
            {
                EndDuel(duel, playerA.Id, $"{playerA.Name} venceu o duelo!");
                continue;
            }

            duel.TrackDamage(playerA, playerB);

            if (_gameTime - duel.StartedAt > DuelMaxDurationSeconds)
            {
                ulong? winnerId = duel.GetWinnerByLeastDamageTaken();
                string result = winnerId == duel.PlayerA
                    ? $"{playerA.Name} venceu o duelo por tomar menos dano!"
                    : winnerId == duel.PlayerB
                        ? $"{playerB.Name} venceu o duelo por tomar menos dano!"
                        : "Duelo encerrado por tempo limite: empate por dano recebido.";
                EndDuel(duel, winnerId, result);
                continue;
            }

            bool playerAInside = duel.IsInside(playerA.Id, GetPlayerCurrentMap(playerA.Id), playerA.X, playerA.Y);
            bool playerBInside = duel.IsInside(playerB.Id, GetPlayerCurrentMap(playerB.Id), playerB.X, playerB.Y);
            if (!playerAInside && playerBInside)
                EndDuel(duel, playerB.Id, $"{playerA.Name} saiu da arena. {playerB.Name} venceu o duelo!");
            else if (!playerBInside && playerAInside)
                EndDuel(duel, playerA.Id, $"{playerB.Name} saiu da arena. {playerA.Name} venceu o duelo!");
            else if (!playerAInside && !playerBInside)
                EndDuel(duel, null, "Duelo encerrado: os dois jogadores sairam da arena.");
        }
    }

    private void EndDuel(ActiveDuel duel, ulong? winnerId, string message)
    {
        _activeDuels.Remove(duel.PlayerA);
        _activeDuels.Remove(duel.PlayerB);

        var playerA = FindEntityById(duel.PlayerA, out var peerA, out _) as PlayerEntity;
        var playerB = FindEntityById(duel.PlayerB, out var peerB, out _) as PlayerEntity;

        if (winnerId.HasValue && duel.GoldWager > 0 && playerA != null && playerB != null && peerA != null && peerB != null)
        {
            var winner = winnerId.Value == duel.PlayerA ? playerA : playerB;
            var loser = winnerId.Value == duel.PlayerA ? playerB : playerA;
            var winnerPeer = winnerId.Value == duel.PlayerA ? peerA : peerB;
            var loserPeer = winnerId.Value == duel.PlayerA ? peerB : peerA;
            var winnerSession = _sessions.TryGetValue(winnerPeer, out var ws) ? ws : null;
            var loserSession = _sessions.TryGetValue(loserPeer, out var ls) ? ls : null;

            if (winnerSession?.SelectedCharacter != null && loserSession?.SelectedCharacter != null
                && winner.Gold >= duel.GoldWager && loser.Gold >= duel.GoldWager)
            {
                winner.Gold += duel.GoldWager;
                loser.Gold -= duel.GoldWager;
                _db.SaveCharacterGold(winnerSession.SelectedCharacter.Id, winner.Gold);
                _db.SaveCharacterGold(loserSession.SelectedCharacter.Id, loser.Gold);
                SendGoldUpdate(winnerPeer, winner.Gold);
                SendGoldUpdate(loserPeer, loser.Gold);
                message += $" {winner.Name} ganhou {duel.GoldWager} ouro da aposta.";
            }
        }

        SendDuelEndToPeer(peerA, winnerId == duel.PlayerA, message);
        SendDuelEndToPeer(peerB, winnerId == duel.PlayerB, message);
        Logger.Info($"[DUEL] Encerrado: {message}");
    }

    private void SendDuelEndToPeer(NetPeer? peer, bool won, string message)
    {
        if (peer == null)
            return;

        var w = PacketSerializer.WritePacket(PacketId.S2C_DuelEnd);
        w.Put(won);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
        SendSystemMessage(peer, message);
    }

    private sealed class DuelInvite
    {
        public ulong ChallengerId { get; set; }
        public string ChallengerName { get; set; } = "";
        public ulong TargetId { get; set; }
        public int GoldWager { get; set; }
        public double CreatedAt { get; set; }
    }

    private sealed class ActiveDuel
    {
        public ulong PlayerA { get; set; }
        public ulong PlayerB { get; set; }
        public string PlayerAName { get; set; } = "";
        public string PlayerBName { get; set; } = "";
        public int GoldWager { get; set; }
        public string MapName { get; set; } = "";
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public double StartedAt { get; set; }
        public int PlayerALastHealth { get; set; }
        public int PlayerBLastHealth { get; set; }
        public int PlayerADamageTaken { get; set; }
        public int PlayerBDamageTaken { get; set; }

        public bool Contains(ulong playerId) => playerId == PlayerA || playerId == PlayerB;

        public void TrackDamage(PlayerEntity playerA, PlayerEntity playerB)
        {
            if (playerA.Health < PlayerALastHealth)
                PlayerADamageTaken += PlayerALastHealth - playerA.Health;
            if (playerB.Health < PlayerBLastHealth)
                PlayerBDamageTaken += PlayerBLastHealth - playerB.Health;

            PlayerALastHealth = playerA.Health;
            PlayerBLastHealth = playerB.Health;
        }

        public ulong? GetWinnerByLeastDamageTaken()
        {
            if (PlayerADamageTaken == PlayerBDamageTaken)
                return null;
            return PlayerADamageTaken < PlayerBDamageTaken ? PlayerA : PlayerB;
        }

        public bool IsInside(ulong playerId, string mapName, float x, float y)
        {
            return Contains(playerId)
                && string.Equals(MapName, mapName, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(x - CenterX) <= DuelArenaHalfSize
                && Math.Abs(y - CenterY) <= DuelArenaHalfSize;
        }
    }
}
