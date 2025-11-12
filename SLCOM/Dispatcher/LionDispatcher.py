import sys,os
import pyodbc
import modules.Constants
from modules.DataTypes import ReceiptInfo

# Obter variáveis de ambiente
DB_SERVER = os.getenv('AA_DBSERVER')
DB_DATABASE = os.getenv('AA_DB_DATABASE')
DB_UID = os.getenv('AA_DB_UID')
DB_PWD = os.getenv('AA_DB_PWD')
DB_PORT = os.getenv('AA_DB_PORT')

# Log das variáveis de ambiente (mascarando senha para segurança)
print("=== DEBUG: Variáveis de Ambiente ===")
print(f"DB_SERVER: {DB_SERVER}")
print(f"DB_DATABASE: {DB_DATABASE}")
print(f"DB_UID: {DB_UID}")
print(f"DB_PWD: {'***' if DB_PWD else 'None'} (length: {len(DB_PWD) if DB_PWD else 0})")
print(f"DB_PORT: {DB_PORT}")
print("=" * 40)

# Construir connection string
cnxn_str = f"Driver={{ODBC Driver 17 for SQL Server}};PORT={DB_PORT};Server={DB_SERVER};Database={DB_DATABASE};UID={DB_UID};PWD={DB_PWD};"
# Log da connection string (mascarando senha)
cnxn_str_masked = f"Driver={{ODBC Driver 17 for SQL Server}};PORT={DB_PORT};Server={DB_SERVER};Database={DB_DATABASE};UID={DB_UID};PWD=***;"
print(f"=== DEBUG: Connection String (masked) ===")
print(cnxn_str_masked)
print("=" * 40)

print("=== DEBUG: Tentando conectar ao banco de dados ===")
conn = pyodbc.connect(cnxn_str, autocommit=False)
print("=== DEBUG: Conexão estabelecida com sucesso ===")

def saveTrxLog(aTrxInfos):
    print(f"=== DEBUG: saveTrxLog chamado com: {aTrxInfos} ===")
    iQuery = """
                INSERT INTO [TRANSACTION_LOG]
                    ([TRX_CODE]
                    ,[TRX_INFO]
                    ,[TRX_STATUS])
                VALUES
                    (?,?,'PENDING')
            """
    print(f"=== DEBUG: Executando INSERT na TRANSACTION_LOG ===")
    iCursor = conn.cursor()
    iCursor.execute(iQuery,aTrxInfos)
    print(f"=== DEBUG: INSERT executado, obtendo IDENTITY ===")
    iCursor.execute("select @@IDENTITY")
    rs = iCursor.fetchone()
    trx_id = rs[0]
    print(f"=== DEBUG: TRX_ID criado: {trx_id} ===")
    return trx_id

######################################
############## START MAIN ############
######################################

####################################
###### MAIN - RECEIPT EMAIL ########
####################################
def updateReceiptInTrx(receiptno, trxid):
    print(f"=== DEBUG: updateReceiptInTrx chamado - receiptno: {receiptno}, trxid: {trxid} ===")
    iQuery = """
                UPDATE RECEIPT
                    SET TRX_ID = ?
                WHERE RECEIPT_NO = ?
            """
    iCursor = conn.cursor()
    print(f"=== DEBUG: Executando UPDATE na tabela RECEIPT ===")
    iCursor.execute(iQuery, trxid, receiptno)
    print(f"=== DEBUG: UPDATE executado com sucesso ===")

selectQuery = """
                select	r.RECEIPT_NO,
                        o.PKId as ORDER_ID,
                        c.SOCIAL_NAME,
                        c.EMAIL,
                        o.NFE_KEY
                from    receipt r
                join    [order] o on o.PKId = r.ORDER_ID
                join    client c on c.PKId = o.CLIENT_ID
                where   trx_id is null
         """
print("=== DEBUG: Query SELECT preparada ===")
print(f"=== DEBUG: Query SQL: {selectQuery} ===")
cursor = conn.cursor();
print("=== DEBUG: Cursor criado ===")

try:
    print("=== DEBUG: Executando SELECT query ===")
    cursor.execute(selectQuery)
    print("=== DEBUG: SELECT executado, buscando resultados ===")
    rs = cursor.fetchall()
    record_count = len(rs)
    print(f"=== DEBUG: Total de registros encontrados: {record_count} ===")
    
    trxInfos = []
    for idx, r in enumerate(rs, 1):
        print(f"=== DEBUG: Processando registro {idx}/{record_count} ===")
        print(f"=== DEBUG: Dados do registro: RECEIPT_NO={r[0]}, ORDER_ID={r[1]}, SOCIAL_NAME={r[2]}, EMAIL={r[3]}, NFE_KEY={r[4]} ===")
        ri = ReceiptInfo(r[0],r[1],r[2],r[3],r[4])
        print(f"=== DEBUG: ReceiptInfo criado ===")
        print(ri.toJSON())
        tuple = (f"{modules.Constants.RECEIPT_EMAIL}", f"{ri.toJSON()}")
        print(f"=== DEBUG: Preparando para salvar TRX_LOG com TRX_CODE: {modules.Constants.RECEIPT_EMAIL} ===")
        i = saveTrxLog(tuple)
        print(f"=== DEBUG: TRX_LOG salvo com ID: {i} ===")
        print(f"Update {ri.receiptNo} with {i}")
        updateReceiptInTrx(ri.receiptNo, i)
        print(f"=== DEBUG: Registro {idx} processado com sucesso ===")
    
    print("=== DEBUG: Todos os registros processados, fazendo commit ===")
    conn.commit()
    print("=== DEBUG: Commit executado com sucesso ===")
except Exception as e:
    print("=== DEBUG: ERRO CAPTURADO ===")
    exc_type, exc_obj, exc_tb = sys.exc_info()
    fname = os.path.split(exc_tb.tb_frame.f_code.co_filename)[1]
    print(f"=== DEBUG: Tipo de exceção: {exc_type} ===")
    print(f"=== DEBUG: Arquivo: {fname} ===")
    print(f"=== DEBUG: Linha: {exc_tb.tb_lineno} ===")
    print(f"=== DEBUG: Mensagem de erro: {e} ===")
    print("=== DEBUG: Fazendo rollback ===")
    try:
        conn.rollback()
        print("=== DEBUG: Rollback executado ===")
    except Exception as rollback_error:
        print(f"=== DEBUG: Erro ao fazer rollback: {rollback_error} ===")
    print(exc_type, fname, exc_tb.tb_lineno, " - " , e)
finally:
    print("=== DEBUG: Fechando conexão ===")
    try:
        cursor.close()
        print("=== DEBUG: Cursor fechado ===")
    except:
        pass
    del conn
    print("=== DEBUG: Conexão deletada ===")

#############################################
###### MAIN -  SITE ORDER SENT EMAIL ########
#############################################
