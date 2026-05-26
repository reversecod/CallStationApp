using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Services;

public class SlaPausaService
{
    private readonly AppDbContext _context;

    public SlaPausaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarTransicaoChamadoAsync(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado statusNovo,
        int usuarioId,
        DateTime agoraUtc,
        string? observacaoEntrada = null,
        string? observacaoSaida = null)
    {
        if (statusAnterior != StatusChamado.Pendente && statusNovo == StatusChamado.Pendente)
        {
            await AbrirPeriodoChamadoAsync(chamado.Id, usuarioId, agoraUtc, observacaoEntrada);
            if (chamado.PrazoConclusaoOperacional == null)
                chamado.PrazoConclusaoOperacional = chamado.PrazoConclusao;
            return;
        }

        if (statusAnterior == StatusChamado.Pendente && statusNovo != StatusChamado.Pendente)
        {
            var duracao = await FecharPeriodoChamadoAsync(chamado.Id, usuarioId, agoraUtc, observacaoSaida);
            if (duracao > TimeSpan.Zero)
                chamado.PrazoConclusaoOperacional = (chamado.PrazoConclusaoOperacional ?? chamado.PrazoConclusao)?.Add(duracao);
        }
    }

    public async Task RegistrarTransicaoCartaoAsync(
        CartaoTarefa cartao,
        StatusCartaoTarefa statusAnterior,
        StatusCartaoTarefa statusNovo,
        int usuarioId,
        DateTime agoraUtc,
        string? observacaoEntrada = null,
        string? observacaoSaida = null)
    {
        if (statusAnterior != StatusCartaoTarefa.Pendente && statusNovo == StatusCartaoTarefa.Pendente)
        {
            await AbrirPeriodoCartaoAsync(cartao.Id, usuarioId, agoraUtc, observacaoEntrada);
            if (cartao.DataVencimentoOperacional == null)
                cartao.DataVencimentoOperacional = cartao.DataVencimento;
            return;
        }

        if (statusAnterior == StatusCartaoTarefa.Pendente && statusNovo != StatusCartaoTarefa.Pendente)
        {
            var duracao = await FecharPeriodoCartaoAsync(cartao.Id, usuarioId, agoraUtc, observacaoSaida);
            if (duracao > TimeSpan.Zero)
                cartao.DataVencimentoOperacional = (cartao.DataVencimentoOperacional ?? cartao.DataVencimento)?.Add(duracao);
        }
    }

    private async Task AbrirPeriodoChamadoAsync(int chamadoId, int usuarioId, DateTime agoraUtc, string? observacao)
    {
        var existeAberto = await _context.ChamadosPeriodosPendentes
            .AnyAsync(p => p.ChamadoId == chamadoId && p.FimPendente == null);
        if (existeAberto) return;

        _context.ChamadosPeriodosPendentes.Add(new ChamadoPeriodoPendente
        {
            ChamadoId = chamadoId,
            InicioPendente = agoraUtc,
            ObservacaoEntrada = NormalizarObservacao(observacao),
            CriadoPorUsuarioId = usuarioId,
            CriadoEm = agoraUtc
        });
    }

    private async Task<TimeSpan> FecharPeriodoChamadoAsync(int chamadoId, int usuarioId, DateTime agoraUtc, string? observacao)
    {
        var periodo = await _context.ChamadosPeriodosPendentes
            .Where(p => p.ChamadoId == chamadoId && p.FimPendente == null)
            .OrderByDescending(p => p.InicioPendente)
            .FirstOrDefaultAsync();
        if (periodo == null) return TimeSpan.Zero;

        var duracao = agoraUtc - periodo.InicioPendente;
        if (duracao < TimeSpan.Zero) duracao = TimeSpan.Zero;

        periodo.FimPendente = agoraUtc;
        periodo.DuracaoSegundos = (long)duracao.TotalSeconds;
        periodo.ObservacaoSaida = NormalizarObservacao(observacao);
        periodo.FinalizadoPorUsuarioId = usuarioId;
        periodo.AtualizadoEm = agoraUtc;
        return duracao;
    }

    private async Task AbrirPeriodoCartaoAsync(int cartaoId, int usuarioId, DateTime agoraUtc, string? observacao)
    {
        var existeAberto = await _context.CartoesTarefasPeriodosPendentes
            .AnyAsync(p => p.CartaoTarefaId == cartaoId && p.FimPendente == null);
        if (existeAberto) return;

        _context.CartoesTarefasPeriodosPendentes.Add(new CartaoTarefaPeriodoPendente
        {
            CartaoTarefaId = cartaoId,
            InicioPendente = agoraUtc,
            ObservacaoEntrada = NormalizarObservacao(observacao),
            CriadoPorUsuarioId = usuarioId,
            CriadoEm = agoraUtc
        });
    }

    private async Task<TimeSpan> FecharPeriodoCartaoAsync(int cartaoId, int usuarioId, DateTime agoraUtc, string? observacao)
    {
        var periodo = await _context.CartoesTarefasPeriodosPendentes
            .Where(p => p.CartaoTarefaId == cartaoId && p.FimPendente == null)
            .OrderByDescending(p => p.InicioPendente)
            .FirstOrDefaultAsync();
        if (periodo == null) return TimeSpan.Zero;

        var duracao = agoraUtc - periodo.InicioPendente;
        if (duracao < TimeSpan.Zero) duracao = TimeSpan.Zero;

        periodo.FimPendente = agoraUtc;
        periodo.DuracaoSegundos = (long)duracao.TotalSeconds;
        periodo.ObservacaoSaida = NormalizarObservacao(observacao);
        periodo.FinalizadoPorUsuarioId = usuarioId;
        periodo.AtualizadoEm = agoraUtc;
        return duracao;
    }

    private static string? NormalizarObservacao(string? observacao)
    {
        var valor = observacao?.Trim();
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Length <= 500 ? valor : valor[..500];
    }
}
