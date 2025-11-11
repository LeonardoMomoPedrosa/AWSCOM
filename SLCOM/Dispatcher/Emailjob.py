import pyodbc
import os
import modules.Constants
from email.message import EmailMessage
from modules.DataTypes import ReceiptInfo
from types import SimpleNamespace
from string import Template
import json
import ssl
import boto3
from botocore.exceptions import ClientError

# Obter variáveis de ambiente
DB_SERVER = os.getenv('AA_DBSERVER')
DB_DATABASE = os.getenv('AA_DB_DATABASE')
DB_UID = os.getenv('AA_DB_UID')
DB_PWD = os.getenv('AA_DB_PWD')
DB_PORT = os.getenv('AA_DB_PORT')

AWS_REGION = os.getenv('AWS_REGION')
SES_FROM_EMAIL = os.getenv('SES_FROM_EMAIL', 'aquanimal@aquanimal.com.br')
SES_CC_EMAIL = os.getenv('SES_CC_EMAIL', 'aquanimal@aquanimal.com.br')
SES_BCC_EMAIL = os.getenv('SES_BCC_EMAIL', 'pedrosa.leonardo@gmail.com')

# Construir connection string
cnxn_str = f"Driver={{ODBC Driver 17 for SQL Server}};PORT={DB_PORT};Server={DB_SERVER};Database={DB_DATABASE};UID={DB_UID};PWD={DB_PWD};"
conn = pyodbc.connect(cnxn_str, autocommit=False)

def trxSuccess(aTrxId):
    iQuery = "UPDATE TRANSACTION_LOG SET TRX_STATUS = 'PROCESSED' WHERE TRX_ID = ?;"
    iCursor = conn.cursor()
    iCursor.execute(iQuery, int(aTrxId))

def send_mail2(to_email, subject, message, cci):
    """Envia email usando AWS SES SDK (boto3)"""
    ses_client = boto3.client('ses', region_name=AWS_REGION)
    
    to_addresses = [to_email]
    cc_addresses = [SES_CC_EMAIL] if cci == 1 else []
    bcc_addresses = [SES_BCC_EMAIL] if SES_BCC_EMAIL else []
    
    print(f"Enviando email para {to_email}")
    
    try:
        response = ses_client.send_email(
            Source=SES_FROM_EMAIL,
            Destination={
                'ToAddresses': to_addresses,
                'CcAddresses': cc_addresses,
                'BccAddresses': bcc_addresses
            },
            Message={
                'Subject': {'Data': subject, 'Charset': 'UTF-8'},
                'Body': {'Html': {'Data': message, 'Charset': 'UTF-8'}}
            }
        )
        print(f"✅ Email enviado! MessageId: {response['MessageId']}")
        return True
    except ClientError as e:
        print(f"❌ Error sending email: {e.response['Error']['Message']}")
        return False
    except Exception as e:
        print(f"❌ Error sending email: {e}")
        return False

def send_mail(to_email,
               subject,
               message):
    send_mail2(to_email, subject, message, 0)

########################################################
#RECEIPT_EMAIL - Sent e-mail for new NF in LION 
########################################################
selectQuery = """
                select TRX_INFO,TRX_ID
                from TRANSACTION_LOG 
                WHERE TRX_CODE=? 
                AND TRX_STATUS='PENDING';
         """
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.RECEIPT_EMAIL)
rs = cursor.fetchall()

t = Template("""
<html>
<body>
<img src="https://aquanimal.com.br/images/mailogo.jpg" style="width: 200px"><br>
Ola $nome,
<br><br>
Uma nova Nota Fiscal foi gerada para você, seu pedido poderá ser enviado ainda hoje.
<br>
Nota Fiscal: $nf<br>
Chave de Acesso: $key<br>
<br>
Agradecemos o seu pedido e esperamos atendê-lo novamente em breve.<br>
Equipe Aquanimal.
</body>
</html>
        """);
print("Starting lion receipt email process")
for r in rs:
    receiptInfo = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail(receiptInfo.email,"Nota Fiscal",t.substitute(nome=receiptInfo.socialName,
                                                                      nf=receiptInfo.receiptNo,
                                                                      key=receiptInfo.nfeKey))
    print(f"Processing Lion Receipt Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()

########################################################
### SITE NEW ORDER EMAIL
########################################################
selectQuery = """
                select TRX_INFO,TRX_ID
                from TRANSACTION_LOG
                WHERE TRX_CODE=?
                AND TRX_STATUS='PENDING';
         """
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.SITE_0_EMAIL)
rs = cursor.fetchall()

t = Template("""
<html><body><img src="https://aquanimal.com.br/images/mailogo.jpg"><br><br><font face="Verdana,Arial" size=2><b>Pedido $ped - Recebido com Sucesso.</b><br><br>Prezado Cliente,<br><br>Gostaríamos de informar que sua compra já foi recebida com sucesso e será processada em breve.<br>Lembre-se que, de acordo com as instruções do -como comprar- em nosso site, o prazo para envio pode variar entre 5 a 15 dias. Itens mais populares e que tem uma boa saída são mantidos em estoque a pronta entrega e enviados mais rápido. Itens com menor giro trabalhamos com o estoque do fornecedor, por isso podem demorar mais tempo.<br>Animais de água doce normalmente tem alto estoque e são enviados em até uma semana, entretanto os marinhos ou doces que necessitam de cuidado especial como quarentena diferenciada para envio, podem demorar até 15 dias. O mesmo se aplica para os casos de cliente retira! Quanto ao envio, a sua encomenda será entregue no endereço cadastrado por você, e o prazo de transporte pode variar de acordo com a sua cidade, mas não se preocupe! Nossas embalagens seguem um protocolo de acordo com o tempo da viagem para que os animais cheguem em completa segurança. Logo após o envio do seu pedido você recebe um e-mail dizendo que ele foi despachado, e assim, pode se programar melhor para recebe-lo.<br><br><a href="https://aquanimal.com.br/Orders">Clique aqui acessar os dados de depósito ou para acompanhar o seu pedido.</a><br><br><br>Agradecemos por comprar conosco!<br>Aquanimal</font></body></html>
        """);
print("Starting site 0-email process")
for r in rs:
    receiptInfo = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail2(receiptInfo.email,"Recebemos o seu pedido.",t.substitute(nome=receiptInfo.socialName,ped=receiptInfo.orderId),1)
    print(f"Processing Site 0-mail Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()



########################################################
### SITE SENT ORDER EMAIL
########################################################
selectQuery = """
                select TRX_INFO,TRX_ID
                from TRANSACTION_LOG
                WHERE TRX_CODE=?
                AND TRX_STATUS='PENDING';
         """
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.SITE_V_EMAIL)
rs = cursor.fetchall()

t = Template("""
<html>
<body>
<img src="https://aquanimal.com.br/images/mailogo.jpg" style="width: 200px"><br>
<font face="Verdana,Arial" size=2><br>
Ol&aacute; $nome,<br><br>
Informamos que seu pedido $ped foi enviado na data de hoje.<br><br>
Escolhemos sempre a melhor maneira de envio para a sua cidade!<br><br>
Para envios via <b>JADLOG</b> o rastreio poder&aacute; ser feito hoje ap&oacute;s as 20h, direto no site da transportadora www.jadlog.com.br, com seu CPF.<br><br>
Para envios pela transportadora <b>BUSLOG</b>, voc&ecirc; receber&aacute; via whatsapp o <b>n&uacute;mero da encomenda</b> para rastreio direto no site https://envio.buslog.com.br/rastreamento - Voc&ecirc; tamb&eacute;m poder&aacute; usar o seu CPF.<br><br>
Se voc&ecirc; reside na regi&atilde;o Norte, Nordeste ou algumas cidades do Centro Oeste ou escolheu Retira Aeroporto, a sua carga foi enviada via <b>GOLLOG</b>. No final do dia, voc&ecirc; receber&aacute; via whatsapp o <b>n&uacute;mero operacional</b> para rastreio direto no site - https://servicos.gollog.com.br/app/site/tracking<br><br>
Cargas enviadas via <b>JADLOG</b> e <b>BUSLOG</b> ser&atilde;o entregues no endere&ccedil;o indicado, ou retirados na transportadora, conforme acordado com a Aquanimal.<br><br>
Cargas enviadas via aeroporto, dever&atilde;o ser retiradas no <b>Galp&atilde;o da GOLLOG</b> no aeroporto escolhido por voc&ecirc;.<br><br>
Caso o seu pedido seja apenas de produtos, enviamos via <b>CORREIOS</b> e voc&ecirc; poder&aacute; verificar em nosso site, atrav&eacute;s do link <b>Meus Pedidos</b> o c&oacute;digo de rastreamento do seu PAC.<br><br>
Fazemos embalagem para que os peixes fiquem confort&aacute;veis durante a viagem, a maioria dos envios leva at&eacute; 3 dias, caso n&atilde;o ocorra neste prazo, por favor entre em contato, lembramos que as trasnportadoras n&atilde;o fazem entregas nos finais de semana nem feriados.<br><br>
Abaixo, nossas instru&ccedil;&otilde;es de como receber os peixes novos no seu aqu&aacute;rio, tamb&eacute;m enviamos as mesmas instru&ccedil;&otilde;es em uma cartinha dentro da sua encomenda.<br><br>
NUNCA COLOQUE A &Aacute;GUA DO AQU&Aacute;RIO NO SAQUINHO COM O PEIXE<br><br>
1 - Apague a luz do aqu&aacute;rio para reduzir o estresse do peixe.<br>
2 - Deixe o saco fechado boiando na &aacute;gua do aqu&aacute;rio por 10 minutos para igualar a temperatura.<br>
3 - Corte o saquinho e descarte a &aacute;gua fora, em seguida, coloque o peixe direto no aqu&aacute;rio.<br>
4 - Acenda a luz novamente em algumas horas.<br><br>
Para saber mais, acesse http://blog.aquanimal.com.br/2016/05/aclimatizando-seu-novo-peixe-de-agua.html<br><br>
Obrigada por comprar conosco!<br><br>
Aquanimal<br>
www.aquanimal.com.br<br>
Whatsapp 11 9 9221-2363
</body>
</html>
        """);
print("Starting site v-email process")
for r in rs:
    receiptInfo = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail(receiptInfo.email,"Pedido Enviado!",t.substitute(nome=receiptInfo.socialName,ped=receiptInfo.orderId))
    print(f"Processing Site V-mail Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()

########################################################
### SITE READY TO PICKUP  EMAIL
########################################################
selectQuery = """
                select TRX_INFO,TRX_ID
                from TRANSACTION_LOG
                WHERE TRX_CODE=?
                AND TRX_STATUS='PENDING';
         """
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.SITE_R_EMAIL)
rs = cursor.fetchall()

t = Template("""
Ol&aacute; $nome,<br><br>
<b>Agradecemos por comprar conosco.</b><br><br>
Gostaríamos de informar que o seu pedido $ped já está pronto para ser retirado em nossa loja.<br><br>
Nosso endereço se encontra no rodap&eacute; de nosso site.<br><br>
Aquanimal
        """);
print("Starting site r-email process")
for r in rs:
    receiptInfo = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail(receiptInfo.email,"Pedido pronto para retirada!",t.substitute(nome=receiptInfo.socialName,ped=receiptInfo.orderId))
    print(f"Processing Site r-mail Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()

########################################################
### SITE CC NOT AUTHORIZED  EMAIL
########################################################
selectQuery = """
                select TRX_INFO,TRX_ID
                from TRANSACTION_LOG
                WHERE TRX_CODE=?
                AND TRX_STATUS='PENDING';
         """
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.SITE_N_EMAIL)
rs = cursor.fetchall()

t = Template("""
Ol&aacute; $nome,<br><br>
O sue pedido $ped n&atilde;o pode ser conclu&iacute;do.<br>
A operadora do seu cart&atilde;o de cr&eacute;dito n&atilde;o autorizou a transa&ccedil;&atilde;o.<br>
Entre em contato com sua administradora e nos retorne.<br>
Se tiver d&uacute;vidas, entre em contato com o nosso e-mail <a href="mailto:aquanimal@aquanimal.com.br">aquanimal@aquanimal.com.br</a>, ou pelo telefone.<br><br>
Atenciosamente.<br>Aquanimal</br>
        """);
print("Starting site n-email process")
for r in rs:
    receiptInfo = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail(receiptInfo.email,"Cartão não autorizado.",t.substitute(nome=receiptInfo.socialName,ped=receiptInfo.orderId))
    print(f"Processing Site n-mail Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()

##########################################################
### LOST EMAIL
##########################################################
cursor = conn.cursor()
cursor.execute(selectQuery, modules.Constants.SITE_6_EMAIL)
rs = cursor.fetchall()

t = Template("""
Segue sua senha tempor&aacute;ria: $senha<br>
Altere essa senha o mais r&aacute;pido poss&iacute;vel em nosso site, no link "Cadastro".
<br><br>
Atenciosamente. <br>Aquanimal
             """);
print("Starting site n-email process")
for r in rs:
    trxInfo  = json.loads(r[0], object_hook=lambda d: SimpleNamespace(**d))
    trxId = r[1]
    send_mail(trxInfo.email,"Reset de Senha",t.substitute(senha=trxInfo.socialName))
    print(f"Processing Site n-mail Trx Id {trxId}")
    trxSuccess(trxId)
    conn.commit()


