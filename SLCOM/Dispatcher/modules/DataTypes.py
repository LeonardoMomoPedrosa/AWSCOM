import json

class ReceiptInfo:
    def __init__(self,
                 aReceiptNo, 
                 aOrderId, 
                 aSocialName,
                 aEmail,
                 aNfeKey):
        self.receiptNo = aReceiptNo
        self.orderId = aOrderId
        self.socialName = aSocialName
        self.email = aEmail
        self.nfeKey = aNfeKey

    def __str__(self):
        return f"{self.receiptNo} - {self.orderId} - {self.socialName} - {self.email} - {self.nfeKey}"

    def toJSON(self):
        return json.dumps(self, default=lambda o: o.__dict__, 
            sort_keys=True, indent=4)

class TrxLogInfo:
    def __init__(self, aTrxCode, aTrxInfo):
        self.trxCode = aTrxCode
        self.trxInfo = aTrxInfo

    def __str__(self):
        return f"{self.trxCode} - {self.trxInfo}"