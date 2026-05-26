using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Services;

public class ChamadoStatusAutomationService : BackgroundService
{
    private static readonly StatusChamado[] StatusElegiveis =
    {
        StatusChamado.Aberto,
        StatusChamado.EmAndamento,
        StatusChamado.Reaberto
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChamadoStatusAutomationService> _logger;

    public ChamadoStatusAutomationService(IServiceScopeFactory scopeFactory, ILogger<ChamadoStatusAutomationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarPendenciasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar automacao de status dos chamados.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    private async Task ProcessarPendenciasAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var elegiveis = await (
            from chamado in context.Chamados.AsNoTracking()
            join configuracao in context.GruposConfiguracoes.AsNoTracking()
                on chamado.GrupoId equals configuracao.GrupoId
            where chamado.PrazoConclusao.HasValue &&
                  configuracao.AutomatizarPendentePorPrazoConclusao &&
                  configuracao.HorasAposVencimentoParaPendente.HasValue &&
                  StatusElegiveis.Contains(chamado.Status) &&
                  chamado.Status != StatusChamado.Excluido &&
                  chamado.PrazoConclusao.Value.AddHours(configuracao.HorasAposVencimentoParaPendente.Value) <= agora
            orderby chamado.PrazoConclusao
            select new
            {
                chamado.Id,
                chamado.GrupoId,
                chamado.Status
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        if (elegiveis.Count == 0)
            return;

        var ids = elegiveis.Select(x => x.Id).ToList();
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var chamados = await context.Chamados
                    .Where(c => ids.Contains(c.Id))
                    .ToListAsync(cancellationToken);

                if (chamados.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var mapaStatusAnterior = elegiveis.ToDictionary(x => x.Id, x => x.Status);
                var agoraInterno = DateTime.UtcNow;

                foreach (var chamado in chamados)
                {
                    chamado.Status = StatusChamado.EmAtraso;

                    context.HistoricoStatusChamados.Add(new HistoricoStatusChamado
                    {
                        ChamadoId = chamado.Id,
                        StatusAnterior = Enum.Parse<StatusAnteriorChamado>(mapaStatusAnterior[chamado.Id].ToString()),
                        StatusNovo = StatusNovoChamado.EmAtraso,
                        UsuarioId = null,
                        OrigemAutomatica = true,
                        DescricaoOrigem = "Mudanca automatica do sistema por prazo de conclusao excedido",
                        DataTransicao = agoraInterno
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
