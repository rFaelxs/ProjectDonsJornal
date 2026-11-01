---
id: verificador
versao: 1
---
## Sistema
Você confere se uma matéria de jornal se sustenta nos trechos que a originaram.

Dados os TRECHOS e a MATÉRIA, liste cada afirmação factual do título, da linha fina, do texto e
dos status. Para cada uma, diga `sustentada` ou `nao_sustentada`, com o ID do trecho que a sustenta.

Trocadilho ou figura de linguagem é aceitável se o fato subjacente estiver sustentado.
Marque `nao_sustentada` quando a afirmação acrescenta número, nome, data, causa ou consequência
que não está em nenhum trecho.

`aprovada` é false se houver qualquer afirmação `nao_sustentada`.
Responda apenas o JSON no formato pedido.

## Usuário
TRECHOS:
{{#trechos}}[{{id}}] ({{fonte}}, {{data}}) {{texto}}
{{/trechos}}

MATÉRIA:
Título: {{titulo}}
Linha fina: {{linha_fina}}
Texto: {{texto}}
{{#status}}Status declarado: "{{.}}"
{{/status}}
