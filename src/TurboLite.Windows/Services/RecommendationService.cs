namespace TurboLite.Windows.Services;

public sealed record DiagnosticRecommendation(string Title, string Detail, string Action, string Accent);

public sealed class RecommendationService
{
    private const long GiB = 1024L * 1024 * 1024;

    public IReadOnlyList<DiagnosticRecommendation> Build(
        double ramPercent, long diskFreeBytes, long diskTotalBytes, int processCount, string osName)
    {
        var items = new List<DiagnosticRecommendation>();

        if (ramPercent >= 90)
            items.Add(new("Pressão alta de memória",
                $"A RAM está em {ramPercent:0}%. Isso pode aumentar paginação e lentidão.",
                "Abra Processos, ordene por memória e feche somente aplicativos que você reconhece após salvar o trabalho.",
                "#EF4444"));
        else if (ramPercent >= 80)
            items.Add(new("Memória bastante ocupada",
                $"A RAM está em {ramPercent:0}%. Observe se há lentidão antes de alterar configurações.",
                "Confira os maiores consumidores no Gerenciador de Processos; não encerre processos do Windows.",
                "#FFC93C"));

        if (diskTotalBytes > 0 && (diskFreeBytes < 5 * GiB || (double)diskFreeBytes / diskTotalBytes < 0.08))
            items.Add(new("Pouquíssimo espaço livre",
                $"Restam {SystemDiagnosticsService.FormatBytes(diskFreeBytes)} no volume do sistema.",
                "Revise o armazenamento e a prévia de limpeza temporária antes de remover arquivos pessoais.",
                "#EF4444"));
        else if (diskTotalBytes > 0 && (diskFreeBytes < 15 * GiB || (double)diskFreeBytes / diskTotalBytes < 0.15))
            items.Add(new("Espaço livre reduzido",
                $"Restam {SystemDiagnosticsService.FormatBytes(diskFreeBytes)} no volume do sistema.",
                "Abra Armazenamento e avalie os arquivos temporários. Não apague pastas do Windows manualmente.",
                "#FFC93C"));

        if (processCount >= 180)
            items.Add(new("Muitos processos em execução",
                $"O Windows informou {processCount} processos. A quantidade, sozinha, não prova que exista um problema.",
                "Use a lista de processos para investigar consumo alto de RAM; não aplique encerramento em massa.",
                "#FFC93C"));

        if (osName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            items.Add(new("Verifique a cobertura de segurança do Windows",
                "O suporte padrão do Windows 10 terminou em 14/10/2025.",
                "Confirme se o PC recebe atualizações de segurança pelo ESU ou planeje uma migração compatível. Não desative o Windows Update.",
                "#FFC93C"));

        if (items.Count == 0)
            items.Add(new("Nenhum alerta crítico detectado",
                "As métricas atuais não ultrapassaram os limites de alerta do Turbo Lite.",
                "Evite desativar serviços aleatoriamente. Se o PC continuar lento, investigue a atividade e a saúde do disco.",
                "#5DE0A5"));

        return items;
    }
}
