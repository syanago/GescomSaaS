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

UPDATE dbo.Tenants
SET
    CompanyLegalName = CASE WHEN LTRIM(RTRIM(ISNULL(CompanyLegalName, N''))) = N'' THEN CompanyName ELSE CompanyLegalName END,
    CashCurrencyCode = CASE WHEN LTRIM(RTRIM(ISNULL(CashCurrencyCode, N''))) = N'' THEN CurrencyCode ELSE CashCurrencyCode END,
    CurrencySymbol = CASE WHEN LTRIM(RTRIM(ISNULL(CurrencySymbol, N''))) = N'' THEN N'$' ELSE CurrencySymbol END,
    MoneyDecimalSeparator = CASE WHEN LTRIM(RTRIM(ISNULL(MoneyDecimalSeparator, N''))) = N'' THEN N',' ELSE MoneyDecimalSeparator END,
    MoneyGroupSeparator = CASE WHEN MoneyGroupSeparator IS NULL THEN N' ' ELSE MoneyGroupSeparator END,
    QuantityDecimalSeparator = CASE WHEN LTRIM(RTRIM(ISNULL(QuantityDecimalSeparator, N''))) = N'' THEN N',' ELSE QuantityDecimalSeparator END,
    QuantityGroupSeparator = CASE WHEN QuantityGroupSeparator IS NULL THEN N' ' ELSE QuantityGroupSeparator END;
GO
