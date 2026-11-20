<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/reconhecimento-facial/recomendacoes-fotos-instalacao/ -->

# Recomendações - Fotos e Instalação

Para o bom funcionamento do terminal de controle de acesso por reconhecimento facial, é importante que o cadastro de faces e as condições de instalação do equipamento sejam adequados. Esta página apresentará algumas recomendações para garantir uma excelente qualidade de cadastro e de identificação.

## Fotos para cadastro

No cadastro de fotos pela API para reconhecimento facial, são aceitas imagens de rosto nos formatos **JPG** ou **PNG**. É importante que as fotos utilizadas para cadastro contenham apenas um rosto e que este tenha uma boa iluminação, distribuída uniformemente.

As fotos devem ser de pessoas sem máscara e com todo o rosto claramente visível na foto. Além disso, as pessoas devem estar olhando diretamente para a câmera. O uso de máscara no cadastro é permitido mas não é recomendado. O uso de óculos de grau é perfeitamente aceitável, mas o uso de óculos escuros não é.

### Dimensões da imagem

Serão aceitas imagens com tamanho mínimo de 160x160 pixels. O limite superior é determinado pelo produto entre as dimensões *1920 x 1080 = 2.073.600 pixels*. Assim, por exemplo, imagens de 300x300, 600x600, 2000x1000 serão aceitas mas 4000x3000 não.

### Critérios de cadastro

Há ainda outros critérios que precisam ser observados em relação à qualidade da face contida na foto para cadastro:

- **Largura da face na foto** : A foto deve conter uma face que não esteja nem muito próxima nem muito distante. A região da face deve estar totalmente contida na foto, sendo mantida uma pequena margem em relação a cada um dos lados.
- **Centralização da face** : A região da face deve ocupar o centro da imagem tanto na direção vertical quanto na horizontal.
- **Centralização da pose** : A pessoa na foto deve estar com a face voltada para a câmera, olhando para a frente.
- **Nitidez da região da face** : A região da face deve estar nítida e bem iluminada.

## Instalação do produto

A correta identificação por reconhecimento facial depende da boa qualidade das imagens que a câmera do terminal consegue captar. Por isso, é fundamental observar as recomendações proporcionem as melhores condições de instalação.

### Cuidados com o ambiente

Para favorecer à captura de boas imagens para reconhecimento, é importante que o ambiente esteja bem iluminado. Contudo, não deve haver luz ou brilho de grande intensidade que atinja diretamente a câmera do equipamento.

É especialmente recomendado evitar que a luz do Sol incida diretamente sobre a câmera em qualquer momento de uso do equipamento. Muito brilho ou grandes contrastes de luz de fundo nas imagens captadas podem prejudicar a detecção de face ou a qualidade das informações extraídas para a identificação do usuário.

### Posicionamento do terminal

Recomenda-se que o equipamento seja instalado a aproximadamente 1,30m do chão, de forma a enquadrar corretamente rostos de pessoas de diferentes alturas. É possível ainda fazer o ajuste vertical da imagem captada pela câmera diretamente pelo menu do equipamento, seguindo por *Menu > Config. Faciais > Região de Interesse*.
