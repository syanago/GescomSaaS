IF DB_ID(N'GescomSaas') IS NULL
BEGIN
    CREATE DATABASE [GescomSaas];
END
GO

USE [GescomSaas];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetRoleClaims];
IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserClaims];
IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserLogins];
IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserRoles];
IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserTokens];
IF OBJECT_ID(N'[dbo].[PaymentAllocations]', N'U') IS NOT NULL DROP TABLE [dbo].[PaymentAllocations];
IF OBJECT_ID(N'[dbo].[ReminderLogs]', N'U') IS NOT NULL DROP TABLE [dbo].[ReminderLogs];
IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL DROP TABLE [dbo].[StockMovements];
IF OBJECT_ID(N'[dbo].[CommercialDocumentLines]', N'U') IS NOT NULL DROP TABLE [dbo].[CommercialDocumentLines];
IF OBJECT_ID(N'[dbo].[Payments]', N'U') IS NOT NULL DROP TABLE [dbo].[Payments];
IF OBJECT_ID(N'[dbo].[CommercialDocuments]', N'U') IS NOT NULL DROP TABLE [dbo].[CommercialDocuments];
IF OBJECT_ID(N'[dbo].[PriceListLines]', N'U') IS NOT NULL DROP TABLE [dbo].[PriceListLines];
IF OBJECT_ID(N'[dbo].[PriceLists]', N'U') IS NOT NULL DROP TABLE [dbo].[PriceLists];
IF OBJECT_ID(N'[dbo].[DocumentSequences]', N'U') IS NOT NULL DROP TABLE [dbo].[DocumentSequences];
IF OBJECT_ID(N'[dbo].[BusinessPartners]', N'U') IS NOT NULL DROP TABLE [dbo].[BusinessPartners];
IF OBJECT_ID(N'[dbo].[Products]', N'U') IS NOT NULL DROP TABLE [dbo].[Products];
IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL DROP TABLE [dbo].[Warehouses];
IF OBJECT_ID(N'[dbo].[ProductCategories]', N'U') IS NOT NULL DROP TABLE [dbo].[ProductCategories];
IF OBJECT_ID(N'[dbo].[TaxCodes]', N'U') IS NOT NULL DROP TABLE [dbo].[TaxCodes];
IF OBJECT_ID(N'[dbo].[PaymentTerms]', N'U') IS NOT NULL DROP TABLE [dbo].[PaymentTerms];
IF OBJECT_ID(N'[dbo].[TenantSubscriptions]', N'U') IS NOT NULL DROP TABLE [dbo].[TenantSubscriptions];
IF OBJECT_ID(N'[dbo].[SubscriptionPlans]', N'U') IS NOT NULL DROP TABLE [dbo].[SubscriptionPlans];
IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUsers];
IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetRoles];
IF OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NOT NULL DROP TABLE [dbo].[Tenants];
GO

CREATE TABLE [dbo].[Tenants]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Tenants] PRIMARY KEY DEFAULT NEWID(),
    [CompanyName] NVARCHAR(200) NOT NULL,
    [Slug] NVARCHAR(80) NOT NULL,
    [PrimaryContactEmail] NVARCHAR(200) NOT NULL,
    [CountryCode] NVARCHAR(2) NOT NULL,
    [CurrencyCode] NVARCHAR(3) NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Tenants_IsActive] DEFAULT (1),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Tenants_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL
);
GO

CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [dbo].[Tenants]([Slug]);
GO

CREATE TABLE [dbo].[AspNetRoles]
(
    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_AspNetRoles] PRIMARY KEY,
    [Name] NVARCHAR(256) NULL,
    [NormalizedName] NVARCHAR(256) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL
);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles]([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE TABLE [dbo].[AspNetUsers]
(
    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_AspNetUsers] PRIMARY KEY,
    [TenantId] UNIQUEIDENTIFIER NULL,
    [FirstName] NVARCHAR(80) NOT NULL CONSTRAINT [DF_AspNetUsers_FirstName] DEFAULT N'',
    [LastName] NVARCHAR(80) NOT NULL CONSTRAINT [DF_AspNetUsers_LastName] DEFAULT N'',
    [UserName] NVARCHAR(256) NULL,
    [NormalizedUserName] NVARCHAR(256) NULL,
    [Email] NVARCHAR(256) NULL,
    [NormalizedEmail] NVARCHAR(256) NULL,
    [EmailConfirmed] BIT NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NULL,
    [SecurityStamp] NVARCHAR(MAX) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL,
    [PhoneNumber] NVARCHAR(MAX) NULL,
    [PhoneNumberConfirmed] BIT NOT NULL,
    [TwoFactorEnabled] BIT NOT NULL,
    [LockoutEnd] DATETIMEOFFSET NULL,
    [LockoutEnabled] BIT NOT NULL,
    [AccessFailedCount] INT NOT NULL
);
GO

CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers]([NormalizedEmail]);
CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers]([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE TABLE [dbo].[SubscriptionPlans]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY DEFAULT NEWID(),
    [Code] NVARCHAR(40) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [Edition] INT NOT NULL,
    [MonthlyPrice] DECIMAL(18, 2) NOT NULL,
    [MaxUsers] INT NOT NULL,
    [IncludesAdvancedStock] BIT NOT NULL CONSTRAINT [DF_SubscriptionPlans_AdvancedStock] DEFAULT (0),
    [IncludesPurchasing] BIT NOT NULL CONSTRAINT [DF_SubscriptionPlans_Purchasing] DEFAULT (0),
    [IncludesBusinessIntelligence] BIT NOT NULL CONSTRAINT [DF_SubscriptionPlans_BI] DEFAULT (0),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_SubscriptionPlans_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL
);
GO

CREATE UNIQUE INDEX [IX_SubscriptionPlans_Code] ON [dbo].[SubscriptionPlans]([Code]);
GO

CREATE TABLE [dbo].[TenantSubscriptions]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_TenantSubscriptions] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [SubscriptionPlanId] UNIQUEIDENTIFIER NOT NULL,
    [Status] INT NOT NULL,
    [StartsOn] DATE NOT NULL,
    [EndsOn] DATE NULL,
    [AutoRenew] BIT NOT NULL CONSTRAINT [DF_TenantSubscriptions_AutoRenew] DEFAULT (1),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_TenantSubscriptions_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_TenantSubscriptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId] FOREIGN KEY ([SubscriptionPlanId]) REFERENCES [dbo].[SubscriptionPlans]([Id])
);
GO

CREATE INDEX [IX_TenantSubscriptions_TenantId] ON [dbo].[TenantSubscriptions]([TenantId]);
CREATE INDEX [IX_TenantSubscriptions_SubscriptionPlanId] ON [dbo].[TenantSubscriptions]([SubscriptionPlanId]);
GO

CREATE TABLE [dbo].[PaymentTerms]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PaymentTerms] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(20) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [DueInDays] INT NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentTerms_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_PaymentTerms_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_PaymentTerms_TenantId_Code] ON [dbo].[PaymentTerms]([TenantId], [Code]);
GO

CREATE TABLE [dbo].[TaxCodes]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_TaxCodes] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(20) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [Rate] DECIMAL(9, 4) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_TaxCodes_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_TaxCodes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_TaxCodes_TenantId_Code] ON [dbo].[TaxCodes]([TenantId], [Code]);
GO

CREATE TABLE [dbo].[ProductCategories]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ProductCategories] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(30) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ProductCategories_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_ProductCategories_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_ProductCategories_TenantId_Code] ON [dbo].[ProductCategories]([TenantId], [Code]);
GO

CREATE TABLE [dbo].[Warehouses]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Warehouses] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(20) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [IsDefault] BIT NOT NULL CONSTRAINT [DF_Warehouses_IsDefault] DEFAULT (0),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Warehouses_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_Warehouses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_Warehouses_TenantId_Code] ON [dbo].[Warehouses]([TenantId], [Code]);
GO

CREATE TABLE [dbo].[Products]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Products] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Sku] NVARCHAR(50) NOT NULL,
    [Label] NVARCHAR(160) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [ProductType] INT NOT NULL,
    [UnitOfMeasure] NVARCHAR(10) NOT NULL,
    [TrackStock] BIT NOT NULL CONSTRAINT [DF_Products_TrackStock] DEFAULT (1),
    [IsActive] BIT NOT NULL CONSTRAINT [DF_Products_IsActive] DEFAULT (1),
    [ProductCategoryId] UNIQUEIDENTIFIER NULL,
    [TaxCodeId] UNIQUEIDENTIFIER NULL,
    [PurchasePrice] DECIMAL(18, 2) NOT NULL,
    [SalesPrice] DECIMAL(18, 2) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Products_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_Products_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Products_ProductCategories_ProductCategoryId] FOREIGN KEY ([ProductCategoryId]) REFERENCES [dbo].[ProductCategories]([Id]),
    CONSTRAINT [FK_Products_TaxCodes_TaxCodeId] FOREIGN KEY ([TaxCodeId]) REFERENCES [dbo].[TaxCodes]([Id])
);
GO

CREATE UNIQUE INDEX [IX_Products_TenantId_Sku] ON [dbo].[Products]([TenantId], [Sku]);
CREATE INDEX [IX_Products_ProductCategoryId] ON [dbo].[Products]([ProductCategoryId]);
CREATE INDEX [IX_Products_TaxCodeId] ON [dbo].[Products]([TaxCodeId]);
GO

CREATE TABLE [dbo].[BusinessPartners]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_BusinessPartners] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(30) NOT NULL,
    [Name] NVARCHAR(180) NOT NULL,
    [PartnerType] INT NOT NULL,
    [Email] NVARCHAR(200) NULL,
    [PhoneNumber] NVARCHAR(40) NULL,
    [VatNumber] NVARCHAR(40) NULL,
    [CreditLimit] DECIMAL(18, 2) NOT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_BusinessPartners_IsActive] DEFAULT (1),
    [PaymentTermId] UNIQUEIDENTIFIER NULL,
    [BillingAddress_Recipient] NVARCHAR(120) NULL,
    [BillingAddress_StreetLine1] NVARCHAR(160) NULL,
    [BillingAddress_StreetLine2] NVARCHAR(160) NULL,
    [BillingAddress_PostalCode] NVARCHAR(20) NULL,
    [BillingAddress_City] NVARCHAR(80) NULL,
    [BillingAddress_State] NVARCHAR(80) NULL,
    [BillingAddress_Country] NVARCHAR(80) NULL,
    [ShippingAddress_Recipient] NVARCHAR(120) NULL,
    [ShippingAddress_StreetLine1] NVARCHAR(160) NULL,
    [ShippingAddress_StreetLine2] NVARCHAR(160) NULL,
    [ShippingAddress_PostalCode] NVARCHAR(20) NULL,
    [ShippingAddress_City] NVARCHAR(80) NULL,
    [ShippingAddress_State] NVARCHAR(80) NULL,
    [ShippingAddress_Country] NVARCHAR(80) NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_BusinessPartners_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_BusinessPartners_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_BusinessPartners_PaymentTerms_PaymentTermId] FOREIGN KEY ([PaymentTermId]) REFERENCES [dbo].[PaymentTerms]([Id])
);
GO

CREATE UNIQUE INDEX [IX_BusinessPartners_TenantId_Code] ON [dbo].[BusinessPartners]([TenantId], [Code]);
CREATE INDEX [IX_BusinessPartners_PaymentTermId] ON [dbo].[BusinessPartners]([PaymentTermId]);
GO

CREATE TABLE [dbo].[DocumentSequences]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_DocumentSequences] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentType] INT NOT NULL,
    [Prefix] NVARCHAR(20) NOT NULL,
    [NextValue] INT NOT NULL CONSTRAINT [DF_DocumentSequences_NextValue] DEFAULT (1),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_DocumentSequences_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_DocumentSequences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_DocumentSequences_TenantId_DocumentType] ON [dbo].[DocumentSequences]([TenantId], [DocumentType]);
GO

CREATE TABLE [dbo].[PriceLists]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PriceLists] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(30) NOT NULL,
    [Label] NVARCHAR(120) NOT NULL,
    [CurrencyCode] NVARCHAR(3) NOT NULL,
    [IsDefault] BIT NOT NULL CONSTRAINT [DF_PriceLists_IsDefault] DEFAULT (0),
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_PriceLists_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_PriceLists_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id])
);
GO

CREATE UNIQUE INDEX [IX_PriceLists_TenantId_Code] ON [dbo].[PriceLists]([TenantId], [Code]);
GO

CREATE TABLE [dbo].[CommercialDocuments]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CommercialDocuments] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentType] INT NOT NULL,
    [Status] INT NOT NULL,
    [Number] NVARCHAR(40) NOT NULL,
    [DocumentDate] DATE NOT NULL,
    [DueDate] DATE NULL,
    [CurrencyCode] NVARCHAR(3) NOT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [SourceDocumentId] UNIQUEIDENTIFIER NULL,
    [PartnerId] UNIQUEIDENTIFIER NOT NULL,
    [WarehouseId] UNIQUEIDENTIFIER NULL,
    [TotalExcludingTax] DECIMAL(18, 2) NOT NULL,
    [TotalTax] DECIMAL(18, 2) NOT NULL,
    [TotalIncludingTax] DECIMAL(18, 2) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_CommercialDocuments_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_CommercialDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_CommercialDocuments_BusinessPartners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [dbo].[BusinessPartners]([Id]),
    CONSTRAINT [FK_CommercialDocuments_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
    CONSTRAINT [FK_CommercialDocuments_CommercialDocuments_SourceDocumentId] FOREIGN KEY ([SourceDocumentId]) REFERENCES [dbo].[CommercialDocuments]([Id]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_CommercialDocuments_TenantId_Number] ON [dbo].[CommercialDocuments]([TenantId], [Number]);
CREATE INDEX [IX_CommercialDocuments_PartnerId] ON [dbo].[CommercialDocuments]([PartnerId]);
CREATE INDEX [IX_CommercialDocuments_WarehouseId] ON [dbo].[CommercialDocuments]([WarehouseId]);
CREATE INDEX [IX_CommercialDocuments_SourceDocumentId] ON [dbo].[CommercialDocuments]([SourceDocumentId]);
GO

CREATE TABLE [dbo].[Payments]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Payments] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [PaymentDate] DATE NOT NULL,
    [Direction] INT NOT NULL,
    [Method] INT NOT NULL,
    [ReferenceNumber] NVARCHAR(50) NOT NULL,
    [CurrencyCode] NVARCHAR(3) NOT NULL,
    [Amount] DECIMAL(18, 2) NOT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [PartnerId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Payments_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_Payments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_Payments_BusinessPartners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [dbo].[BusinessPartners]([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_Payments_TenantId_ReferenceNumber] ON [dbo].[Payments]([TenantId], [ReferenceNumber]);
CREATE INDEX [IX_Payments_PartnerId] ON [dbo].[Payments]([PartnerId]);
GO

CREATE TABLE [dbo].[PriceListLines]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PriceListLines] PRIMARY KEY DEFAULT NEWID(),
    [PriceListId] UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [UnitPrice] DECIMAL(18, 2) NOT NULL,
    [ValidFrom] DATE NULL,
    [ValidTo] DATE NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_PriceListLines_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_PriceListLines_PriceLists_PriceListId] FOREIGN KEY ([PriceListId]) REFERENCES [dbo].[PriceLists]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PriceListLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_PriceListLines_PriceListId] ON [dbo].[PriceListLines]([PriceListId]);
CREATE INDEX [IX_PriceListLines_ProductId] ON [dbo].[PriceListLines]([ProductId]);
GO

CREATE TABLE [dbo].[CommercialDocumentLines]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CommercialDocumentLines] PRIMARY KEY DEFAULT NEWID(),
    [CommercialDocumentId] UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NULL,
    [Description] NVARCHAR(240) NOT NULL,
    [Quantity] DECIMAL(18, 3) NOT NULL,
    [UnitPriceExcludingTax] DECIMAL(18, 2) NOT NULL,
    [DiscountRate] DECIMAL(9, 4) NOT NULL,
    [TaxRate] DECIMAL(9, 4) NOT NULL,
    [LineTotalExcludingTax] DECIMAL(18, 2) NOT NULL,
    [LineTaxAmount] DECIMAL(18, 2) NOT NULL,
    [LineTotalIncludingTax] DECIMAL(18, 2) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_CommercialDocumentLines_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_CommercialDocumentLines_CommercialDocuments_CommercialDocumentId] FOREIGN KEY ([CommercialDocumentId]) REFERENCES [dbo].[CommercialDocuments]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CommercialDocumentLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
);
GO

CREATE INDEX [IX_CommercialDocumentLines_CommercialDocumentId] ON [dbo].[CommercialDocumentLines]([CommercialDocumentId]);
CREATE INDEX [IX_CommercialDocumentLines_ProductId] ON [dbo].[CommercialDocumentLines]([ProductId]);
GO

CREATE TABLE [dbo].[PaymentAllocations]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY DEFAULT NEWID(),
    [PaymentId] UNIQUEIDENTIFIER NOT NULL,
    [CommercialDocumentId] UNIQUEIDENTIFIER NOT NULL,
    [AllocatedAmount] DECIMAL(18, 2) NOT NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentAllocations_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_PaymentAllocations_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PaymentAllocations_CommercialDocuments_CommercialDocumentId] FOREIGN KEY ([CommercialDocumentId]) REFERENCES [dbo].[CommercialDocuments]([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_PaymentAllocations_PaymentId] ON [dbo].[PaymentAllocations]([PaymentId]);
CREATE INDEX [IX_PaymentAllocations_CommercialDocumentId] ON [dbo].[PaymentAllocations]([CommercialDocumentId]);
GO

CREATE TABLE [dbo].[ReminderLogs]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ReminderLogs] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [CommercialDocumentId] UNIQUEIDENTIFIER NOT NULL,
    [ReminderLevel] INT NOT NULL,
    [SentOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ReminderLogs_SentOnUtc] DEFAULT SYSUTCDATETIME(),
    [Channel] NVARCHAR(30) NOT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ReminderLogs_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_ReminderLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_ReminderLogs_CommercialDocuments_CommercialDocumentId] FOREIGN KEY ([CommercialDocumentId]) REFERENCES [dbo].[CommercialDocuments]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ReminderLogs_CommercialDocumentId] ON [dbo].[ReminderLogs]([CommercialDocumentId]);
GO

CREATE TABLE [dbo].[StockMovements]
(
    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_StockMovements] PRIMARY KEY DEFAULT NEWID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [WarehouseId] UNIQUEIDENTIFIER NOT NULL,
    [MovementDate] DATE NOT NULL,
    [MovementType] INT NOT NULL,
    [Quantity] DECIMAL(18, 3) NOT NULL,
    [UnitCost] DECIMAL(18, 2) NOT NULL,
    [ReferenceNumber] NVARCHAR(40) NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL CONSTRAINT [DF_StockMovements_CreatedOnUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedOnUtc] DATETIME2 NULL,
    CONSTRAINT [FK_StockMovements_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([Id]),
    CONSTRAINT [FK_StockMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]),
    CONSTRAINT [FK_StockMovements_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id])
);
GO

CREATE INDEX [IX_StockMovements_ProductId] ON [dbo].[StockMovements]([ProductId]);
CREATE INDEX [IX_StockMovements_WarehouseId] ON [dbo].[StockMovements]([WarehouseId]);
GO

CREATE TABLE [dbo].[AspNetRoleClaims]
(
    [Id] INT NOT NULL IDENTITY(1,1) CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY,
    [RoleId] NVARCHAR(450) NOT NULL,
    [ClaimType] NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims]([RoleId]);
GO

CREATE TABLE [dbo].[AspNetUserClaims]
(
    [Id] INT NOT NULL IDENTITY(1,1) CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY,
    [UserId] NVARCHAR(450) NOT NULL,
    [ClaimType] NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims]([UserId]);
GO

CREATE TABLE [dbo].[AspNetUserLogins]
(
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [ProviderKey] NVARCHAR(450) NOT NULL,
    [ProviderDisplayName] NVARCHAR(MAX) NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins]([UserId]);
GO

CREATE TABLE [dbo].[AspNetUserRoles]
(
    [UserId] NVARCHAR(450) NOT NULL,
    [RoleId] NVARCHAR(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles]([RoleId]);
GO

CREATE TABLE [dbo].[AspNetUserTokens]
(
    [UserId] NVARCHAR(450) NOT NULL,
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [Name] NVARCHAR(450) NOT NULL,
    [Value] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = N'PlatformAdmin')
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(NVARCHAR(450), NEWID()), N'PlatformAdmin', N'PLATFORMADMIN', CONVERT(NVARCHAR(36), NEWID()));
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = N'TenantOwner')
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(NVARCHAR(450), NEWID()), N'TenantOwner', N'TENANTOWNER', CONVERT(NVARCHAR(36), NEWID()));
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = N'SalesManager')
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(NVARCHAR(450), NEWID()), N'SalesManager', N'SALESMANAGER', CONVERT(NVARCHAR(36), NEWID()));
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = N'PurchasingManager')
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(NVARCHAR(450), NEWID()), N'PurchasingManager', N'PURCHASINGMANAGER', CONVERT(NVARCHAR(36), NEWID()));
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans] WHERE [Code] = N'ESSENTIALS')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans] ([Id], [Code], [Label], [Edition], [MonthlyPrice], [MaxUsers], [IncludesAdvancedStock], [IncludesPurchasing], [IncludesBusinessIntelligence], [CreatedOnUtc])
    VALUES (NEWID(), N'ESSENTIALS', N'Essentials', 1, 79.00, 3, 0, 0, 0, SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans] WHERE [Code] = N'STANDARD')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans] ([Id], [Code], [Label], [Edition], [MonthlyPrice], [MaxUsers], [IncludesAdvancedStock], [IncludesPurchasing], [IncludesBusinessIntelligence], [CreatedOnUtc])
    VALUES (NEWID(), N'STANDARD', N'Standard', 2, 149.00, 10, 1, 1, 0, SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans] WHERE [Code] = N'ENTERPRISE')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans] ([Id], [Code], [Label], [Edition], [MonthlyPrice], [MaxUsers], [IncludesAdvancedStock], [IncludesPurchasing], [IncludesBusinessIntelligence], [CreatedOnUtc])
    VALUES (NEWID(), N'ENTERPRISE', N'Enterprise', 3, 299.00, 100, 1, 1, 1, SYSUTCDATETIME());
END
GO
