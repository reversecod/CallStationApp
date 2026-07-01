// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const MB = 1024 * 1024;
    const regras = {
        ".jpg": { limite: 5 * MB, tipo: "imagem", rotulo: "JPG" },
        ".jpeg": { limite: 5 * MB, tipo: "imagem", rotulo: "JPEG" },
        ".png": { limite: 5 * MB, tipo: "imagem", rotulo: "PNG" },
        ".gif": { limite: 5 * MB, tipo: "imagem", rotulo: "GIF" },
        ".webp": { limite: 5 * MB, tipo: "imagem", rotulo: "WEBP" },
        ".txt": { limite: 1 * MB, tipo: "texto", rotulo: "TXT" },
        ".msg": { limite: 15 * MB, tipo: "download", rotulo: "MSG" },
        ".xlsx": { limite: 15 * MB, tipo: "download", rotulo: "XLSX" },
        ".docx": { limite: 15 * MB, tipo: "download", rotulo: "DOCX" },
        ".pdf": { limite: 25 * MB, tipo: "pdf", rotulo: "PDF" },
        ".mp4": { limite: 100 * MB, tipo: "video", rotulo: "MP4" }
    };

    function obterExtensao(nome) {
        const indice = String(nome || "").lastIndexOf(".");
        return indice >= 0 ? String(nome).slice(indice).toLowerCase() : "";
    }

    function formatarBytes(bytes) {
        if (bytes >= MB) return `${(bytes / MB).toFixed(bytes % MB === 0 ? 0 : 1)} MB`;
        return `${Math.ceil(bytes / 1024)} KB`;
    }

    function validarArquivo(arquivo) {
        if (!arquivo) return { ok: false, mensagem: "Selecione um arquivo." };

        const extensao = obterExtensao(arquivo.name);
        const regra = regras[extensao];
        if (!regra) {
            return {
                ok: false,
                mensagem: "Tipo de arquivo nao permitido. Use JPG, PNG, GIF, WEBP, TXT, MSG, XLSX, DOCX, PDF ou MP4."
            };
        }

        if (arquivo.size > regra.limite) {
            return {
                ok: false,
                mensagem: `O arquivo ${regra.rotulo} deve ter no maximo ${formatarBytes(regra.limite)}.`
            };
        }

        return { ok: true, extensao, regra };
    }

    async function prepararParaEnvio(arquivo) {
        if (!arquivo || typeof CompressionStream !== "function" || typeof File !== "function") {
            return { arquivo, compactado: false };
        }

        const extensao = obterExtensao(arquivo.name);
        if (extensao !== ".txt") {
            return { arquivo, compactado: false };
        }

        try {
            const compactado = await new Response(arquivo.stream().pipeThrough(new CompressionStream("gzip"))).blob();
            if (!compactado.size || compactado.size >= arquivo.size) {
                return { arquivo, compactado: false };
            }

            return {
                arquivo: new File([compactado], arquivo.name, {
                    type: "application/gzip",
                    lastModified: arquivo.lastModified
                }),
                compactado: true
            };
        } catch {
            return { arquivo, compactado: false };
        }
    }

    function tipoPorExtensao(extensao) {
        return regras[String(extensao || "").toLowerCase()]?.tipo || "download";
    }

    function iconePorExtensao(extensao) {
        switch (String(extensao || "").toLowerCase()) {
            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".gif":
            case ".webp":
                return "bi-image";
            case ".txt":
                return "bi-file-text";
            case ".msg":
                return "bi-envelope-paper";
            case ".xlsx":
                return "bi-file-earmark-spreadsheet";
            case ".docx":
                return "bi-file-earmark-word";
            case ".pdf":
                return "bi-file-earmark-pdf";
            case ".mp4":
                return "bi-file-earmark-play";
            default:
                return "bi-paperclip";
        }
    }

    function normalizarMetadados(anexo, urlLegada) {
        if (!anexo && !urlLegada) return null;
        if (typeof anexo === "string") {
            return {
                nome: "Anexo",
                extensao: "",
                tipoVisualizacao: "imagem",
                podeVisualizar: true,
                visualizacaoUrl: anexo,
                downloadUrl: anexo
            };
        }

        const extensao = anexo?.extensao || "";
        const tipoVisualizacao = anexo?.tipoVisualizacao || tipoPorExtensao(extensao);
        return {
            nome: anexo?.nome || "Anexo",
            extensao,
            tipoVisualizacao,
            podeVisualizar: Boolean(anexo?.podeVisualizar),
            visualizacaoUrl: anexo?.visualizacaoUrl || urlLegada || "",
            downloadUrl: anexo?.downloadUrl || urlLegada || ""
        };
    }

    window.CallStationAnexos = {
        accept: Object.keys(regras).join(","),
        regras,
        obterExtensao,
        validarArquivo,
        prepararParaEnvio,
        formatarBytes,
        tipoPorExtensao,
        iconePorExtensao,
        normalizarMetadados
    };
})();
