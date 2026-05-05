-- Execute no banco existente do servidor interno uma unica vez.
-- Se o indice ja existir, ignore o erro de duplicidade de nome.

USE CallStation;

CREATE INDEX IX_historico_alteracoes_chamado_campo_data
    ON Historico_alteracoes_chamado (chamado_id, campo_alterado, data_alteracao);
