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

IF COL_LENGTH('dbo.Tenants', 'PaymentMethodsJson') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD PaymentMethodsJson nvarchar(400) NOT NULL CONSTRAINT DF_Tenants_PaymentMethodsJson DEFAULT N'["BankTransfer","Cash","Check"]';
END
GO

IF COL_LENGTH('dbo.Tenants', 'PartnerLookupMode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD PartnerLookupMode int NOT NULL CONSTRAINT DF_Tenants_PartnerLookupMode DEFAULT 0;
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

IF OBJECT_ID('dbo.JournalAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JournalAccounts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_JournalAccounts PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantId uniqueidentifier NOT NULL,
        Code nvarchar(20) NOT NULL,
        Label nvarchar(120) NOT NULL,
        CounterpartAccountCode nvarchar(30) NULL,
        CONSTRAINT FK_JournalAccounts_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalAccounts_TenantId_Code' AND object_id = OBJECT_ID('dbo.JournalAccounts'))
BEGIN
    CREATE UNIQUE INDEX IX_JournalAccounts_TenantId_Code ON dbo.JournalAccounts(TenantId, Code);
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
        CONSTRAINT FK_SageImportProfileVersions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_SageImportProfileVersions_SageImportProfiles FOREIGN KEY (SageImportProfileId) REFERENCES dbo.SageImportProfiles(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.SageImportProfileVersions', 'U') IS NOT NULL
AND EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_SageImportProfileVersions_Tenants'
      AND parent_object_id = OBJECT_ID('dbo.SageImportProfileVersions')
      AND delete_referential_action = 1
)
BEGIN
    ALTER TABLE dbo.SageImportProfileVersions DROP CONSTRAINT FK_SageImportProfileVersions_Tenants;
    ALTER TABLE dbo.SageImportProfileVersions
        ADD CONSTRAINT FK_SageImportProfileVersions_Tenants
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SageImportProfileVersions_ProfileId_VersionNumber' AND object_id = OBJECT_ID('dbo.SageImportProfileVersions'))
BEGIN
    CREATE UNIQUE INDEX IX_SageImportProfileVersions_ProfileId_VersionNumber ON dbo.SageImportProfileVersions(SageImportProfileId, VersionNumber);
END
GO

-- Le script d'upgrade ne doit plus modifier les formats deja definis sur les tenants.
-- Il ajoute uniquement les colonnes manquantes avec leurs valeurs par defaut SQL.
GO

IF COL_LENGTH('dbo.DocumentSequences', 'Mode') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentSequences ADD Mode int NOT NULL CONSTRAINT DF_DocumentSequences_Mode DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.DocumentSequences', 'NumberLength') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentSequences ADD NumberLength int NOT NULL CONSTRAINT DF_DocumentSequences_NumberLength DEFAULT 4;
END
GO

IF COL_LENGTH('dbo.DocumentSequences', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentSequences ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_DocumentSequences_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.DocumentSequences', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentSequences ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF OBJECT_ID('dbo.ReferenceNumberingSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReferenceNumberingSettings
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ReferenceNumberingSettings PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantId uniqueidentifier NOT NULL,
        Scope int NOT NULL,
        Mode int NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_Mode DEFAULT 1,
        Prefix nvarchar(20) NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_Prefix DEFAULT N'',
        NumberLength int NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_NumberLength DEFAULT 4,
        NextValue int NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_NextValue DEFAULT 1,
        CONSTRAINT FK_ReferenceNumberingSettings_Tenants_TenantId FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_ReferenceNumberingSettings_TenantId_Scope
        ON dbo.ReferenceNumberingSettings(TenantId, Scope);
END
GO

IF COL_LENGTH('dbo.ReferenceNumberingSettings', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ReferenceNumberingSettings ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.ReferenceNumberingSettings', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ReferenceNumberingSettings ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.ReferenceNumberingSettings', 'NumberLength') IS NULL
BEGIN
    ALTER TABLE dbo.ReferenceNumberingSettings ADD NumberLength int NOT NULL CONSTRAINT DF_ReferenceNumberingSettings_NumberLength DEFAULT 4;
END
GO

IF COL_LENGTH('dbo.SubscriptionPlans', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.SubscriptionPlans ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_SubscriptionPlans_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.SubscriptionPlans', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.SubscriptionPlans ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.Tenants', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_Tenants_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.Tenants', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.TenantSubscriptions', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TenantSubscriptions ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_TenantSubscriptions_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.TenantSubscriptions', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TenantSubscriptions ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.TenantQuotaNotifications', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TenantQuotaNotifications ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_TenantQuotaNotifications_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.TenantQuotaNotifications', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TenantQuotaNotifications ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.UserInvitations', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.UserInvitations ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_UserInvitations_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.UserInvitations', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.UserInvitations ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PaymentTerms', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentTerms ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PaymentTerms_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PaymentTerms', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentTerms ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.TaxCodes', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TaxCodes ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_TaxCodes_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.TaxCodes', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.TaxCodes ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.ProductCategories', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ProductCategories ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_ProductCategories_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.ProductCategories', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ProductCategories ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.Products', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_Products_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.Products', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PriceLists', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PriceLists ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PriceLists_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PriceLists', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PriceLists ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PriceListLines', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PriceListLines ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PriceListLines_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PriceListLines', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PriceListLines ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.Warehouses', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Warehouses ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_Warehouses_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.Warehouses', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Warehouses ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.BusinessPartners', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.BusinessPartners ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_BusinessPartners_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.BusinessPartners', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.BusinessPartners ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_CommercialDocuments_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocumentLines', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocumentLines ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_CommercialDocumentLines_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.CommercialDocumentLines', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocumentLines ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.Payments', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_Payments_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.Payments', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PaymentAllocations', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentAllocations ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PaymentAllocations_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PaymentAllocations', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentAllocations ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.ReminderLogs', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ReminderLogs ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_ReminderLogs_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.ReminderLogs', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ReminderLogs ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.StockMovements', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.StockMovements ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_StockMovements_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.StockMovements', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.StockMovements ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PlatformInvoices', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformInvoices ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PlatformInvoices_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PlatformInvoices', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformInvoices ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.PlatformInvoiceLines', 'CreatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformInvoiceLines ADD CreatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PlatformInvoiceLines_CreatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PlatformInvoiceLines', 'UpdatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformInvoiceLines ADD UpdatedOnUtc datetime2 NULL;
END
GO

IF COL_LENGTH('dbo.Payments', 'Type') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD Type int NOT NULL CONSTRAINT DF_Payments_Type DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.Payments', 'AllocationStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD AllocationStatus int NOT NULL CONSTRAINT DF_Payments_AllocationStatus DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.Payments', 'AllocatedAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD AllocatedAmount decimal(18,2) NOT NULL CONSTRAINT DF_Payments_AllocatedAmount DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Payments', 'AvailableAmount') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD AvailableAmount decimal(18,2) NOT NULL CONSTRAINT DF_Payments_AvailableAmount DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Payments', 'SourceCommercialDocumentId') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD SourceCommercialDocumentId uniqueidentifier NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Payments_CommercialDocuments_SourceCommercialDocumentId'
)
AND COL_LENGTH('dbo.Payments', 'SourceCommercialDocumentId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Payments
    ADD CONSTRAINT FK_Payments_CommercialDocuments_SourceCommercialDocumentId
        FOREIGN KEY (SourceCommercialDocumentId) REFERENCES dbo.CommercialDocuments(Id);
END
GO

IF COL_LENGTH('dbo.PaymentAllocations', 'AllocatedOnUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentAllocations ADD AllocatedOnUtc datetime2 NOT NULL CONSTRAINT DF_PaymentAllocations_AllocatedOnUtc DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH('dbo.PaymentAllocations', 'Notes') IS NULL
BEGIN
    ALTER TABLE dbo.PaymentAllocations ADD Notes nvarchar(400) NULL;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'PaymentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD PaymentStatus int NOT NULL CONSTRAINT DF_CommercialDocuments_PaymentStatus DEFAULT 1;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'PaidAmount') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD PaidAmount decimal(18,2) NOT NULL CONSTRAINT DF_CommercialDocuments_PaidAmount DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'BalanceAmount') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD BalanceAmount decimal(18,2) NOT NULL CONSTRAINT DF_CommercialDocuments_BalanceAmount DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'InDispute') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD InDispute bit NOT NULL CONSTRAINT DF_CommercialDocuments_InDispute DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.CommercialDocuments', 'PromiseToPayDate') IS NULL
BEGIN
    ALTER TABLE dbo.CommercialDocuments ADD PromiseToPayDate date NULL;
END
GO

IF COL_LENGTH('dbo.ReminderLogs', 'IsAutomatic') IS NULL
BEGIN
    ALTER TABLE dbo.ReminderLogs ADD IsAutomatic bit NOT NULL CONSTRAINT DF_ReminderLogs_IsAutomatic DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.ReminderLogs', 'IsGrouped') IS NULL
BEGIN
    ALTER TABLE dbo.ReminderLogs ADD IsGrouped bit NOT NULL CONSTRAINT DF_ReminderLogs_IsGrouped DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.ReminderLogs', 'NextActionDate') IS NULL
BEGIN
    ALTER TABLE dbo.ReminderLogs ADD NextActionDate date NULL;
END
GO

UPDATE dbo.Payments
SET AllocatedAmount = ISNULL(alloc.AllocatedAmount, 0),
    AvailableAmount = Amount - ISNULL(alloc.AllocatedAmount, 0),
    AllocationStatus = CASE
        WHEN Amount - ISNULL(alloc.AllocatedAmount, 0) <= 0 THEN 3
        WHEN ISNULL(alloc.AllocatedAmount, 0) > 0 THEN 2
        ELSE 1
    END
FROM dbo.Payments p
OUTER APPLY (
    SELECT SUM(pa.AllocatedAmount) AS AllocatedAmount
    FROM dbo.PaymentAllocations pa
    WHERE pa.PaymentId = p.Id
) alloc;
GO

UPDATE dbo.CommercialDocuments
SET PaidAmount = ISNULL(alloc.PaidAmount, 0),
    BalanceAmount = TotalIncludingTax - ISNULL(alloc.PaidAmount, 0),
    PaymentStatus = CASE
        WHEN InDispute = 1 THEN 5
        WHEN PromiseToPayDate IS NOT NULL THEN 6
        WHEN TotalIncludingTax - ISNULL(alloc.PaidAmount, 0) <= 0 THEN 3
        WHEN ISNULL(alloc.PaidAmount, 0) > 0 THEN 2
        WHEN DueDate IS NOT NULL AND DueDate < CAST(GETUTCDATE() AS date) THEN 4
        ELSE 1
    END
FROM dbo.CommercialDocuments d
OUTER APPLY (
    SELECT SUM(pa.AllocatedAmount) AS PaidAmount
    FROM dbo.PaymentAllocations pa
    WHERE pa.CommercialDocumentId = d.Id
) alloc;
GO

IF COL_LENGTH('dbo.Tenants', 'IncomingPaymentAllocationMode') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD IncomingPaymentAllocationMode int NOT NULL CONSTRAINT DF_Tenants_IncomingPaymentAllocationMode DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'ReminderFriendlyDelayDays') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD ReminderFriendlyDelayDays int NOT NULL CONSTRAINT DF_Tenants_ReminderFriendlyDelayDays DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'ReminderFormalDelayDays') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD ReminderFormalDelayDays int NOT NULL CONSTRAINT DF_Tenants_ReminderFormalDelayDays DEFAULT 7;
END
GO

IF COL_LENGTH('dbo.Tenants', 'ReminderFinalNoticeDelayDays') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD ReminderFinalNoticeDelayDays int NOT NULL CONSTRAINT DF_Tenants_ReminderFinalNoticeDelayDays DEFAULT 14;
END
GO

IF COL_LENGTH('dbo.Tenants', 'BlockSalesOrdersOnCreditLimit') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD BlockSalesOrdersOnCreditLimit bit NOT NULL CONSTRAINT DF_Tenants_BlockSalesOrdersOnCreditLimit DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'BlockSalesOrdersOnOverdue') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD BlockSalesOrdersOnOverdue bit NOT NULL CONSTRAINT DF_Tenants_BlockSalesOrdersOnOverdue DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'BlockDeliveriesOnCreditLimit') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD BlockDeliveriesOnCreditLimit bit NOT NULL CONSTRAINT DF_Tenants_BlockDeliveriesOnCreditLimit DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Tenants', 'BlockDeliveriesOnOverdue') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD BlockDeliveriesOnOverdue bit NOT NULL CONSTRAINT DF_Tenants_BlockDeliveriesOnOverdue DEFAULT 0;
END
GO

IF OBJECT_ID('dbo.TenantAccessProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantAccessProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TenantAccessProfiles PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantId uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(600) NULL,
        IsDefault bit NOT NULL CONSTRAINT DF_TenantAccessProfiles_IsDefault DEFAULT 0,
        CONSTRAINT FK_TenantAccessProfiles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantAccessProfiles_TenantId_Name' AND object_id = OBJECT_ID('dbo.TenantAccessProfiles'))
BEGIN
    CREATE UNIQUE INDEX IX_TenantAccessProfiles_TenantId_Name ON dbo.TenantAccessProfiles(TenantId, [Name]);
END
GO

IF OBJECT_ID('dbo.TenantAccessProfilePermissions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantAccessProfilePermissions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TenantAccessProfilePermissions PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantAccessProfileId uniqueidentifier NOT NULL,
        PermissionKey nvarchar(120) NOT NULL,
        CONSTRAINT FK_TenantAccessProfilePermissions_TenantAccessProfiles FOREIGN KEY (TenantAccessProfileId) REFERENCES dbo.TenantAccessProfiles(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantAccessProfilePermissions_ProfileId_Key' AND object_id = OBJECT_ID('dbo.TenantAccessProfilePermissions'))
BEGIN
    CREATE UNIQUE INDEX IX_TenantAccessProfilePermissions_ProfileId_Key ON dbo.TenantAccessProfilePermissions(TenantAccessProfileId, PermissionKey);
END
GO

IF OBJECT_ID('dbo.TenantUserAccessProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantUserAccessProfiles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TenantUserAccessProfiles PRIMARY KEY,
        CreatedOnUtc datetime2 NOT NULL,
        UpdatedOnUtc datetime2 NULL,
        TenantId uniqueidentifier NOT NULL,
        UserId nvarchar(450) NOT NULL,
        TenantAccessProfileId uniqueidentifier NOT NULL,
        CONSTRAINT FK_TenantUserAccessProfiles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TenantUserAccessProfiles_TenantAccessProfiles FOREIGN KEY (TenantAccessProfileId) REFERENCES dbo.TenantAccessProfiles(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.TenantUserAccessProfiles', 'U') IS NOT NULL
AND EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_TenantUserAccessProfiles_Tenants'
      AND parent_object_id = OBJECT_ID('dbo.TenantUserAccessProfiles')
      AND delete_referential_action = 1
)
BEGIN
    ALTER TABLE dbo.TenantUserAccessProfiles DROP CONSTRAINT FK_TenantUserAccessProfiles_Tenants;
    ALTER TABLE dbo.TenantUserAccessProfiles
        ADD CONSTRAINT FK_TenantUserAccessProfiles_Tenants
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantUserAccessProfiles_TenantId_UserId_ProfileId' AND object_id = OBJECT_ID('dbo.TenantUserAccessProfiles'))
BEGIN
    CREATE UNIQUE INDEX IX_TenantUserAccessProfiles_TenantId_UserId_ProfileId ON dbo.TenantUserAccessProfiles(TenantId, UserId, TenantAccessProfileId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TenantUserAccessProfiles_TenantId_UserId' AND object_id = OBJECT_ID('dbo.TenantUserAccessProfiles'))
BEGIN
    CREATE INDEX IX_TenantUserAccessProfiles_TenantId_UserId ON dbo.TenantUserAccessProfiles(TenantId, UserId);
END
GO
