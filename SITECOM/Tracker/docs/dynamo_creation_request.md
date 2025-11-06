preciso criar um DynamoDB para uma função especifica. 
Crie um arquivo nessa mesma pasta com as instruções.
A configuração do dynamo deverá ser feita pelo console e não via CLI nem CloudFormation.
A configuração deverá ser seguida de uma explicação teorica.
Por enquanto preciso apenas das instruções de como criar.
Vou consultar um banco de dados SQL para verificar se tem algum novo pedido com rastreamento (essa logica nao importa agora).
Uma vez que eu encontro esse novo pedido, vou chamar a API de rastreamento, pegar o JSON de retorno e guardar no Dynamo. A chave pode ser o proprio id da compra.
tambem tenho que identificar no registro, qual o tipo de envio feito (Correios, Jadlog, Gollog, etc)
Ao longo do dia, vou ler todos os registros do dynamo e chamar novamente o rastreamento e comparar com o registro armazenado. Se houve atualização, vou mandar uma comunicação para o cliente e atualizar o dynamo com o ultimo json recebido.
Quando eu identificar que o pedido foi entregue, irei remover o registro do dynamo.
 