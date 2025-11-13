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

# Construir connection string
cnxn_str = f"Driver={{ODBC Driver 17 for SQL Server}};PORT={DB_PORT};Server={DB_SERVER};Database={DB_DATABASE};UID={DB_UID};PWD={DB_PWD};"
conn = pyodbc.connect(cnxn_str, autocommit=False)

def saveTrxLog(aTrxInfos):
    iQuery = """
                INSERT INTO [TRANSACTION_LOG]
                    ([TRX_CODE]
                    ,[TRX_INFO]
                    ,[TRX_STATUS])
                VALUES
                    (?,?,'PENDING')
            """
    iCursor = conn.cursor()
    iCursor.execute(iQuery,aTrxInfos)
    iCursor.execute("select @@IDENTITY")
    rs = iCursor.fetchone()
    trx_id = rs[0]
    return trx_id

######################################
############## START MAIN ############
######################################

####################################
###### MAIN - RECEIPT EMAIL ########
####################################
def updateReceiptInTrx(receiptno, trxid):
    iQuery = """
                UPDATE RECEIPT
                    SET TRX_ID = ?
                WHERE RECEIPT_NO = ?
            """
    iCursor = conn.cursor()
    iCursor.execute(iQuery, trxid, receiptno)

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
cursor = conn.cursor();

try:
    cursor.execute(selectQuery)
    rs = cursor.fetchall()
    record_count = len(rs)
    print(f"=== DEBUG: Total de registros encontrados: {record_count} ===")
    
    trxInfos = []
    for idx, r in enumerate(rs, 1):
        ri = ReceiptInfo(r[0],r[1],r[2],r[3],r[4])
        tuple = (f"{modules.Constants.RECEIPT_EMAIL}", f"{ri.toJSON()}")
        i = saveTrxLog(tuple)
        print(f"Update {ri.receiptNo} with {i}")
        updateReceiptInTrx(ri.receiptNo, i)
        
    conn.commit()
except Exception as e:
    exc_type, exc_obj, exc_tb = sys.exc_info()
    fname = os.path.split(exc_tb.tb_frame.f_code.co_filename)[1]
    print(exc_type, fname, exc_tb.tb_lineno, " - " , e)
    try:
        conn.rollback()
    except:
        pass
finally:
    try:
        cursor.close()
    except:
        pass
    del conn

#############################################
###### MAIN -  SITE ORDER SENT EMAIL ########
#############################################
