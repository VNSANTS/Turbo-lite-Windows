# Turbo Lite Windows

Aplicativo Windows para diagnóstico e otimização controlada de computadores modestos, com foco inicial em Windows 10 x64.

## Direção do projeto

O escopo planejado inclui dashboard de hardware, perfis de otimização explicáveis e reversíveis, limpeza seletiva, gerenciamento de inicialização, registro das alterações e comparações de diagnóstico antes/depois. Esses itens são objetivos de evolução, não uma afirmação de que todos já estejam implementados. O estado atual está discriminado em **Módulos implementados** abaixo; gerenciamento de serviços e debloat seletivo ainda não são apresentados como recursos disponíveis.

## Stack inicial

- C# / .NET 8
- WPF para interface nativa do Windows
- GitHub Actions para compilação automatizada

## Segurança por padrão

O app combina diagnóstico somente leitura com ações manuais explícitas. Alterações de energia exigem confirmação e guardam o plano anterior; alterações de inicialização compatíveis usam backup e restauração; limpeza temporária mostra prévia e pede confirmação. O gerenciador de processos não encerra nada automaticamente e pede confirmação adicional para encerramento forçado. Não desative Defender, firewall, Windows Update, arquivo de paginação ou serviços essenciais em lote.

## Build

Abra `src/TurboLite.Windows/TurboLite.Windows.csproj` no Visual Studio ou compile com o SDK .NET 8 instalado:

```powershell
dotnet build src/TurboLite.Windows/TurboLite.Windows.csproj -c Release
```

O workflow de CI compila o projeto no Windows e publica os artefatos de build para inspeção.

## Módulos implementados

- Dashboard com leituras de CPU, RAM, processos, disco e recomendações.
- Gerenciador de processos com leitura aproximada de CPU por processo (amostragem de 0,5 s), memória e salvaguardas contra encerramento de processos críticos.
- Gerenciador de inicialização: entradas compatíveis da chave Run do usuário e atalhos .lnk da pasta pessoal podem ser desativados com backup e restaurados; outras origens são somente leitura.
- Armazenamento e limpeza de arquivos temporários com prévia e confirmação.
- Histórico local de ações.
- Modo de energia: lista os planos instalados, permite aplicar Desempenho/Equilibrado/Economia quando disponíveis e restaurar o plano anterior salvo.

O modo de energia usa os planos nativos que já existem no Windows; não cria planos ausentes, não força overclock, não altera voltagem e não promete aumento de FPS. A disponibilidade de cada plano varia conforme o computador. A leitura de CPU por processo é aproximada e pode exibir N/D quando o processo não permite consulta. Não há otimizações automáticas em segundo plano.

## Aviso sobre Windows 10

O suporte padrão do Windows 10 terminou em 14 de outubro de 2025. Verifique as opções de atualizações de segurança aplicáveis à sua edição.

## Direção visual e UX

A interface segue um sistema visual escuro para ferramenta de diagnóstico: superfícies em camadas, bordas discretas, ciano como acento de ação, hierarquia tipográfica consistente e estados de foco/hover visíveis. A revisão usa princípios do [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill): consistência de tokens, legibilidade, feedback claro e controles com estados acessíveis. O skill é uma referência de design; não é uma dependência de runtime do aplicativo.

