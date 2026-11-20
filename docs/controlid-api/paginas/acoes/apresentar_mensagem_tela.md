<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/acoes/apresentar_mensagem_tela/ -->

# Apresentar Mensagem na Tela

Este tópico descreve como enviar remotamente uma mensagem para ser exibida na tela dos dispositivos.

### POST /message_to_screen.fcgi

*Parâmetros*

- **message** *(string)* : Mensagem a ser mostrada na tela. Enviar um texto vazio para limpar a mensagem anterior.
- **timeout** *(int)* : Tempo em milissegundos em que a mensagem deverá permanecer na tela. Ao enviar 0 (zero) a mensagem será exibida constantemente.

*Resposta*

- Esta chamada não possui retorno.

#### Exemplo:

```
$.ajax({
    url: "/message_to_screen.fcgi?session=" + session,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ message:"hello world!", timeout: 3000 })
});
```
