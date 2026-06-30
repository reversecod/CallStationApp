using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using System.Text;

namespace CallStationApp.Services;

public sealed class AnexoUploadService
{
    public const long LimiteRequisicaoBytes = 120L * 1024L * 1024L;

    private const long UmMb = 1024L * 1024L;
    private const int MaximoEntradasZip = 512;
    private const long MaximoTextoPreviewBytes = 1L * UmMb;

    private static readonly IReadOnlyDictionary<string, RegraAnexo> Regras = new Dictionary<string, RegraAnexo>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new(".jpg", "image/jpeg", 5L * UmMb, "imagem", true),
        [".jpeg"] = new(".jpeg", "image/jpeg", 5L * UmMb, "imagem", true),
        [".png"] = new(".png", "image/png", 5L * UmMb, "imagem", true),
        [".gif"] = new(".gif", "image/gif", 5L * UmMb, "imagem", true),
        [".webp"] = new(".webp", "image/webp", 5L * UmMb, "imagem", true),
        [".txt"] = new(".txt", "text/plain; charset=utf-8", 1L * UmMb, "texto", true),
        [".msg"] = new(".msg", "application/vnd.ms-outlook", 15L * UmMb, "download", false),
        [".xlsx"] = new(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 15L * UmMb, "download", false),
        [".docx"] = new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 15L * UmMb, "download", false),
        [".pdf"] = new(".pdf", "application/pdf", 25L * UmMb, "pdf", true),
        [".mp4"] = new(".mp4", "video/mp4", 100L * UmMb, "video", true)
    };

    public IReadOnlyCollection<string> ExtensoesPermitidas => Regras.Keys.ToArray();

    public RegraAnexo? ObterRegra(string? extensao)
    {
        if (string.IsNullOrWhiteSpace(extensao))
            return null;

        return Regras.TryGetValue(extensao.Trim().ToLowerInvariant(), out var regra)
            ? regra
            : null;
    }

    public async Task<ResultadoAnexoSalvo> SalvarAsync(IFormFile arquivo, bool compactadoGzip, string uploadsRoot, CancellationToken cancellationToken = default)
    {
        if (arquivo.Length <= 0)
            return ResultadoAnexoSalvo.Falha("Selecione um arquivo valido.");

        var nomeOriginal = SanitizarNomeOriginal(arquivo.FileName);
        var extensao = Path.GetExtension(nomeOriginal).ToLowerInvariant();
        var regra = ObterRegra(extensao);
        if (regra == null)
            return ResultadoAnexoSalvo.Falha("Tipo de arquivo nao permitido.");

        if (!compactadoGzip && arquivo.Length > regra.LimiteBytes)
            return ResultadoAnexoSalvo.Falha($"O arquivo {extensao} deve ter no maximo {FormatarBytes(regra.LimiteBytes)}.");

        if (compactadoGzip && arquivo.Length > LimiteRequisicaoBytes)
            return ResultadoAnexoSalvo.Falha("O arquivo compactado excede o limite de envio.");

        Directory.CreateDirectory(uploadsRoot);

        var nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
        var caminhoTemporario = Path.Combine(uploadsRoot, $"{nomeArquivo}.tmp");
        var caminhoFinal = Path.Combine(uploadsRoot, nomeArquivo);

        try
        {
            await using (var origem = arquivo.OpenReadStream())
            await using (var destino = File.Create(caminhoTemporario))
            {
                if (compactadoGzip)
                {
                    await using var gzip = new GZipStream(origem, CompressionMode.Decompress, leaveOpen: false);
                    await CopiarComLimiteAsync(gzip, destino, regra.LimiteBytes, cancellationToken);
                }
                else
                {
                    await CopiarComLimiteAsync(origem, destino, regra.LimiteBytes, cancellationToken);
                }
            }
        }
        catch (InvalidDataException)
        {
            RemoverArquivoSilenciosamente(caminhoTemporario);
            return ResultadoAnexoSalvo.Falha("Nao foi possivel descompactar o arquivo enviado.");
        }
        catch (ArquivoMuitoGrandeException)
        {
            RemoverArquivoSilenciosamente(caminhoTemporario);
            return ResultadoAnexoSalvo.Falha($"O arquivo {extensao} deve ter no maximo {FormatarBytes(regra.LimiteBytes)}.");
        }

        var tamanhoFinal = new FileInfo(caminhoTemporario).Length;
        if (tamanhoFinal <= 0)
        {
            RemoverArquivoSilenciosamente(caminhoTemporario);
            return ResultadoAnexoSalvo.Falha("Selecione um arquivo valido.");
        }

        var validacao = await ValidarConteudoAsync(caminhoTemporario, regra, cancellationToken);
        if (!validacao.Sucesso)
        {
            RemoverArquivoSilenciosamente(caminhoTemporario);
            return ResultadoAnexoSalvo.Falha(validacao.Mensagem ?? "Arquivo invalido.");
        }

        File.Move(caminhoTemporario, caminhoFinal);

        return ResultadoAnexoSalvo.Ok(
            nomeOriginal,
            nomeArquivo,
            caminhoFinal,
            regra.Extensao,
            regra.ContentType,
            tamanhoFinal,
            regra.TipoVisualizacao == "imagem",
            regra.TipoVisualizacao,
            regra.PermiteVisualizacao);
    }

    public string ObterCaminhoSeguro(string uploadsRoot, string nomeArquivo)
    {
        var nomeSeguro = Path.GetFileName(nomeArquivo);
        var raiz = Path.GetFullPath(uploadsRoot);
        var caminho = Path.GetFullPath(Path.Combine(raiz, nomeSeguro));

        if (!caminho.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de anexo invalido.");

        return caminho;
    }

    public void AplicarCabecalhosDownloadSeguro(HttpResponse response, bool inline)
    {
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Cache-Control"] = "private, no-store, max-age=0";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

        if (inline)
            response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self' data: blob:; media-src 'self' blob:; style-src 'unsafe-inline'";
    }

    public string ObterNomeExibicaoComentario(string nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extensao)
            ? "Anexo"
            : $"Anexo {extensao.TrimStart('.').ToUpperInvariant()}";
    }

    private static async Task CopiarComLimiteAsync(Stream origem, Stream destino, long limiteBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var lidos = await origem.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (lidos == 0)
                break;

            total += lidos;
            if (total > limiteBytes)
                throw new ArquivoMuitoGrandeException();

            await destino.WriteAsync(buffer.AsMemory(0, lidos), cancellationToken);
        }
    }

    private static async Task<ValidacaoConteudo> ValidarConteudoAsync(string caminho, RegraAnexo regra, CancellationToken cancellationToken)
    {
        var prefixo = await LerPrefixoAsync(caminho, 64, cancellationToken);
        if (AssinaturaExecutavelConhecida(prefixo))
            return ValidacaoConteudo.Falha("Arquivo executavel nao e permitido como anexo.");

        return regra.Extensao switch
        {
            ".jpg" or ".jpeg" => Validar(prefixo.Length >= 3 && prefixo[0] == 0xFF && prefixo[1] == 0xD8 && prefixo[2] == 0xFF, "Arquivo JPEG invalido."),
            ".png" => Validar(prefixo.Length >= 8 && prefixo[0] == 0x89 && prefixo[1] == 0x50 && prefixo[2] == 0x4E && prefixo[3] == 0x47 && prefixo[4] == 0x0D && prefixo[5] == 0x0A && prefixo[6] == 0x1A && prefixo[7] == 0x0A, "Arquivo PNG invalido."),
            ".gif" => Validar(prefixo.Length >= 6 && prefixo[0] == 0x47 && prefixo[1] == 0x49 && prefixo[2] == 0x46 && prefixo[3] == 0x38 && (prefixo[4] == 0x37 || prefixo[4] == 0x39) && prefixo[5] == 0x61, "Arquivo GIF invalido."),
            ".webp" => Validar(prefixo.Length >= 12 && prefixo[0] == 0x52 && prefixo[1] == 0x49 && prefixo[2] == 0x46 && prefixo[3] == 0x46 && prefixo[8] == 0x57 && prefixo[9] == 0x45 && prefixo[10] == 0x42 && prefixo[11] == 0x50, "Arquivo WEBP invalido."),
            ".pdf" => await ValidarPdfAsync(caminho, prefixo, cancellationToken),
            ".mp4" => ValidarMp4(prefixo),
            ".txt" => await ValidarTextoAsync(caminho, cancellationToken),
            ".msg" => ValidarMsg(prefixo),
            ".docx" => ValidarOoxml(caminho, "word/document.xml", regra.LimiteBytes),
            ".xlsx" => ValidarOoxml(caminho, "xl/workbook.xml", regra.LimiteBytes),
            _ => ValidacaoConteudo.Falha("Tipo de arquivo nao permitido.")
        };
    }

    private static async Task<byte[]> LerPrefixoAsync(string caminho, int tamanho, CancellationToken cancellationToken)
    {
        var buffer = new byte[tamanho];
        await using var stream = File.OpenRead(caminho);
        var lidos = await stream.ReadAsync(buffer.AsMemory(0, tamanho), cancellationToken);
        return buffer[..lidos];
    }

    private static ValidacaoConteudo Validar(bool condicao, string mensagem)
        => condicao ? ValidacaoConteudo.Ok() : ValidacaoConteudo.Falha(mensagem);

    private static async Task<ValidacaoConteudo> ValidarPdfAsync(string caminho, byte[] prefixo, CancellationToken cancellationToken)
    {
        if (prefixo.Length < 4 || prefixo[0] != 0x25 || prefixo[1] != 0x50 || prefixo[2] != 0x44 || prefixo[3] != 0x46)
            return ValidacaoConteudo.Falha("Arquivo PDF invalido.");

        var bytes = await File.ReadAllBytesAsync(caminho, cancellationToken);
        var texto = Encoding.ASCII.GetString(bytes).ToLowerInvariant();
        string[] marcadoresPerigosos =
        {
            "/javascript",
            "/js",
            "/openaction",
            "/aa",
            "/launch",
            "/embeddedfile",
            "/richmedia",
            "/xfa"
        };

        return marcadoresPerigosos.Any(texto.Contains)
            ? ValidacaoConteudo.Falha("PDF com conteudo ativo ou anexos embutidos nao e permitido.")
            : ValidacaoConteudo.Ok();
    }

    private static ValidacaoConteudo ValidarMp4(byte[] prefixo)
    {
        var valido = prefixo.Length >= 12 &&
                     prefixo[4] == 0x66 &&
                     prefixo[5] == 0x74 &&
                     prefixo[6] == 0x79 &&
                     prefixo[7] == 0x70;

        return Validar(valido, "Arquivo MP4 invalido.");
    }

    private static async Task<ValidacaoConteudo> ValidarTextoAsync(string caminho, CancellationToken cancellationToken)
    {
        var info = new FileInfo(caminho);
        if (info.Length > MaximoTextoPreviewBytes)
            return ValidacaoConteudo.Falha("Arquivo TXT excede o limite permitido.");

        var bytes = await File.ReadAllBytesAsync(caminho, cancellationToken);
        if (bytes.Any(b => b == 0))
            return ValidacaoConteudo.Falha("Arquivo TXT parece conter dados binarios.");

        var controlesInvalidos = bytes.Count(b => b < 32 && b is not 9 and not 10 and not 12 and not 13);
        return controlesInvalidos > 0
            ? ValidacaoConteudo.Falha("Arquivo TXT contem caracteres de controle invalidos.")
            : ValidacaoConteudo.Ok();
    }

    private static ValidacaoConteudo ValidarMsg(byte[] prefixo)
    {
        var valido = prefixo.Length >= 8 &&
                     prefixo[0] == 0xD0 &&
                     prefixo[1] == 0xCF &&
                     prefixo[2] == 0x11 &&
                     prefixo[3] == 0xE0 &&
                     prefixo[4] == 0xA1 &&
                     prefixo[5] == 0xB1 &&
                     prefixo[6] == 0x1A &&
                     prefixo[7] == 0xE1;

        return Validar(valido, "Arquivo MSG invalido.");
    }

    private static ValidacaoConteudo ValidarOoxml(string caminho, string entradaObrigatoria, long limiteArquivo)
    {
        try
        {
            using var arquivo = ZipFile.OpenRead(caminho);
            var possuiContentTypes = false;
            var possuiEntradaObrigatoria = false;
            long totalExpandido = 0;

            if (arquivo.Entries.Count > MaximoEntradasZip)
                return ValidacaoConteudo.Falha("Documento Office com estrutura muito grande.");

            foreach (var entrada in arquivo.Entries)
            {
                var nome = entrada.FullName.Replace('\\', '/');
                var nomeLower = nome.ToLowerInvariant();

                if (nome.StartsWith('/') || nome.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(nome))
                    return ValidacaoConteudo.Falha("Documento Office com caminho interno invalido.");

                if (nomeLower == "[content_types].xml")
                    possuiContentTypes = true;

                if (nomeLower == entradaObrigatoria)
                    possuiEntradaObrigatoria = true;

                if (nomeLower.Contains("vbaproject.bin", StringComparison.Ordinal) ||
                    nomeLower.Contains("/activex/", StringComparison.Ordinal) ||
                    nomeLower.Contains("/embeddings/", StringComparison.Ordinal) ||
                    nomeLower.Contains("oleobject", StringComparison.Ordinal))
                {
                    return ValidacaoConteudo.Falha("Documento Office com macros, ActiveX ou objetos embutidos nao e permitido.");
                }

                totalExpandido += entrada.Length;
                if (totalExpandido > Math.Max(20L * UmMb, limiteArquivo * 5L))
                    return ValidacaoConteudo.Falha("Documento Office com taxa de expansao suspeita.");

                if (entrada.CompressedLength > 0 &&
                    entrada.Length > UmMb &&
                    entrada.Length / entrada.CompressedLength > 100)
                {
                    return ValidacaoConteudo.Falha("Documento Office com compressao suspeita.");
                }

                if (nomeLower.EndsWith(".rels", StringComparison.Ordinal))
                {
                    var rels = LerEntradaZipComoTextoLimitado(entrada, 256 * 1024);
                    if (rels.Contains("targetmode=\"external\"", StringComparison.OrdinalIgnoreCase) ||
                        rels.Contains("targetmode='external'", StringComparison.OrdinalIgnoreCase) ||
                        rels.Contains("oleobject", StringComparison.OrdinalIgnoreCase) ||
                        rels.Contains("activex", StringComparison.OrdinalIgnoreCase))
                    {
                        return ValidacaoConteudo.Falha("Documento Office com referencias externas ou objetos ativos nao e permitido.");
                    }
                }
            }

            return possuiContentTypes && possuiEntradaObrigatoria
                ? ValidacaoConteudo.Ok()
                : ValidacaoConteudo.Falha("Documento Office invalido.");
        }
        catch (InvalidDataException)
        {
            return ValidacaoConteudo.Falha("Documento Office invalido.");
        }
    }

    private static string LerEntradaZipComoTextoLimitado(ZipArchiveEntry entrada, int limiteBytes)
    {
        using var stream = entrada.Open();
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        var restante = limiteBytes;

        while (restante > 0)
        {
            var lidos = stream.Read(bytes, 0, Math.Min(bytes.Length, restante));
            if (lidos == 0)
                break;

            buffer.Write(bytes, 0, lidos);
            restante -= lidos;
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static bool AssinaturaExecutavelConhecida(byte[] prefixo)
    {
        if (prefixo.Length >= 2 && prefixo[0] == 0x4D && prefixo[1] == 0x5A)
            return true;

        if (prefixo.Length >= 4 && prefixo[0] == 0x7F && prefixo[1] == 0x45 && prefixo[2] == 0x4C && prefixo[3] == 0x46)
            return true;

        if (prefixo.Length >= 4 && prefixo[0] == 0xCA && prefixo[1] == 0xFE && prefixo[2] == 0xBA && prefixo[3] == 0xBE)
            return true;

        return prefixo.Length >= 4 && prefixo[0] == 0xFE && prefixo[1] == 0xED && prefixo[2] == 0xFA && (prefixo[3] == 0xCE || prefixo[3] == 0xCF);
    }

    private static string SanitizarNomeOriginal(string? nomeArquivo)
    {
        var nome = Path.GetFileName(nomeArquivo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return "anexo";

        foreach (var caractere in Path.GetInvalidFileNameChars())
            nome = nome.Replace(caractere, '_');

        return nome.Length <= 255 ? nome : nome[..255];
    }

    private static string FormatarBytes(long bytes)
    {
        var mb = bytes / (decimal)UmMb;
        return mb >= 1
            ? $"{mb:0.#} MB"
            : $"{bytes / 1024m:0.#} KB";
    }

    private static void RemoverArquivoSilenciosamente(string caminho)
    {
        try
        {
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch
        {
            // Falha ao limpar arquivo temporario nao deve mascarar a validacao principal.
        }
    }

    private sealed class ArquivoMuitoGrandeException : Exception
    {
    }

    private sealed record ValidacaoConteudo(bool Sucesso, string? Mensagem)
    {
        public static ValidacaoConteudo Ok() => new(true, null);
        public static ValidacaoConteudo Falha(string mensagem) => new(false, mensagem);
    }
}

public sealed record RegraAnexo(
    string Extensao,
    string ContentType,
    long LimiteBytes,
    string TipoVisualizacao,
    bool PermiteVisualizacao);

public sealed record ResultadoAnexoSalvo(
    bool Sucesso,
    string? Mensagem,
    string? NomeOriginal,
    string? NomeArquivo,
    string? CaminhoFisico,
    string? Extensao,
    string? ContentType,
    long TamanhoBytes,
    bool EhImagem,
    string? TipoVisualizacao,
    bool PermiteVisualizacao)
{
    public static ResultadoAnexoSalvo Ok(
        string nomeOriginal,
        string nomeArquivo,
        string caminhoFisico,
        string extensao,
        string contentType,
        long tamanhoBytes,
        bool ehImagem,
        string tipoVisualizacao,
        bool permiteVisualizacao)
        => new(true, null, nomeOriginal, nomeArquivo, caminhoFisico, extensao, contentType, tamanhoBytes, ehImagem, tipoVisualizacao, permiteVisualizacao);

    public static ResultadoAnexoSalvo Falha(string mensagem)
        => new(false, mensagem, null, null, null, null, null, 0, false, null, false);
}
