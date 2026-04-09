USE [GescomSaas];
GO

IF COL_LENGTH('dbo.Tenants', 'CompanyLegalName') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD CompanyLegalName nvarchar(200) NOT NULL CONSTRAINT DF_Tenants_CompanyLegalName DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'PhoneNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD PhoneNumber nvarchar(40) NOT NULL CONSTRAINT DF_Tenants_PhoneNumber DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'AddressLine1') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD AddressLine1 nvarchar(160) NOT NULL CONSTRAINT DF_Tenants_AddressLine1 DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'AddressLine2') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD AddressLine2 nvarchar(160) NOT NULL CONSTRAINT DF_Tenants_AddressLine2 DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'PostalCode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD PostalCode nvarchar(20) NOT NULL CONSTRAINT DF_Tenants_PostalCode DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'City') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD City nvarchar(80) NOT NULL CONSTRAINT DF_Tenants_City DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'State') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD [State] nvarchar(80) NOT NULL CONSTRAINT DF_Tenants_State DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'CashCurrencyCode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD CashCurrencyCode nvarchar(3) NOT NULL CONSTRAINT DF_Tenants_CashCurrencyCode DEFAULT N'CAD';
END
GO

IF COL_LENGTH('dbo.Tenants', 'CurrencySymbol') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD CurrencySymbol nvarchar(8) NOT NULL CONSTRAINT DF_Tenants_CurrencySymbol DEFAULT N'$';
END
GO

IF COL_LENGTH('dbo.Tenants', 'CurrencySymbolPosition') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD CurrencySymbolPosition int NOT NULL CONSTRAINT DF_Tenants_CurrencySymbolPosition DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'MoneyDecimalSeparator') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD MoneyDecimalSeparator nvarchar(4) NOT NULL CONSTRAINT DF_Tenants_MoneyDecimalSeparator DEFAULT N',';
END
GO

IF COL_LENGTH('dbo.Tenants', 'MoneyGroupSeparator') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD MoneyGroupSeparator nvarchar(4) NOT NULL CONSTRAINT DF_Tenants_MoneyGroupSeparator DEFAULT N' ';
END
GO

IF COL_LENGTH('dbo.Tenants', 'MoneyDecimalPlaces') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD MoneyDecimalPlaces int NOT NULL CONSTRAINT DF_Tenants_MoneyDecimalPlaces DEFAULT 2;
END
GO

IF COL_LENGTH('dbo.Tenants', 'QuantityDecimalSeparator') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD QuantityDecimalSeparator nvarchar(4) NOT NULL CONSTRAINT DF_Tenants_QuantityDecimalSeparator DEFAULT N',';
END
GO

IF COL_LENGTH('dbo.Tenants', 'QuantityGroupSeparator') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD QuantityGroupSeparator nvarchar(4) NOT NULL CONSTRAINT DF_Tenants_QuantityGroupSeparator DEFAULT N' ';
END
GO

IF COL_LENGTH('dbo.Tenants', 'QuantityDecimalPlaces') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD QuantityDecimalPlaces int NOT NULL CONSTRAINT DF_Tenants_QuantityDecimalPlaces DEFAULT 3;
END
GO

IF COL_LENGTH('dbo.Tenants', 'VisualTheme') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD VisualTheme int NOT NULL CONSTRAINT DF_Tenants_VisualTheme DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'AllowNegativeStock') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD AllowNegativeStock bit NOT NULL CONSTRAINT DF_Tenants_AllowNegativeStock DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'DefaultStockValuationMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD DefaultStockValuationMethod int NOT NULL CONSTRAINT DF_Tenants_DefaultStockValuationMethod DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.ProductCategories', 'StockValuationMethod') IS NULL
BEGIN
    ALTER TABLE dbo.ProductCategories ADD StockValuationMethod int NOT NULL CONSTRAINT DF_ProductCategories_StockValuationMethod DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.ProductCategories', 'StockIdentityTrackingMode') IS NULL
BEGIN
    ALTER TABLE dbo.ProductCategories ADD StockIdentityTrackingMode int NOT NULL CONSTRAINT DF_ProductCategories_StockIdentityTrackingMode DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Products', 'StockValuationMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD StockValuationMethod int NOT NULL CONSTRAINT DF_Products_StockValuationMethod DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Products', 'StockIdentityTrackingMode') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD StockIdentityTrackingMode int NOT NULL CONSTRAINT DF_Products_StockIdentityTrackingMode DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.StockMovements', 'LotNumber') IS NULL
BEGIN
    ALTER TABLE dbo.StockMovements ADD LotNumber nvarchar(60) NULL;
END
GO

IF COL_LENGTH('dbo.StockMovements', 'SerialNumber') IS NULL
BEGIN
    ALTER TABLE dbo.StockMovements ADD SerialNumber nvarchar(120) NULL;
END
GO

IF COL_LENGTH('dbo.StockMovements', 'ExpirationDate') IS NULL
BEGIN
    ALTER TABLE dbo.StockMovements ADD ExpirationDate date NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocumentLines', 'LotNumber') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocumentLines ADD LotNumber nvarchar(60) NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocumentLines', 'SerialNumber') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocumentLines ADD SerialNumber nvarchar(120) NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocumentLines', 'ExpirationDate') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocumentLines ADD ExpirationDate date NULL;
END
GO

IF OBJECT_ID('dbo.StockDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockDocuments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_StockDocuments PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantId uniqueidentifier NOT NULL,
        Number nvarchar(40) NOT NULL,
        DocumentType int NOT NULL,
        Status int NOT NULL,
        DocumentDate date NOT NULL,
        SourceWarehouseId uniqueidentifier NULL,
        DestinationWarehouseId uniqueidentifier NULL,
        Notes nvarchar(1000) NULL,
        PostedOnUtc datetime2 NULL
    );

    CREATE UNIQUE INDEX IX_StockDocuments_TenantId_Number ON dbo.StockDocuments(TenantId, Number);
    ALTER TABLE dbo.StockDocuments ADD CONSTRAINT FK_StockDocuments_Tenants_TenantId FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id);
    ALTER TABLE dbo.StockDocuments ADD CONSTRAINT FK_StockDocuments_Warehouses_SourceWarehouseId FOREIGN KEY (SourceWarehouseId) REFERENCES dbo.Warehouses(Id);
    ALTER TABLE dbo.StockDocuments ADD CONSTRAINT FK_StockDocuments_Warehouses_DestinationWarehouseId FOREIGN KEY (DestinationWarehouseId) REFERENCES dbo.Warehouses(Id);
END
GO

IF OBJECT_ID('dbo.StockDocumentLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockDocumentLines
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_StockDocumentLines PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        StockDocumentId uniqueidentifier NOT NULL,
        ProductId uniqueidentifier NULL,
        Description nvarchar(240) NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        UnitCost decimal(18,2) NOT NULL,
        LotNumber nvarchar(60) NULL,
        SerialNumber nvarchar(120) NULL,
        ExpirationDate date NULL
    );

    CREATE INDEX IX_StockDocumentLines_StockDocumentId ON dbo.StockDocumentLines(StockDocumentId);
    ALTER TABLE dbo.StockDocumentLines ADD CONSTRAINT FK_StockDocumentLines_StockDocuments_StockDocumentId FOREIGN KEY (StockDocumentId) REFERENCES dbo.StockDocuments(Id) ON DELETE CASCADE;
    ALTER TABLE dbo.StockDocumentLines ADD CONSTRAINT FK_StockDocumentLines_Products_ProductId FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id);
END
GO

UPDATE dbo.Tenants
SET
    CompanyLegalName = CASE WHEN LTRIM(RTRIM(ISNULL(CompanyLegalName, N''))) = N'' THEN CompanyName ELSE CompanyLegalName END,
    CashCurrencyCode = CASE WHEN LTRIM(RTRIM(ISNULL(CashCurrencyCode, N''))) = N'' THEN CurrencyCode ELSE CashCurrencyCode END,
    CurrencySymbol = CASE WHEN LTRIM(RTRIM(ISNULL(CurrencySymbol, N''))) = N'' THEN N'$' ELSE CurrencySymbol END,
    MoneyDecimalSeparator = CASE WHEN LTRIM(RTRIM(ISNULL(MoneyDecimalSeparator, N''))) = N'' THEN N',' ELSE MoneyDecimalSeparator END,
    MoneyGroupSeparator = CASE WHEN MoneyGroupSeparator IS NULL THEN N' ' ELSE MoneyGroupSeparator END,
    QuantityDecimalSeparator = CASE WHEN LTRIM(RTRIM(ISNULL(QuantityDecimalSeparator, N''))) = N'' THEN N',' ELSE QuantityDecimalSeparator END,
    QuantityGroupSeparator = CASE WHEN QuantityGroupSeparator IS NULL THEN N' ' ELSE QuantityGroupSeparator END,
    DefaultStockValuationMethod = ISNULL(DefaultStockValuationMethod, 0),
    AllowNegativeStock = ISNULL(AllowNegativeStock, 0);
GO
