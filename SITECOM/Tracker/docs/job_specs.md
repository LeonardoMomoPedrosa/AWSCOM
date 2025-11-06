Você é um developer expert em C# e vai desenvolver um executavel para rodar em amazon linux e acessar recursos do AWS.
Necessario criar um job que irá rodar em EC2 via crontab ou similar.
Voce pode se basear em @dynamo_creation_instructions.md para mais informações.
Tambem pode se basear no projeto SITECOM/AvisemeEmailer neste mesmo workspace.
1 - Consultar o DynamoDB e pegar todos os registros existentes.
2 - Para cadaa registro fazer o seguinte:
2.1 - Analisar o rastreamento_json para verificar se a entrega já foi concluída (deixe em aberto irei detalhar depois como será a lógica para isso)
2.2 - Se a entrega consta como concluida, remover esse registro do DynamoDB
2.3 - Se a entrega aida não consta como concluida, faça uma nova consulta de rastreamento:
2.3.1 - Se via = C, consultar nos correios (verificar @rastreamento.md)
2.3.2 - Se o rastreamento retornado for diferente do que está armazenado no rastreamento_json:
2.3.2.1 - Chamar um serviço de agendamento de email https://lion.aquanimal.com.br/ajax/OrderStatusAjaxHandler.ashx?trx_tp=7%oid=OrderId%json=rastreamento_json_NOVO
2.3.2.2 - Caso a chamada acima seja sucesso, atualizar o registro no DynamoDB com o json novo
Inserir novos rastreamentos:
3 - Consultar o Secret Manager arn:aws:secretsmanager:us-east-1:615283740315:secret:prod/sqlserver/ecom-QABqVU pegar a conexão com banco SQL Server de dados do e-commerce
4 - Consultar a base do e-commerce com essa query
 select tc.orderid,c.via,c.track from tbtrackcontrol tc join tbcompra c on c.pkid = tc.orderid where tc.status=0
5 - Se retornar 1+ linhas, realizar o seguinte para cada linha retornada
6.1 - Se via = "C" (correios)
6.1.1 - Chamar a API de rastreio dos correios usando o campo "track" (verificar @rastreamento.md)
6.1.2 - Inserir os dados no Dynamo DB. O Json retornado deverá ir integralmente para o atributo rastreamento_json
6.1.3 - Atualizar a linha do tbTrackControl dessa forma: update tbtrackcontrol set status=1 where orderId = order id no loop
6.1.4 - Se os passos acima retornarem sucesso, chamar o serviço de agendamento de email do passo 2.3.2.1


