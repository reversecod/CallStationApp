use CallStation;

-- ==========================
-- TABELA DE USUÁRIOS
-- ==========================
CREATE TABLE Usuarios (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nome_completo VARCHAR(100) NOT NULL,
    nome_usuario VARCHAR(20) NOT NULL UNIQUE,
    email VARCHAR(100) UNIQUE,
    senha VARCHAR(255) NOT NULL,
    foto_usuario VARCHAR(255),
    modo_escuro BOOLEAN NOT NULL DEFAULT FALSE,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ==========================
-- TABELA DE GRUPOS
-- ==========================

CREATE TABLE Grupos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(35) NOT NULL,
    descricao_grupo VARCHAR(200),
    foto_grupo VARCHAR(255),
    etiqueta_cor ENUM('branco','vermelho','laranja','amarelo','verde','azul','roxo','rosa','preto'),
    criador_id INT NOT NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_grupo_criador
        FOREIGN KEY (criador_id) REFERENCES Usuarios(id),
    UNIQUE (nome, criador_id)
);

-- ==========================
-- USUÁRIOS ↔ GRUPOS
-- ==========================
CREATE TABLE Usuarios_grupos (
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,
    permissao ENUM('Nenhuma','Colaborador','Tecnico','Administracao')
        NOT NULL DEFAULT 'Nenhuma',
    data_adicao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_ultimo_acesso DATETIME NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    data_remocao DATETIME NULL,
    removido_por_usuario_id INT NULL,
    PRIMARY KEY (usuario_id, grupo_id),
    CONSTRAINT fk_ug_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id),
    CONSTRAINT fk_ug_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),
    CONSTRAINT fk_ug_removido_por_usuario
        FOREIGN KEY (removido_por_usuario_id) REFERENCES Usuarios(id)
);

CREATE INDEX idx_ug_usuario_grupo_ativo
    ON Usuarios_grupos (usuario_id, grupo_id, ativo);

CREATE INDEX idx_ug_usuario_ativo_ultimo_acesso
    ON Usuarios_grupos (usuario_id, ativo, data_ultimo_acesso);

CREATE INDEX idx_ug_grupo_ativo_permissao
    ON Usuarios_grupos (grupo_id, ativo, permissao);

-- ==========================
-- INFORMAÇÕES DO USUÁRIO NO GRUPO
-- ==========================
CREATE TABLE Info_usuarios_grupos (
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,
    apelido VARCHAR(35),
    descricao_ativo VARCHAR(500),
    identificador_interno VARCHAR(50),
    observacao VARCHAR(500),
    data_atualizacao_ativo DATETIME,
    data_atualizacao_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (usuario_id, grupo_id),
    CONSTRAINT fk_info_ug
        FOREIGN KEY (usuario_id, grupo_id)
        REFERENCES Usuarios_grupos (usuario_id, grupo_id)
);

-- ==========================
-- TABELAS PADRÃO (POR GRUPO)
-- ==========================
CREATE TABLE Ocorrencias_tipo (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tipo_ocorrencia VARCHAR(50) NOT NULL,
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,
    CONSTRAINT fk_tipo_ug
	FOREIGN KEY (usuario_id, grupo_id)
	REFERENCES Usuarios_grupos (usuario_id, grupo_id),
    UNIQUE (tipo_ocorrencia, grupo_id)
);

CREATE TABLE Setores (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nome_setor VARCHAR(50) NOT NULL,
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,
    CONSTRAINT fk_setor_ug
        FOREIGN KEY (usuario_id, grupo_id)
        REFERENCES Usuarios_grupos (usuario_id, grupo_id),
    UNIQUE (nome_setor, grupo_id)
);

CREATE TABLE Ocorrencias_categoria (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tipo_id INT NOT NULL,
    categoria_ocorrencia VARCHAR(50) NOT NULL,
    CONSTRAINT fk_categoria_tipo
        FOREIGN KEY (tipo_id) REFERENCES Ocorrencias_tipo(id),
    UNIQUE (tipo_id, categoria_ocorrencia)
);

CREATE TABLE Ocorrencias_subcategoria (
    id INT AUTO_INCREMENT PRIMARY KEY,
    categoria_id INT NOT NULL,
    subcategoria_ocorrencia VARCHAR(100) NOT NULL,
    CONSTRAINT fk_subcategoria_categoria
        FOREIGN KEY (categoria_id) REFERENCES Ocorrencias_categoria(id),
    UNIQUE (categoria_id, subcategoria_ocorrencia)
);

-- ==========================
-- CHAMADOS (OBRIGATORIAMENTE EM UM GRUPO)
-- ==========================
CREATE TABLE Chamados (
    id INT AUTO_INCREMENT PRIMARY KEY,
    numero_chamado_grupo INT NOT NULL,
    numero_chamado_usuario INT NOT NULL,
    numero_chamado_usuario_grupo INT NOT NULL,

    titulo VARCHAR(35),
    descricao VARCHAR(500),
    solucao VARCHAR(500),

    grupo_id INT NOT NULL,
    criador_chamado_id INT NOT NULL,

    ocorrencia_tipo_id INT,
    ocorrencia_categoria_id INT,
    ocorrencia_subcategoria_id INT,
    setor_id INT,

    anexo_chamado VARCHAR(255),
    prioridade ENUM('Baixa','Media','Alta','Critica'),
    criticidade ENUM('Baixa','Media','Alta','Critico'),
    urgencia ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia'),

    status ENUM(
        'Aberto','EmAndamento','Pendente',
        'Concluido','Fechado','Reaberto',
        'Cancelado','Excluido'
    ) NOT NULL DEFAULT 'Aberto',

    data_inicio_atendimento DATETIME,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_finalizacao DATETIME,
    prazo_resposta DATETIME,
    prazo_conclusao DATETIME,
    publico BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_chamado_criador
        FOREIGN KEY (criador_chamado_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_chamado_tipo
        FOREIGN KEY (ocorrencia_tipo_id) REFERENCES Ocorrencias_tipo(id),
    CONSTRAINT fk_chamado_categoria
        FOREIGN KEY (ocorrencia_categoria_id) REFERENCES Ocorrencias_categoria(id),
    CONSTRAINT fk_chamado_subcategoria
        FOREIGN KEY (ocorrencia_subcategoria_id) REFERENCES Ocorrencias_subcategoria(id),
    CONSTRAINT fk_chamado_setor
        FOREIGN KEY (setor_id) REFERENCES Setores(id),

    UNIQUE (grupo_id, numero_chamado_grupo),
    UNIQUE (criador_chamado_id, numero_chamado_usuario),
    UNIQUE (criador_chamado_id, grupo_id, numero_chamado_usuario_grupo)
);

CREATE INDEX idx_chamados_grupo_status_data
    ON Chamados (grupo_id, status, data_criacao);

CREATE INDEX idx_chamados_grupo_publico_data
    ON Chamados (grupo_id, publico, data_criacao);

CREATE INDEX idx_chamados_grupo_criador_data
    ON Chamados (grupo_id, criador_chamado_id, data_criacao);

CREATE INDEX idx_chamados_grupo_status_prazo_conclusao
    ON Chamados (grupo_id, status, prazo_conclusao);

-- ==========================
-- HISTÓRICO DE STATUS
-- ==========================
CREATE TABLE Historico_status_chamados (
    id INT AUTO_INCREMENT PRIMARY KEY,
    chamado_id INT NOT NULL,
    usuario_id INT NULL,
    status_anterior ENUM(
        'Aberto','EmAndamento','Pendente',
        'Concluido','Fechado','Reaberto',
        'Cancelado','Excluido'
    ) NOT NULL,
    status_novo ENUM(
        'Aberto','EmAndamento','Pendente',
        'Concluido','Fechado','Reaberto',
        'Cancelado','Excluido'
    ) NOT NULL,
    data_transicao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    origem_automatica BOOLEAN NOT NULL DEFAULT FALSE,
    descricao_origem VARCHAR(100) NULL,
    CONSTRAINT fk_historico_chamado
        FOREIGN KEY (chamado_id) REFERENCES Chamados(id),
    CONSTRAINT fk_historico_chamado_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
);

CREATE INDEX idx_historico_chamado_data
    ON Historico_status_chamados (chamado_id, data_transicao);

-- ==========================
-- COMENTÁRIOS
-- ==========================
CREATE TABLE Comentarios_chamados (
    id INT AUTO_INCREMENT PRIMARY KEY,
    chamado_id INT NOT NULL,
    usuario_id INT NOT NULL,
    mensagem VARCHAR(250) NOT NULL,
    anexo_comentario VARCHAR(255),
    data_comentario DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_comentario_chamado
        FOREIGN KEY (chamado_id) REFERENCES Chamados(id),
    CONSTRAINT fk_comentario_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
);

CREATE INDEX idx_comentario_chamado_data
    ON Comentarios_chamados(chamado_id, data_comentario);

CREATE INDEX idx_comentario_usuario_data
    ON Comentarios_chamados(usuario_id, data_comentario);

-- =========================================================
-- 1) QUADROS DE TAREFAS
-- =========================================================
CREATE TABLE Quadros_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    grupo_id INT NOT NULL,
    nome VARCHAR(100) NOT NULL,
    descricao VARCHAR(300) NULL,
    cor VARCHAR(20) NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    criado_por_usuario_id INT NOT NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_quadros_tarefas_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),

    CONSTRAINT fk_quadros_tarefas_criador
        FOREIGN KEY (criado_por_usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT uq_quadros_tarefas_grupo_nome
        UNIQUE (grupo_id, nome)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_quadros_tarefas_grupo_ativo
    ON Quadros_tarefas (grupo_id, ativo);

-- =========================================================
-- 2) USUÁRIOS COM ACESSO AO QUADRO
-- OBS:
-- - o criador do quadro já tem acesso implícito
-- - esta tabela serve para compartilhar o quadro com outros
-- =========================================================
CREATE TABLE Quadros_tarefas_usuarios (
    quadro_id INT NOT NULL,
    usuario_id INT NOT NULL,
    permissao ENUM('Visualizador','Editor','Administrador')
        NOT NULL DEFAULT 'Visualizador',
    data_adicao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    adicionado_por_usuario_id INT NOT NULL,

    PRIMARY KEY (quadro_id, usuario_id),

    CONSTRAINT fk_quadros_tarefas_usuarios_quadro
        FOREIGN KEY (quadro_id) REFERENCES Quadros_tarefas(id),

    CONSTRAINT fk_quadros_tarefas_usuarios_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_quadros_tarefas_usuarios_adicionado_por
        FOREIGN KEY (adicionado_por_usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_quadros_tarefas_usuarios_usuario_permissao
    ON Quadros_tarefas_usuarios (usuario_id, permissao);

-- =========================================================
-- 3) COLUNAS DO QUADRO
-- OBS:
-- - limite_wip deve ser validado na aplicação
-- =========================================================
CREATE TABLE Colunas_quadro (
    id INT AUTO_INCREMENT PRIMARY KEY,
    quadro_id INT NOT NULL,
    nome VARCHAR(60) NOT NULL,
    posicao DECIMAL(18,6) NOT NULL,
    cor VARCHAR(20) NULL,
    limite_wip INT NULL,
    ativa BOOLEAN NOT NULL DEFAULT TRUE,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_colunas_quadro_quadro
        FOREIGN KEY (quadro_id) REFERENCES Quadros_tarefas(id),

    CONSTRAINT uq_colunas_quadro_posicao
        UNIQUE (quadro_id, posicao),

    CONSTRAINT uq_colunas_quadro_nome
        UNIQUE (quadro_id, nome)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_colunas_quadro_quadro_ativa_posicao
    ON Colunas_quadro (quadro_id, ativa, posicao);

-- =========================================================
-- 4) CONTADOR DE CARTÕES POR GRUPO
-- =========================================================
CREATE TABLE Cartao_tarefa_contador_grupo (
    grupo_id INT NOT NULL PRIMARY KEY,
    ultimo_numero INT NOT NULL DEFAULT 0,

    CONSTRAINT fk_cartao_tarefa_contador_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =========================================================
-- 5) TEMPLATES DE CARTÕES
-- =========================================================
CREATE TABLE Templates_cartoes_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    grupo_id INT NOT NULL,
    criado_por_usuario_id INT NOT NULL,
    nome VARCHAR(100) NOT NULL,
    descricao TEXT NULL,
    prioridade ENUM('Baixa','Media','Alta','Critica') NULL,
    criticidade ENUM('Baixa','Media','Alta','Critico') NULL,
    urgencia ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia') NULL,
    cor_capa VARCHAR(20) NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_atualizacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT fk_templates_cartoes_tarefas_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),

    CONSTRAINT fk_templates_cartoes_tarefas_criador
        FOREIGN KEY (criado_por_usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE UNIQUE INDEX uq_templates_cartoes_tarefas_grupo_nome_ativo
    ON Templates_cartoes_tarefas (grupo_id, nome, ativo);

-- =========================================================
-- 5) CARTÕES / TAREFAS
-- OBS:
-- - tarefa nasce privada para o criador
-- - responsavel_usuario_id = responsável principal
-- - pai_cartao_id = subtarefa nativa
-- =========================================================
CREATE TABLE Cartoes_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,

    quadro_id INT NOT NULL,
    coluna_id INT NOT NULL,
    grupo_id INT NOT NULL,

    pai_cartao_id INT NULL,

    numero_cartao_grupo INT NOT NULL,

    titulo VARCHAR(150) NOT NULL,
    descricao TEXT NULL,

    criador_id INT NOT NULL,
    responsavel_usuario_id INT NULL,

    prioridade ENUM('Baixa','Media','Alta','Critica') NULL,
    criticidade ENUM('Baixa','Media','Alta','Critico') NULL,
    urgencia ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia') NULL,

    status ENUM('Ativa','Concluida','Arquivada','Cancelada')
        NOT NULL DEFAULT 'Ativa',

    cor_capa VARCHAR(20) NULL,
    imagem_capa VARCHAR(255) NULL,

    data_inicio DATETIME NULL,
    data_vencimento DATETIME NULL,
    data_conclusao DATETIME NULL,

    ordem_coluna DECIMAL(18,6) NOT NULL,
    percentual_conclusao DECIMAL(5,2) NOT NULL DEFAULT 0.00,

    bloqueada BOOLEAN NOT NULL DEFAULT FALSE,
    motivo_bloqueio VARCHAR(255) NULL,
    privado BOOLEAN NOT NULL DEFAULT TRUE,

    criado_rapidamente BOOLEAN NOT NULL DEFAULT FALSE,

    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_atualizacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,

    CONSTRAINT fk_cartoes_tarefas_quadro
        FOREIGN KEY (quadro_id) REFERENCES Quadros_tarefas(id),

    CONSTRAINT fk_cartoes_tarefas_coluna
        FOREIGN KEY (coluna_id) REFERENCES Colunas_quadro(id),

    CONSTRAINT fk_cartoes_tarefas_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),

    CONSTRAINT fk_cartoes_tarefas_pai
        FOREIGN KEY (pai_cartao_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_cartoes_tarefas_criador
        FOREIGN KEY (criador_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_cartoes_tarefas_responsavel
        FOREIGN KEY (responsavel_usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT uq_cartoes_tarefas_numero_grupo
        UNIQUE (grupo_id, numero_cartao_grupo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_cartoes_tarefas_quadro_coluna_ordem
    ON Cartoes_tarefas (quadro_id, coluna_id, ordem_coluna);

CREATE INDEX idx_cartoes_tarefas_responsavel_status
    ON Cartoes_tarefas (responsavel_usuario_id, status, data_vencimento);

CREATE INDEX idx_cartoes_tarefas_grupo_status
    ON Cartoes_tarefas (grupo_id, status, data_criacao);

CREATE INDEX idx_cartoes_tarefas_criador
    ON Cartoes_tarefas (criador_id, data_criacao);

CREATE INDEX idx_cartoes_tarefas_pai
    ON Cartoes_tarefas (pai_cartao_id);

-- =========================================================
-- 6) USUÁRIOS COM ACESSO DIRETO À TAREFA
-- OBS:
-- - não existe mais tipo 'Responsavel'
-- - responsável principal fica em Cartoes_tarefas.responsavel_usuario_id
-- - esta tabela serve para compartilhar a tarefa com usuários específicos
-- =========================================================
CREATE TABLE Cartoes_tarefas_usuarios (
    cartao_tarefa_id INT NOT NULL,
    usuario_id INT NOT NULL,
    tipo_participacao ENUM('Participante','Observador')
        NOT NULL DEFAULT 'Participante',
    permissao ENUM('Visualizador','Editor')
        NOT NULL DEFAULT 'Visualizador',
    data_adicao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    adicionado_por_usuario_id INT NOT NULL,

    PRIMARY KEY (cartao_tarefa_id, usuario_id),

    CONSTRAINT fk_cartoes_tarefas_usuarios_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_cartoes_tarefas_usuarios_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_cartoes_tarefas_usuarios_adicionado_por
        FOREIGN KEY (adicionado_por_usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_cartoes_tarefas_usuarios_usuario_permissao
    ON Cartoes_tarefas_usuarios (usuario_id, permissao);

-- =========================================================
-- 7) COMENTÁRIOS DA TAREFA
-- =========================================================
CREATE TABLE Comentarios_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cartao_tarefa_id INT NOT NULL,
    usuario_id INT NOT NULL,
    mensagem VARCHAR(250) NOT NULL,
    editado BOOLEAN NOT NULL DEFAULT FALSE,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_edicao DATETIME NULL,

    CONSTRAINT fk_comentarios_tarefas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_comentarios_tarefas_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_comentarios_tarefas_cartao_data
    ON Comentarios_tarefas (cartao_tarefa_id, data_criacao);

-- =========================================================
-- 8) ANEXOS DA TAREFA
-- =========================================================
CREATE TABLE Anexos_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cartao_tarefa_id INT NOT NULL,
    usuario_id INT NOT NULL,
    nome_arquivo VARCHAR(255) NOT NULL,
    caminho_arquivo VARCHAR(500) NOT NULL,
    tipo_arquivo VARCHAR(100) NULL,
    tamanho_bytes BIGINT NULL,
    eh_capa BOOLEAN NOT NULL DEFAULT FALSE,
    data_upload DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_anexos_tarefas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_anexos_tarefas_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_anexos_tarefas_cartao
    ON Anexos_tarefas (cartao_tarefa_id);

-- =========================================================
-- 9) CHECKLISTS DA TAREFA
-- =========================================================
CREATE TABLE Checklists_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cartao_tarefa_id INT NOT NULL,
    titulo VARCHAR(120) NOT NULL,
    posicao DECIMAL(18,6) NOT NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_checklists_tarefas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_checklists_tarefas_cartao_posicao
    ON Checklists_tarefas (cartao_tarefa_id, posicao);

CREATE TABLE Checklist_itens_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    checklist_id INT NOT NULL,
    descricao VARCHAR(255) NOT NULL,
    concluido BOOLEAN NOT NULL DEFAULT FALSE,
    concluido_por_usuario_id INT NULL,
    data_conclusao DATETIME NULL,
    posicao DECIMAL(18,6) NOT NULL,

    CONSTRAINT fk_checklist_itens_tarefas_checklist
        FOREIGN KEY (checklist_id) REFERENCES Checklists_tarefas(id),

    CONSTRAINT fk_checklist_itens_tarefas_usuario
        FOREIGN KEY (concluido_por_usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_checklist_itens_tarefas_checklist_posicao
    ON Checklist_itens_tarefas (checklist_id, posicao);

-- =========================================================
-- 10) ETIQUETAS
-- OBS:
-- - mantidas no nível do grupo para reúso
-- =========================================================
CREATE TABLE Etiquetas_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    grupo_id INT NOT NULL,
    nome VARCHAR(50) NOT NULL,
    cor VARCHAR(20) NOT NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_etiquetas_tarefas_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),

    CONSTRAINT uq_etiquetas_tarefas_grupo_nome
        UNIQUE (grupo_id, nome)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE Cartoes_tarefas_etiquetas (
    cartao_tarefa_id INT NOT NULL,
    etiqueta_id INT NOT NULL,

    PRIMARY KEY (cartao_tarefa_id, etiqueta_id),

    CONSTRAINT fk_cartoes_tarefas_etiquetas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_cartoes_tarefas_etiquetas_etiqueta
        FOREIGN KEY (etiqueta_id) REFERENCES Etiquetas_tarefas(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =========================================================
-- 11) RELAÇÃO TAREFA ↔ CHAMADOS
-- OBS:
-- - permite vários chamados por tarefa
-- - permite desassociar depois
-- =========================================================
CREATE TABLE Cartoes_tarefas_chamados (
    cartao_tarefa_id INT NOT NULL,
    chamado_id INT NOT NULL,
    tipo_relacao ENUM('Origem','Relacionada','BloqueadaPor','ResolveChamado')
        NOT NULL DEFAULT 'Relacionada',
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    data_vinculo DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    vinculado_por_usuario_id INT NOT NULL,
    data_desvinculo DATETIME NULL,
    desvinculado_por_usuario_id INT NULL,

    PRIMARY KEY (cartao_tarefa_id, chamado_id),

    CONSTRAINT fk_cartoes_tarefas_chamados_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_cartoes_tarefas_chamados_chamado
        FOREIGN KEY (chamado_id) REFERENCES Chamados(id),

    CONSTRAINT fk_cartoes_tarefas_chamados_vinculado_por
        FOREIGN KEY (vinculado_por_usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_cartoes_tarefas_chamados_desvinculado_por
        FOREIGN KEY (desvinculado_por_usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_cartoes_tarefas_chamados_chamado_ativo
    ON Cartoes_tarefas_chamados (chamado_id, ativo);

CREATE INDEX idx_cartoes_tarefas_chamados_cartao_ativo
    ON Cartoes_tarefas_chamados (cartao_tarefa_id, ativo);

-- =========================================================
-- 12) HISTÓRICO DAS TAREFAS
-- OBS:
-- - valor_anterior e valor_novo em TEXT
-- =========================================================
CREATE TABLE Historico_tarefas (
    id INT AUTO_INCREMENT PRIMARY KEY,
    cartao_tarefa_id INT NOT NULL,
    usuario_id INT NOT NULL,
    tipo_acao VARCHAR(50) NOT NULL,
    campo_alterado VARCHAR(100) NULL,
    valor_anterior TEXT NULL,
    valor_novo TEXT NULL,
    data_acao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_historico_tarefas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_historico_tarefas_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_historico_tarefas_cartao_data
    ON Historico_tarefas (cartao_tarefa_id, data_acao);

CREATE INDEX idx_historico_tarefas_usuario
    ON Historico_tarefas (usuario_id);

-- =========================================================
-- 13) DEPENDÊNCIAS ENTRE TAREFAS
-- OBS:
-- - subtarefa saiu daqui
-- - agora só dependência real
-- =========================================================
CREATE TABLE Dependencias_tarefas (
    cartao_tarefa_id INT NOT NULL,
    cartao_dependente_id INT NOT NULL,
    tipo_dependencia ENUM('Bloqueia','Relacionada')
        NOT NULL DEFAULT 'Bloqueia',

    PRIMARY KEY (cartao_tarefa_id, cartao_dependente_id),

    CONSTRAINT fk_dependencias_tarefas_cartao
        FOREIGN KEY (cartao_tarefa_id) REFERENCES Cartoes_tarefas(id),

    CONSTRAINT fk_dependencias_tarefas_cartao_dependente
        FOREIGN KEY (cartao_dependente_id) REFERENCES Cartoes_tarefas(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ==========================
-- FEEDBACKS
-- ==========================
CREATE TABLE Feedbacks_chamados (
    id INT AUTO_INCREMENT PRIMARY KEY,
    chamado_id INT NOT NULL,
    avaliador_id INT NOT NULL,
    nota INT NOT NULL CHECK (nota BETWEEN 1 AND 5),
    comentario VARCHAR(250),
    tempo_resposta INT,
    tempo_resolucao INT,
    data_avaliacao DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_feedback_chamado
        FOREIGN KEY (chamado_id) REFERENCES Chamados(id),
    CONSTRAINT fk_feedback_avaliador
        FOREIGN KEY (avaliador_id) REFERENCES Usuarios(id),
    UNIQUE (chamado_id, avaliador_id)
);

-- ==========================
-- CONVITES
-- ==========================

CREATE TABLE Convites_grupo (
    id INT AUTO_INCREMENT PRIMARY KEY,
    grupo_id INT NOT NULL,
    remetente_usuario_id INT NOT NULL,
    destinatario_usuario_id INT NOT NULL,

    status ENUM('Pendente','Aceito','Recusado','Cancelado')
        NOT NULL DEFAULT 'Pendente',

    mensagem VARCHAR(255),
    pendente_chave VARCHAR(64)
        GENERATED ALWAYS AS (
            CASE
                WHEN status = 'Pendente'
                THEN CONCAT(grupo_id, ':', destinatario_usuario_id)
                ELSE NULL
            END
        ) STORED,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_resposta DATETIME NULL,

    CONSTRAINT fk_convite_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),

    CONSTRAINT fk_convite_remetente
        FOREIGN KEY (remetente_usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT fk_convite_destinatario
        FOREIGN KEY (destinatario_usuario_id) REFERENCES Usuarios(id),

    CONSTRAINT chk_convite_usuarios_diferentes
        CHECK (remetente_usuario_id <> destinatario_usuario_id)
);

CREATE INDEX idx_convite_destinatario_status
    ON Convites_grupo(destinatario_usuario_id, status);

CREATE INDEX idx_convite_grupo_status
    ON Convites_grupo(grupo_id, status);

CREATE UNIQUE INDEX uq_convites_grupo_pendente
    ON Convites_grupo(pendente_chave);
    
-- ==========================
-- NOTIFICAÇÕES
-- ==========================

CREATE TABLE Notificacoes (
    id INT AUTO_INCREMENT PRIMARY KEY,
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,

    tipo ENUM('ConviteGrupo','Sistema','Chamado','Tarefa')
        NOT NULL DEFAULT 'Sistema',

    titulo VARCHAR(150) NOT NULL,
    mensagem VARCHAR(500) NOT NULL,

    lida BOOLEAN NOT NULL DEFAULT FALSE,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_leitura DATETIME NULL,

    referencia_id INT NULL,
    referencia_tipo VARCHAR(50) NULL,
    link_destino VARCHAR(255) NULL,

    CONSTRAINT fk_notificacao_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id),
    CONSTRAINT fk_notificacao_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id)
);

CREATE INDEX idx_notificacao_usuario_lida
    ON Notificacoes(usuario_id, lida, data_criacao);

CREATE INDEX idx_notificacao_usuario_grupo_lida_data
    ON Notificacoes(usuario_id, grupo_id, lida, data_criacao);

CREATE INDEX idx_notificacao_usuario_referencia_lida
    ON Notificacoes(usuario_id, lida, tipo, referencia_tipo, referencia_id);
    
-- ==========================
-- HISTÓRICO DE ALTERAÇÕES
-- ==========================

CREATE TABLE Historico_alteracoes_chamado (
    id INT NOT NULL AUTO_INCREMENT,
    chamado_id INT NOT NULL,
    grupo_id INT NOT NULL,
    usuario_id INT NOT NULL,

    campo_alterado VARCHAR(100) NOT NULL,
    valor_anterior VARCHAR(500) NULL,
    valor_alterado VARCHAR(500) NULL,

    tipo_alteracao VARCHAR(50) NOT NULL,
    data_alteracao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT PK_historico_alteracoes_chamado PRIMARY KEY (id),

    CONSTRAINT FK_historico_alteracoes_chamado_chamado
        FOREIGN KEY (chamado_id) REFERENCES chamados(id),

    CONSTRAINT FK_historico_alteracoes_chamado_grupo
        FOREIGN KEY (grupo_id) REFERENCES grupos(id),

    CONSTRAINT FK_historico_alteracoes_chamado_usuario
        FOREIGN KEY (usuario_id) REFERENCES usuarios(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX IX_historico_alteracoes_chamado_chamado_data
    ON historico_alteracoes_chamado (chamado_id, data_alteracao);

CREATE INDEX IX_historico_alteracoes_chamado_campo_data
    ON historico_alteracoes_chamado (chamado_id, campo_alterado, data_alteracao);

CREATE INDEX IX_historico_alteracoes_chamado_usuario
    ON historico_alteracoes_chamado (usuario_id);
    
-- ==========================
-- CONTADORES DE CHAMADOS
-- ==========================

CREATE TABLE Chamado_contador_grupo (
    grupo_id INT NOT NULL,
    ultimo_numero INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_chamado_contador_grupo PRIMARY KEY (grupo_id),
    CONSTRAINT FK_chamado_contador_grupo_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id)
);

CREATE TABLE Chamado_contador_usuario (
    usuario_id INT NOT NULL,
    ultimo_numero INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_chamado_contador_usuario PRIMARY KEY (usuario_id),
    CONSTRAINT FK_chamado_contador_usuario_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
);

CREATE TABLE Chamado_contador_usuario_grupo (
    usuario_id INT NOT NULL,
    grupo_id INT NOT NULL,
    ultimo_numero INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_chamado_contador_usuario_grupo PRIMARY KEY (usuario_id, grupo_id),
    CONSTRAINT FK_chamado_contador_usuario_grupo_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id),
    CONSTRAINT FK_chamado_contador_usuario_grupo_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id)
);

-- ==========================
-- CONFIGURACOES DO GRUPO
-- ==========================

CREATE TABLE Grupos_configuracoes (
    grupo_id INT NOT NULL,
    slug VARCHAR(60) NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    obrigar_setor BOOLEAN NOT NULL DEFAULT FALSE,
    obrigar_tipo_ocorrencia BOOLEAN NOT NULL DEFAULT FALSE,
    obrigar_categoria BOOLEAN NOT NULL DEFAULT FALSE,
    obrigar_subcategoria BOOLEAN NOT NULL DEFAULT FALSE,
    permitir_chamado_publico BOOLEAN NOT NULL DEFAULT TRUE,
    exigir_solucao_para_concluir BOOLEAN NOT NULL DEFAULT FALSE,
    dias_para_fechamento_automatico INT NULL,
    automatizar_pendente_por_prazo_conclusao BOOLEAN NOT NULL DEFAULT FALSE,
    horas_apos_vencimento_para_pendente INT NULL,
    horas_antes_prazo_para_alerta INT NULL,
    notificar_administradores_sla BOOLEAN NOT NULL DEFAULT TRUE,
    exibir_data_finalizacao_modal BOOLEAN NOT NULL DEFAULT TRUE,
    exibir_prazo_resposta_modal BOOLEAN NOT NULL DEFAULT TRUE,
    exibir_prazo_conclusao_modal BOOLEAN NOT NULL DEFAULT TRUE,
    atualizado_por_usuario_id INT NULL,
    data_atualizacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT PK_grupos_configuracoes PRIMARY KEY (grupo_id),
    CONSTRAINT FK_grupos_configuracoes_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),
    CONSTRAINT FK_grupos_configuracoes_usuario
        FOREIGN KEY (atualizado_por_usuario_id) REFERENCES Usuarios(id)
);

CREATE UNIQUE INDEX uq_grupos_configuracoes_slug
    ON Grupos_configuracoes(slug);

CREATE TABLE Grupos_tipos_chamados (
    id INT NOT NULL AUTO_INCREMENT,
    grupo_id INT NOT NULL,
    nome VARCHAR(50) NOT NULL,
    descricao VARCHAR(160) NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    posicao INT NOT NULL DEFAULT 0,
    criado_por_usuario_id INT NOT NULL,
    arquivado_por_usuario_id INT NULL,
    data_criacao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    data_arquivamento DATETIME NULL,

    CONSTRAINT PK_grupos_tipos_chamados PRIMARY KEY (id),
    CONSTRAINT FK_grupos_tipos_chamados_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),
    CONSTRAINT FK_grupos_tipos_chamados_criador
        FOREIGN KEY (criado_por_usuario_id) REFERENCES Usuarios(id),
    CONSTRAINT FK_grupos_tipos_chamados_arquivado_por
        FOREIGN KEY (arquivado_por_usuario_id) REFERENCES Usuarios(id)
);

CREATE UNIQUE INDEX uq_grupos_tipos_chamados_grupo_nome
    ON Grupos_tipos_chamados(grupo_id, nome);

CREATE INDEX idx_grupos_tipos_chamados_grupo_ativo_posicao
    ON Grupos_tipos_chamados(grupo_id, ativo, posicao);

CREATE TABLE Grupos_auditorias (
    id BIGINT NOT NULL AUTO_INCREMENT,
    grupo_id INT NOT NULL,
    usuario_id INT NOT NULL,
    tipo_acao VARCHAR(50) NOT NULL,
    entidade VARCHAR(50) NOT NULL,
    entidade_id INT NULL,
    campo_alterado VARCHAR(80) NULL,
    valor_anterior VARCHAR(500) NULL,
    valor_novo VARCHAR(500) NULL,
    ip_origem VARCHAR(64) NULL,
    user_agent VARCHAR(255) NULL,
    data_acao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT PK_grupos_auditorias PRIMARY KEY (id),
    CONSTRAINT FK_grupos_auditorias_grupo
        FOREIGN KEY (grupo_id) REFERENCES Grupos(id),
    CONSTRAINT FK_grupos_auditorias_usuario
        FOREIGN KEY (usuario_id) REFERENCES Usuarios(id)
);

CREATE INDEX idx_grupos_auditorias_grupo_data
    ON Grupos_auditorias(grupo_id, data_acao);

CREATE INDEX idx_grupos_auditorias_grupo_acao
    ON Grupos_auditorias(grupo_id, tipo_acao);
