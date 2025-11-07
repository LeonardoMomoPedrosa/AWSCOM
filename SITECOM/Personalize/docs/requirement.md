você um especialista em desenvolvimento C# e tambem machine learning.
Quero implmentar um sistema de personalização para produtos no ecommerce.
Esse projeto "Personalize" é um executável em C# que vai rodar 1x por semana na crontab.
O objetivo desse projeto é criar listas de recomendações de produtos para clientes.
o processo vai fazer uma query no banco de dados e pegar os dados das vendas realizadas no periodo.
Na primeira execução, todo o historico será puxado.
Nas execuções semanais apenas o delta deverá ser considerado.
Voce deverá usar a tecnica SIMS (co-purchase) e escores (count/lift/cosine) com decaimento temporal.
as vendas estão localizadas na tabela tbCompra e os itens em tbProdutosCompra
aqui estão os campos relevantes

SELECT [PKIdUsuario]
      ,[PKId]
      ,[status]
      ,[idDados]
      ,[data]
      ,[dataMdSt]
  FROM [tbCompra]

USE [ecoanimal]
GO

SELECT [idUsuario]
      ,[idProduto]
      ,[quantidade]
      ,[PKId]
      ,[PKIdCompra]
      ,[preco]
      ,[nome]
      ,[sys_creation_date]
      ,[sys_update_date]
  FROM [tbProdutosCompra]

a extração pode ser em arquivo para facilitar o debug, ou in-memory, o que achar melhor.
Após extrair os dados, deverá ser feito o processamento deles e os registros deverão ser salvos na tabela dynamo-personalize do DynamoDB (veja como é feito no projeto Tracker nesse mesmo workspace).
a chave da tabela do Dynamo é o productId, e os dados que estão armazenado (atributo data) deverá conter os 5 top produtos a serem recomendados aos clientes.
Em cada processamento semanal, se o produto já existir no Dynamo, atualize os dados, se o produto não exisir, insira.