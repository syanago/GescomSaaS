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

IF COL_LENGTH('dbo.Tenants', 'SageImportEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageImportEnabled bit NOT NULL CONSTRAINT DF_Tenants_SageImportEnabled DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageSqlServerName') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageSqlServerName nvarchar(120) NOT NULL CONSTRAINT DF_Tenants_SageSqlServerName DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageSqlDatabaseName') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageSqlDatabaseName nvarchar(120) NOT NULL CONSTRAINT DF_Tenants_SageSqlDatabaseName DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageSqlAuthenticationMode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageSqlAuthenticationMode int NOT NULL CONSTRAINT DF_Tenants_SageSqlAuthenticationMode DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageSqlUserName') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageSqlUserName nvarchar(120) NOT NULL CONSTRAINT DF_Tenants_SageSqlUserName DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageSqlPassword') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageSqlPassword nvarchar(200) NOT NULL CONSTRAINT DF_Tenants_SageSqlPassword DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageCompanyCode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageCompanyCode nvarchar(80) NOT NULL CONSTRAINT DF_Tenants_SageCompanyCode DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageImportMode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageImportMode int NOT NULL CONSTRAINT DF_Tenants_SageImportMode DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageImportScopeJson') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageImportScopeJson nvarchar(max) NOT NULL CONSTRAINT DF_Tenants_SageImportScopeJson DEFAULT N'{}';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageImportFilterJson') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageImportFilterJson nvarchar(max) NOT NULL CONSTRAINT DF_Tenants_SageImportFilterJson DEFAULT N'{}';
END
GO

IF COL_LENGTH('dbo.Tenants', 'SageImportMappingJson') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD SageImportMappingJson nvarchar(max) NOT NULL CONSTRAINT DF_Tenants_SageImportMappingJson DEFAULT N'{}';
END
GO

IF OBJECT_ID('dbo.SageImportRuns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SageImportRuns
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        IsDryRun bit NOT NULL,
        IsSuccessful bit NOT NULL,
        SourceServer nvarchar(120) NOT NULL,
        SourceDatabase nvarchar(120) NOT NULL,
        ImportMode int NOT NULL,
        TotalImported int NOT NULL,
        TotalUpdated int NOT NULL,
        TotalSkipped int NOT NULL,
        WarningSummary nvarchar(2000) NOT NULL,
        CONSTRAINT FK_SageImportRuns_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.SageImportRunModules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SageImportRunModules
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        SageImportRunId uniqueidentifier NOT NULL,
        ModuleName nvarchar(120) NOT NULL,
        Status nvarchar(40) NOT NULL,
        SourceTable nvarchar(240) NOT NULL,
        Imported int NOT NULL,
        Updated int NOT NULL,
        Skipped int NOT NULL,
        Summary nvarchar(600) NOT NULL,
        NoteSummary nvarchar(2000) NOT NULL,
        CONSTRAINT FK_SageImportRunModules_SageImportRuns FOREIGN KEY (SageImportRunId) REFERENCES dbo.SageImportRuns(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.SageImportProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SageImportProfiles
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(600) NOT NULL,
        IsDefault bit NOT NULL,
        IsArchived bit NOT NULL,
        CONSTRAINT FK_SageImportProfiles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );
END
GO

IF COL_LENGTH('dbo.SageImportProfiles', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE dbo.SageImportProfiles ADD IsArchived bit NOT NULL CONSTRAINT DF_SageImportProfiles_IsArchived DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SageImportProfiles_TenantId_Name' AND object_id = OBJECT_ID('dbo.SageImportProfiles'))
BEGIN
    CREATE UNIQUE INDEX IX_SageImportProfiles_TenantId_Name ON dbo.SageImportProfiles(TenantId, [Name]);
END
GO

IF OBJECT_ID('dbo.SageImportProfileVersions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SageImportProfileVersions
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        SageImportProfileId uniqueidentifier NOT NULL,
        VersionNumber int NOT NULL,
        Notes nvarchar(600) NOT NULL,
        ProfileJson nvarchar(max) NOT NULL,
        CONSTRAINT FK_SageImportProfileVersions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE,
        CONSTRAINT FK_SageImportProfileVersions_SageImportProfiles FOREIGN KEY (SageImportProfileId) REFERENCES dbo.SageImportProfiles(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SageImportProfileVersions_ProfileId_VersionNumber' AND object_id = OBJECT_ID('dbo.SageImportProfileVersions'))
BEGIN
    CREATE UNIQUE INDEX IX_SageImportProfileVersions_ProfileId_VersionNumber ON dbo.SageImportProfileVersions(SageImportProfileId, VersionNumber);
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
    AllowNegativeStock = ISNULL(AllowNegativeStock, 0),
    SageImportScopeJson = CASE WHEN LTRIM(RTRIM(ISNULL(SageImportScopeJson, N''))) = N'' THEN N'{}' ELSE SageImportScopeJson END,
    SageImportFilterJson = CASE WHEN LTRIM(RTRIM(ISNULL(SageImportFilterJson, N''))) = N'' THEN N'{}' ELSE SageImportFilterJson END,
    SageImportMappingJson = CASE WHEN LTRIM(RTRIM(ISNULL(SageImportMappingJson, N''))) = N'' THEN N'{}' ELSE SageImportMappingJson END;
GO
