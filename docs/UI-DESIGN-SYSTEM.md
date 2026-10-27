# Turbo Lite Windows — UI Design System

Referência de implementação inspirada no fluxo de auditoria do UI/UX Pro Max para aplicações WPF desktop.

## Direção
- Produto: ferramenta utilitária de diagnóstico/otimização para Windows, especialmente PCs modestos.
- Padrão: dashboard operacional escuro, compacto, orientado a dados.
- Princípios: informação primeiro; não esconder avisos; ações destrutivas diferenciadas; feedback claro; não prometer ganhos de desempenho não medidos.
- Tipografia: Segoe UI nativa, com títulos fortes e metadados compactos.
- Movimento: mínimo; preservar responsividade em hardware antigo.

## Tokens
| Papel | Valor | Uso |
|---|---|---|
| Background | #080D18 | fundo da janela |
| Surface | #101A2B | cartões e painéis |
| Surface elevated | #17243A | controles e itens secundários |
| Border | #253650 | divisões discretas |
| Accent | #35D0FF | foco, links e destaque |
| Text | #F1F5F9 | texto principal |
| Muted | #94A3B8 | metadados |
| Success | #5DE0A5 | estado positivo |
| Warning | #F6C66A | avisos |
| Danger | #EF6B7B | ações destrutivas |

## Regras de interação
1. Botões precisam ter hover, pressed, disabled e foco de teclado visíveis.
2. Ações que podem interromper trabalho pedem confirmação explícita.
3. Nunca depender só da cor para comunicar estado: usar rótulos e texto explicativo.
4. Layout deve manter conteúdo legível em resoluções menores; evitar animações pesadas.
5. Métricas são aproximações quando a fonte de dados do Windows não oferece precisão maior.
6. Não apresentar recursos planejados como concluídos.
7. Otimização de RAM deve ser descrita como solicitação de redução do working set, não como limpeza garantida ou ganho permanente.

## Checklist de revisão
- [x] Paleta consistente em superfícies, bordas e destaque
- [x] Fonte nativa Segoe UI no dashboard
- [x] Estados de foco e hover para botões
- [x] Avisos explícitos para ações potencialmente arriscadas
- [ ] Validar visualmente em execução no Windows 10
- [ ] Validar escala em 100%, 125% e 150%
- [ ] Revisar todas as janelas secundárias com o mesmo sistema
