-- Use na VPS somente se quiser limpar os anuncios ativos/de teste do mercado.
-- Os itens anunciados deixam de aparecer no jogo/site; itens ja retirados do inventario
-- nao voltam automaticamente por este script.
UPDATE marketplace_listings
SET status = 'cancelled',
    updated_at = CURRENT_TIMESTAMP
WHERE status IN ('active', 'pending_payment', 'expired');
