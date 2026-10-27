# Turbo Lite Windows

Aplicativo Windows para diagnóstico e otimização controlada de computadores modestos, com foco inicial em Windows 10 x64.

## Objetivos

- Dashboard de CPU, memória, armazenamento e sistema operacional.
- Perfis de otimização explicáveis e reversíveis.
- Limpeza seletiva de arquivos temporários.
- Gerenciamento de inicialização e serviços com salvaguardas.
- Debloat seletivo, sem remoção automática indiscriminada.
- Registro das alterações e diagnóstico antes/depois.

## Stack inicial

- C# / .NET 8
- WPF para interface nativa do Windows
- GitHub Actions para compilação automatizada

## Segurança por padrão

A primeira versão é somente diagnóstico: não altera configurações do Windows automaticamente. Cada operação de sistema será implementada como ação explícita, com explicação, validação de permissões, log e mecanismo de reversão quando tecnicamente possível. Não desative Defender, firewall, atualizações ou serviços essenciais em lote.

## Build

Abra `src/TurboLite.Windows/TurboLite.Windows.csproj` no Visual Studio ou compile com o SDK .NET 8 instalado:

```powershell
dotnet build src/TurboLite.Windows/TurboLite.Windows.csproj -c Release
```

O workflow de CI compila o projeto no Windows e publica os artefatos de build para inspeção.

## Módulos implementados

- Dashboard com leituras de CPU, RAM, processos, disco e recomendações.
- Gerenciador de processos com salvaguardas contra encerramento de processos críticos.
- Gerenciador de inicialização do usuário com cópia de segurança e restauração.
- Armazenamento e limpeza de arquivos temporários com prévia e confirmação.
- Histórico local de ações.
- Modo de energia: lista os planos instalados, permite aplicar Desempenho/Equilibrado/Economia quando disponíveis e restaurar o plano anterior salvo.

O modo de energia usa os planos nativos do Windows; não força overclock, não altera voltagem e não promete aumento de FPS. A disponibilidade de cada plano varia conforme o computador. Não há otimizações automáticas em segundo plano.

## Aviso sobre Windows 10

O suporte padrão do Windows 10 terminou em 14 de outubro de 2025. Verifique as opções de atualizações de segurança aplicáveis à sua edição.