using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CallStationApp.Services;

public class FotoGrupoUploadService
{
    public const string MensagemArquivoInvalido = "Arquivo de imagem inválido.";

    private const long TamanhoMaximoBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly IWebHostEnvironment _environment;

    public FotoGrupoUploadService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<bool> FotoGrupoValidaAsync(IFormFile arquivo)
    {
        return ExtensaoETamanhoValidos(arquivo) && await AssinaturaImagemValidaAsync(arquivo);
    }

    public async Task<string> SalvarFotoGrupoAsync(IFormFile arquivo, int identificadorNomeArquivo)
    {
        if (!await FotoGrupoValidaAsync(arquivo))
            throw new InvalidOperationException(MensagemArquivoInvalido);

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var pasta = Path.Combine(_environment.WebRootPath, "uploads", "grupos");
        Directory.CreateDirectory(pasta);

        var nomeArquivo = $"grupo-{identificadorNomeArquivo}-{Guid.NewGuid():N}{extensao}";
        var caminho = Path.Combine(pasta, nomeArquivo);

        await using var stream = new FileStream(caminho, FileMode.Create);
        await arquivo.CopyToAsync(stream);

        return $"/uploads/grupos/{nomeArquivo}";
    }

    public void RemoverFotoGrupoSeExistir(string? caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
            return;

        var nomeArquivo = Path.GetFileName(caminhoRelativo);
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return;

        var caminho = Path.Combine(_environment.WebRootPath, "uploads", "grupos", nomeArquivo);
        if (File.Exists(caminho))
            File.Delete(caminho);
    }

    private static bool ExtensaoETamanhoValidos(IFormFile arquivo)
    {
        var extensao = Path.GetExtension(arquivo.FileName);
        return arquivo.Length <= TamanhoMaximoBytes && ExtensoesPermitidas.Contains(extensao);
    }

    private static async Task<bool> AssinaturaImagemValidaAsync(IFormFile arquivo)
    {
        var buffer = new byte[12];
        await using var stream = arquivo.OpenReadStream();
        var bytesLidos = await stream.ReadAsync(buffer);
        if (bytesLidos < 12)
            return false;

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        return extensao switch
        {
            ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
            ".png" => buffer[0] == 0x89 &&
                      buffer[1] == 0x50 &&
                      buffer[2] == 0x4E &&
                      buffer[3] == 0x47 &&
                      buffer[4] == 0x0D &&
                      buffer[5] == 0x0A &&
                      buffer[6] == 0x1A &&
                      buffer[7] == 0x0A,
            ".webp" => buffer[0] == 0x52 &&
                       buffer[1] == 0x49 &&
                       buffer[2] == 0x46 &&
                       buffer[3] == 0x46 &&
                       buffer[8] == 0x57 &&
                       buffer[9] == 0x45 &&
                       buffer[10] == 0x42 &&
                       buffer[11] == 0x50,
            _ => false
        };
    }
}
