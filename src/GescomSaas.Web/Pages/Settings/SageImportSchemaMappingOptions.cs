namespace GescomSaas.Web.Pages.Settings;

public class SageImportSchemaMappingOptions
{
    public SageImportModuleMappingOptions Partners { get; set; } = new("F_COMPTET", "Code=CT_NUM\nLabel=CT_INTITULE\nEmail=CT_EMAIL\nPhone=CT_TELEPHONE\nVat=CT_IDENTIFIANT\nPaymentTerm=RG_CODE\nContact=CT_CONTACT\nAddress1=CT_ADRESSE\nAddress2=CT_COMPLEMENT\nPostalCode=CT_CODEPOSTAL\nCity=CT_VILLE\nCountry=CT_PAYS\nActive=ACTIF");
    public SageImportModuleMappingOptions ProductCategories { get; set; } = new("F_FAMILLE", "Code=FA_CODEFAMILLE\nLabel=FA_INTITULE");
    public SageImportModuleMappingOptions TaxCodes { get; set; } = new("F_TAXE", "Code=TA_CODE\nLabel=TA_INTITULE\nRate=TA_TAUX");
    public SageImportModuleMappingOptions PaymentTerms { get; set; } = new("F_REGLEMENT", "Code=RG_CODE\nLabel=RG_LIBELLE\nDueInDays=RG_NBJOUR");
    public SageImportModuleMappingOptions Warehouses { get; set; } = new("F_DEPOT", "Code=DE_NO\nLabel=DE_INTITULE");
    public SageImportModuleMappingOptions Products { get; set; } = new("F_ARTICLE", "Code=AR_REF\nLabel=AR_DESIGN\nDescription=AR_DESIGN2\nPurchasePrice=AR_PRIXACH\nSalesPrice=AR_PRIXVEN\nUnit=AR_UNITEVEN\nCategoryCode=FA_CODEFAMILLE\nTaxCode=TA_CODE\nTrackStock=AR_SUIVISTOCK\nProductType=AR_TYPE\nActive=ACTIF");
    public SageImportModuleMappingOptions PriceLists { get; set; } = new("F_TARIF", "Code=TL_CODE\nLabel=TL_LIBELLE\nProductCode=AR_REF\nUnitPrice=PRIX");
    public SageImportModuleMappingOptions Stock { get; set; } = new("F_STOCK", "ProductCode=AR_REF\nWarehouseCode=DE_NO\nQuantity=QTE\nUnitCost=CMP\nMovementDate=MS_DATE");
    public SageImportModuleMappingOptions DocumentHeaders { get; set; } = new("F_DOCENTETE", "Number=DO_PIECE\nDate=DO_DATE\nDueDate=DO_ECHEANCE\nPartnerCode=DO_TIERS\nType=DO_TYPE\nStatus=DO_STATUT\nCurrency=DO_DEVISE\nWarehouseCode=DE_NO\nNotes=DO_REFCOM\nTotal=DO_TTC\nPaid=DO_REGLE\nBalance=DO_SOLDE");
    public SageImportModuleMappingOptions DocumentLines { get; set; } = new("F_DOCLIGNE", "Number=DO_PIECE\nProductCode=AR_REF\nDescription=DL_DESIGN\nQuantity=DL_QTE\nUnitPrice=DL_PUHT\nDiscountRate=DL_REMISE\nTaxRate=DL_TAUXTAXE");
}

public class SageImportModuleMappingOptions
{
    public SageImportModuleMappingOptions()
    {
    }

    public SageImportModuleMappingOptions(string tableName, string fieldMap)
    {
        TableName = tableName;
        FieldMap = fieldMap;
    }

    public string TableName { get; set; } = string.Empty;
    public string FieldMap { get; set; } = string.Empty;
}
