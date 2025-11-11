import sys,os
import pyodbc
import modules.Constants
from modules.DataTypes import ReceiptInfo

#cnxn_str = ("Driver={ODBC Driver 17 for SQL Server};PORT=1433;Server=aadbcloud.cu9zlyfmg2ii.us-east-1.rds.amazonaws.com;Database=SL4AAProd;UID=admin;PWD=Qgmfl123!;")
cnxn_str = ("Driver={ODBC Driver 17 for SQL Server};PORT=1433;Server=aadbcloud.cu9zlyfmg2ii.us-east-1.rds.amazonaws.com;Database=SL4AAProd;UID=Admin;PWD=Qgmfl123!;")
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
    return rs[0]

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
    trxInfos = []
    for r in rs:
        ri = ReceiptInfo(r[0],r[1],r[2],r[3],r[4])
        print (ri.toJSON())
        tuple = (f"{modules.Constants.RECEIPT_EMAIL}", f"{ri.toJSON()}")
        i = saveTrxLog(tuple)
        print (f"Update {ri.receiptNo} with {i}")
        updateReceiptInTrx(ri.receiptNo, i)
        
    conn.commit()
except Exception as e:
    exc_type, exc_obj, exc_tb = sys.exc_info()
    fname = os.path.split(exc_tb.tb_frame.f_code.co_filename)[1]
    print(exc_type, fname, exc_tb.tb_lineno, " - " , e)
finally:
    del conn

#############################################
###### MAIN -  SITE ORDER SENT EMAIL ########
#############################################
