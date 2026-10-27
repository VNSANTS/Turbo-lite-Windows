# Pesquisa técnica: otimização do Windows para o Turbo Lite

Data da pesquisa: 27/10/2026
Hardware-alvo inicial: Windows 10, Intel Core i5 de primeira geração, 8 GB de RAM e HD mecânico.

## Resumo executivo

A abordagem mais útil não é encerrar processos indiscriminadamente. O Turbo Lite deve separar quatro ações diferentes:
1. Diagnosticar: descobrir o que está consumindo CPU, RAM, disco e rede.
2. Encerrar processo: interromper um programa específico agora; pode perder trabalho não salvo e o programa pode reabrir automaticamente.
3. Desativar inicialização automática: impedir que um aplicativo comum inicie junto com o Windows, sem desinstalá-lo.
4. Configurar o Windows: mudar uma opção documentada, com explicação de impacto e possibilidade de restauração quando disponível.

## Projetos de referência

### 1. Chris Titus Tech WinUtil
Repo: https://github.com/ChrisTitusTech/winutil
Docs: https://winutil.christitus.com/

Ferramenta PowerShell de código aberto com instalação de aplicativos, ajustes, correções, configurações e opções de debloat. Documenta presets Minimal, Standard e Advanced. Exige privilégios administrativos para tarefas de sistema. É uma boa referência para agrupar ações em categorias e permitir seleção explícita; não devemos executar seu script remoto automaticamente nem copiar tweaks sem revisão.

### 2. Sophia Script for Windows
Repo: https://github.com/farag2/Sophia-Script-for-Windows
Releases: https://github.com/farag2/Sophia-Script-for-Windows/releases

Projeto PowerShell para Windows 10 e 11 com mais de 150 funções, incluindo privacidade, segurança, personalização e remoção seletiva de aplicativos. O projeto declara que cada ajuste tem função para restaurar o padrão. É referência para implementar ações modulares e reversíveis. A release consultada lista pacotes Windows 10 e Windows 11 em 11/10/2026. Cada função ainda precisa de revisão de compatibilidade antes de ser adaptada.

### 3. AtlasOS
Repo: https://github.com/Atlas-OS/Atlas
Docs: https://docs.atlasos.net/getting-started/install/install-playbook/

Modifica a configuração do Windows usando um Playbook. O projeto é útil como referência de transparência e foco em redução de componentes, mas a versão atual removeu suporte ao Windows 10. A instalação exige reinstalação limpa e o próprio projeto avisa que remover o Atlas requer reinstalar o Windows. Portanto, não será dependência nem caminho recomendado para este PC.

### 4. Microsoft Sysinternals Process Explorer
Página oficial: https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer

Ferramenta de diagnóstico avançado de processos, DLLs e handles. Serve como referência para mostrar processo, caminho executável, processo pai e assinatura quando disponível. O Turbo Lite não deve tentar reproduzir todos os recursos avançados no primeiro módulo.

### 5. Microsoft Sysinternals Autoruns
Página oficial: https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns

Lista locais de inicialização automática, incluindo logon, tarefas agendadas, serviços e outros pontos de extensão. Referência para a futura tela de inicialização. Precisamos distinguir inicialização de aplicativos do usuário de componentes do sistema.

### 6. Windows Task Manager / Startup Apps
Documentação: https://support.microsoft.com/en-us/windows

O Gerenciador de Tarefas do Windows permite inspecionar consumo de recursos e impacto de inicialização. Para ações básicas, é a fonte nativa mais segura; o Turbo Lite deve complementar com explicações e histórico, não ocultar o que o Windows já informa.

## Prioridades técnicas para o hardware-alvo

1. Medir primeiro: amostrar CPU, memória disponível/comprometida, atividade de disco e processos por consumo. Não afirmar que um processo é inútil só pelo nome.
2. Inicialização: listar entradas comuns de inicialização do usuário, com nome, comando, caminho, editor quando identificável e localização da entrada. Permitir desativação reversível, sem excluir arquivos.
3. Processos em execução: listar nome, PID, CPU aproximada, memória de trabalho, caminho e usuário quando disponível. Botão Encerrar deve ter confirmação, aviso sobre perda de dados e proteção contra processos críticos do Windows e do próprio Turbo Lite. Não oferecer encerrar todos.
4. Aplicativos instalados: inventariar antes de sugerir remoção. Diferenciar aplicativo de usuário, app re-instalável e componente/provisionamento do sistema. Exigir seleção individual.
5. Arquivos temporários: listar categorias e tamanho estimado; não apagar Downloads, documentos, perfis de navegador ou pastas arbitrárias. Não remover arquivos em uso.
6. Energia: exibir planos existentes e permitir escolher um plano suportado; não inventar planos que o hardware/Windows não oferece.
7. Privacidade: ajustes individualizados, com estado atual, efeito esperado, risco e reversão. Não desligar Defender, firewall, UAC, Windows Update ou mitigações de CPU como padrão.
8. Serviços e tarefas agendadas: inicialmente apenas leitura e explicação. Não desabilitar serviços em massa nem classificar um serviço como dispensável sem regra específica e compatível com a versão do Windows.
9. Restauração e auditoria: guardar estado anterior, registrar data, usuário, comando/alteração e resultado. Criar ponto de restauração quando disponível e autorizado; isso não substitui backup e não garante reverter toda mudança.
10. Comparação antes/depois: medir com o mesmo intervalo e deixar claro que resultados variam com uso, drivers, temperatura, disco e atualizações.

## O que significa encerrar processo

Encerrar um processo pode liberar RAM ou CPU naquele momento, mas não remove o aplicativo, não impede que ele inicie novamente, pode perder dados não salvos, pode quebrar recursos se for um processo de driver, segurança ou sistema, e não é necessariamente uma otimização permanente.

A prioridade é identificar o causador do gargalo. Em um computador com HD mecânico, uso de disco próximo de 100% pode ser o principal problema; desativar apps de inicialização pode ajudar, mas não transforma o HD em SSD. Não prometer ganhos fixos de FPS ou memória sem testes.

## Arquitetura sugerida

- Diagnostics: leitura e amostragem de recursos.
- StartupManager: inventário de inicialização e desativação de entradas suportadas.
- ProcessManager: inspeção e encerramento individual protegido.
- Cleanup: análise de categorias temporárias e exclusão seletiva.
- AppInventory: listagem e remoção seletiva com aviso.
- Tweaks: catálogo de configurações com estado atual, risco, requisito, aplicação e restauração.
- AuditLog: histórico local legível.
- Recovery: backups de configurações e pontos de restauração quando disponíveis.

Cada ação deve ter: ID estável, sistema/versão compatível, pré-condições, nível de risco, estado antes/depois, modo de simulação quando viável, confirmação explícita e resultado auditável.

## Segurança e suporte do Windows

A Microsoft informa que o suporte padrão do Windows 10 terminou em 14/10/2025; opções de atualização de segurança estendida podem depender da elegibilidade e inscrição. Otimizar não substitui atualizações de segurança.
Fonte: https://support.microsoft.com/pt-br/windows/deployment/updates-lifecycle/windows-10-support-has-ended-on-october-14-2025

## Fontes oficiais consultadas

- WinUtil: https://github.com/ChrisTitusTech/winutil
- Sophia Script: https://github.com/farag2/Sophia-Script-for-Windows
- AtlasOS: https://github.com/Atlas-OS/Atlas
- Process Explorer: https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer
- Autoruns: https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns
- Ciclo de vida do Windows 10: https://support.microsoft.com/pt-br/windows/deployment/updates-lifecycle/windows-10-support-has-ended-on-october-14-2025

## Decisão para o Turbo Lite

Implementar primeiro diagnósticos confiáveis e um gerenciador de inicialização; depois um gerenciador de processos com proteções. Em seguida, limpeza seletiva e tweaks individualizados. Manter serviços críticos e recursos de segurança intactos por padrão. Não incorporar scripts externos em bloco nem executar código remoto sem inspeção.
