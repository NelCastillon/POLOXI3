-- ============================================================
-- MIGRATION 0045: AGENCY SETUP SCHEMA - COMPLETE
-- Creates comprehensive agency management tables
-- ============================================================

-- ============================================================
-- AGENCY SCHEMA CREATION
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Agency')
BEGIN
    EXEC('CREATE SCHEMA Agency');
END
GO

-- ============================================================
-- AGENCY PROFILE TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Agency') AND name = 'Profile')
BEGIN
    CREATE TABLE Agency.Profile (
        ProfileId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,

        -- Legal Information
        LegalName           NVARCHAR(255)    NOT NULL,
        DBA                 NVARCHAR(255)    NULL,
        LegalEntityType     NVARCHAR(100)    NULL,
        FederalTaxId        NVARCHAR(50)     NULL,
        LicenseNumber       NVARCHAR(100)    NULL,

        -- Contact Information
        ContactFirstName    NVARCHAR(100)    NOT NULL,
        ContactLastName     NVARCHAR(100)    NOT NULL,
        ContactEmail        NVARCHAR(200)    NOT NULL,
        ContactPhone        NVARCHAR(20)     NOT NULL,

        -- Address
        StreetAddress       NVARCHAR(255)    NOT NULL,
        City                NVARCHAR(100)    NOT NULL,
        State               NVARCHAR(50)     NOT NULL,
        ZipCode             NVARCHAR(10)     NOT NULL,
        Country             NVARCHAR(100)    NULL DEFAULT 'United States',

        -- E&O Insurance
        EoCarrier           NVARCHAR(200)    NULL,
        EoPolicyNumber      NVARCHAR(100)    NULL,
        EoExpiryDate        DATETIME2        NULL,
        EoCoverageAmount    DECIMAL(18,2)    NULL,

        -- Branding
        LogoUrl             NVARCHAR(500)    NULL,
        WebsiteUrl          NVARCHAR(500)    NULL,
        PrimaryColor        NVARCHAR(7)      NULL DEFAULT '#3b82f6',

        -- Audit
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        ModifiedByUserId    UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_Profile_TenantId ON Agency.Profile(TenantId, IsDeleted);
END
GO

-- ============================================================
-- BRANCHES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Agency') AND name = 'Branch')
BEGIN
    CREATE TABLE Agency.Branch (
        BranchId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,

        -- Branch Information
        BranchName          NVARCHAR(255)    NOT NULL,
        BranchCode          NVARCHAR(50)     NOT NULL UNIQUE,
        BranchType          NVARCHAR(100)    NULL,

        -- Location
        StreetAddress       NVARCHAR(255)    NOT NULL,
        City                NVARCHAR(100)    NOT NULL,
        State               NVARCHAR(50)     NOT NULL,
        ZipCode             NVARCHAR(10)     NOT NULL,
        Country             NVARCHAR(100)    NULL DEFAULT 'United States',

        -- Contact
        Phone               NVARCHAR(20)     NULL,
        Fax                 NVARCHAR(20)     NULL,
        Email               NVARCHAR(200)    NULL,

        -- Manager
        ManagerUserId       UNIQUEIDENTIFIER NULL,
        ManagerName         NVARCHAR(200)    NULL,

        -- Status
        IsActive            BIT              NOT NULL DEFAULT 1,
        IsHeadquarters      BIT              NOT NULL DEFAULT 0,

        -- Audit
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        ModifiedByUserId    UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_Branch_TenantId ON Agency.Branch(TenantId, IsActive, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Branch_Code ON Agency.Branch(BranchCode, IsDeleted);
END
GO

-- ============================================================
-- DEPARTMENTS TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Agency') AND name = 'Department')
BEGIN
    CREATE TABLE Agency.Department (
        DepartmentId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        BranchId            UNIQUEIDENTIFIER NOT NULL,

        -- Department Information
        DepartmentName      NVARCHAR(255)    NOT NULL,
        DepartmentCode      NVARCHAR(50)     NULL,
        Description         NVARCHAR(1000)   NULL,

        -- Manager
        ManagerUserId       UNIQUEIDENTIFIER NULL,
        ManagerName         NVARCHAR(200)    NULL,

        -- Status
        IsActive            BIT              NOT NULL DEFAULT 1,

        -- Audit
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,

        FOREIGN KEY (BranchId) REFERENCES Agency.Branch(BranchId)
    );

    CREATE NONCLUSTERED INDEX IX_Department_TenantId ON Agency.Department(TenantId, IsActive, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Department_BranchId ON Agency.Department(BranchId, IsActive, IsDeleted);
END
GO

-- ============================================================
-- TEAMS TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Agency') AND name = 'Team')
BEGIN
    CREATE TABLE Agency.Team (
        TeamId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        DepartmentId        UNIQUEIDENTIFIER NOT NULL,

        -- Team Information
        TeamName            NVARCHAR(255)    NOT NULL,
        TeamCode            NVARCHAR(50)     NULL,
        Description         NVARCHAR(1000)   NULL,

        -- Manager
        ManagerUserId       UNIQUEIDENTIFIER NULL,
        ManagerName         NVARCHAR(200)    NULL,

        -- Team Focus Area
        TeamType            NVARCHAR(100)    NULL,
        MemberCount         INT              NOT NULL DEFAULT 0,

        -- Status
        IsActive            BIT              NOT NULL DEFAULT 1,

        -- Audit
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,

        FOREIGN KEY (DepartmentId) REFERENCES Agency.Department(DepartmentId)
    );

    CREATE NONCLUSTERED INDEX IX_Team_TenantId ON Agency.Team(TenantId, IsActive, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Team_DepartmentId ON Agency.Team(DepartmentId, IsActive, IsDeleted);
END
GO

-- ============================================================
-- STAFF (PRODUCERS / CSRs) TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Agency') AND name = 'Staff')
BEGIN
    CREATE TABLE Agency.Staff (
        StaffId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        UserId              UNIQUEIDENTIFIER NULL,

        -- Personal Information
        FirstName           NVARCHAR(100)    NOT NULL,
        LastName            NVARCHAR(100)    NOT NULL,
        Email               NVARCHAR(200)    NOT NULL,
        Phone               NVARCHAR(20)     NULL,

        -- Professional Information
        Title               NVARCHAR(100)    NULL,
        Role                NVARCHAR(100)    NOT NULL,
        Department          NVARCHAR(100)    NULL,
        Team                NVARCHAR(100)    NULL,
        BranchId            UNIQUEIDENTIFIER NULL,

        -- Licensing
        LicenseType         NVARCHAR(100)    NULL,
        LicenseNumber       NVARCHAR(100)    NULL UNIQUE,
        LicenseStates       NVARCHAR(500)    NULL,
        LicenseExpiryDate   DATETIME2        NULL,
        LicenseRenewalDate  DATETIME2        NULL,

        -- Appointments
        AppointedCarriers   NVARCHAR(MAX)    NULL,
        CommissionRate      DECIMAL(5,2)     NULL,

        -- Status
        IsActive            BIT              NOT NULL DEFAULT 1,
        EmploymentStatus    NVARCHAR(50)     NULL DEFAULT 'Active',
        HireDate            DATETIME2        NULL,
        TerminationDate     DATETIME2        NULL,

        -- Audit
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        ModifiedByUserId    UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,

        FOREIGN KEY (BranchId) REFERENCES Agency.Branch(BranchId)
    );

    CREATE NONCLUSTERED INDEX IX_Staff_TenantId ON Agency.Staff(TenantId, IsActive, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Staff_LicenseNumber ON Agency.Staff(LicenseNumber, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Staff_LicenseExpiry ON Agency.Staff(LicenseExpiryDate) WHERE IsDeleted = 0 AND LicenseExpiryDate IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Staff_Role ON Agency.Staff(Role, IsActive, IsDeleted);
END
GO

-- ============================================================
-- SEED DATA
-- ============================================================

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';
DECLARE @HeadquartersId UNIQUEIDENTIFIER;
DECLARE @NYBranchId UNIQUEIDENTIFIER;
DECLARE @LAbenchId UNIQUEIDENTIFIER;
DECLARE @SalesDepId UNIQUEIDENTIFIER;
DECLARE @ClaimsDepId UNIQUEIDENTIFIER;

-- Insert Agency Profile
IF NOT EXISTS (SELECT 1 FROM Agency.Profile WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Agency.Profile (
        ProfileId, TenantId, LegalName, DBA, LegalEntityType, FederalTaxId,
        ContactFirstName, ContactLastName, ContactEmail, ContactPhone,
        StreetAddress, City, State, ZipCode,
        EoCarrier, EoPolicyNumber, EoExpiryDate, EoCoverageAmount,
        CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'AgencyBinder, Inc.', 'AgencyBinder', 'C-Corporation', '12-3456789',
        'John', 'Doe', 'john.doe@agencybinder.com', '(212) 555-0001',
        '123 Insurance Plaza', 'New York', 'NY', '10001',
        'Berkley Insurance', 'POL-2024-001', DATEADD(YEAR, 1, GETUTCDATE()), 1000000,
        @UserId
    );
END

-- Insert Branches
IF NOT EXISTS (SELECT 1 FROM Agency.Branch WHERE TenantId = @TenantId AND BranchCode = 'HQ-001')
BEGIN
    SET @HeadquartersId = NEWID();
    INSERT INTO Agency.Branch (
        BranchId, TenantId, BranchName, BranchCode, StreetAddress, City, State, ZipCode,
        Phone, Email, ManagerName, IsActive, IsHeadquarters, CreatedByUserId
    ) VALUES (
        @HeadquartersId, @TenantId, 'Headquarters', 'HQ-001', '123 Insurance Plaza', 'New York', 'NY', '10001',
        '(212) 555-0001', 'hq@agencybinder.com', 'John Doe', 1, 1, @UserId
    );

    SET @NYBranchId = NEWID();
    INSERT INTO Agency.Branch (
        BranchId, TenantId, BranchName, BranchCode, StreetAddress, City, State, ZipCode,
        Phone, Email, ManagerName, IsActive, CreatedByUserId
    ) VALUES (
        @NYBranchId, @TenantId, 'New York Downtown', 'NY-001', '456 Madison Ave', 'New York', 'NY', '10016',
        '(212) 555-0002', 'ny@agencybinder.com', 'Sarah Johnson', 1, @UserId
    );

    SET @LAbenchId = NEWID();
    INSERT INTO Agency.Branch (
        BranchId, TenantId, BranchName, BranchCode, StreetAddress, City, State, ZipCode,
        Phone, Email, ManagerName, IsActive, CreatedByUserId
    ) VALUES (
        @LAbenchId, @TenantId, 'Los Angeles', 'LA-001', '789 Hollywood Blvd', 'Los Angeles', 'CA', '90001',
        '(213) 555-0003', 'la@agencybinder.com', 'Mike Davis', 1, @UserId
    );
END

-- Insert Departments
IF NOT EXISTS (SELECT 1 FROM Agency.Department WHERE TenantId = @TenantId AND DepartmentName = 'Sales')
BEGIN
    SET @SalesDepId = NEWID();
    INSERT INTO Agency.Department (
        DepartmentId, TenantId, BranchId, DepartmentName, DepartmentCode, Description,
        ManagerName, IsActive, CreatedByUserId
    ) VALUES (
        @SalesDepId, @TenantId, @HeadquartersId, 'Sales', 'SALES-001', 'Commercial and personal lines sales',
        'Sarah Johnson', 1, @UserId
    );

    SET @ClaimsDepId = NEWID();
    INSERT INTO Agency.Department (
        DepartmentId, TenantId, BranchId, DepartmentName, DepartmentCode, Description,
        ManagerName, IsActive, CreatedByUserId
    ) VALUES (
        @ClaimsDepId, @TenantId, @HeadquartersId, 'Claims', 'CLAIMS-001', 'Claims processing and management',
        'Mike Davis', 1, @UserId
    );

    INSERT INTO Agency.Department (
        DepartmentId, TenantId, BranchId, DepartmentName, DepartmentCode, Description,
        ManagerName, IsActive, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, @HeadquartersId, 'Operations', 'OPS-001', 'Back office operations',
        'Lisa Anderson', 1, @UserId
    );
END

-- Insert Teams
IF NOT EXISTS (SELECT 1 FROM Agency.Team WHERE TenantId = @TenantId AND TeamName = 'Sales East')
BEGIN
    INSERT INTO Agency.Team (
        TeamId, TenantId, DepartmentId, TeamName, TeamCode, Description, ManagerName, TeamType, MemberCount, IsActive, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, @SalesDepId, 'Sales East', 'SALES-EAST', 'Commercial lines - Eastern territory', 'Sarah Johnson', 'Commercial', 8, 1, @UserId
    );

    INSERT INTO Agency.Team (
        TeamId, TenantId, DepartmentId, TeamName, TeamCode, Description, ManagerName, TeamType, MemberCount, IsActive, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, @SalesDepId, 'Sales West', 'SALES-WEST', 'Personal lines - Western territory', 'Mike Davis', 'Personal', 6, 1, @UserId
    );

    INSERT INTO Agency.Team (
        TeamId, TenantId, DepartmentId, TeamName, TeamCode, Description, ManagerName, TeamType, MemberCount, IsActive, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, @ClaimsDepId, 'Claims Processing', 'CLAIMS-PROC', 'First notice of loss processing', 'Robert Brown', 'Claims', 12, 1, @UserId
    );
END

-- Insert Staff
IF NOT EXISTS (SELECT 1 FROM Agency.Staff WHERE TenantId = @TenantId AND LicenseNumber = 'NPN123001')
BEGIN
    INSERT INTO Agency.Staff (
        StaffId, TenantId, FirstName, LastName, Email, Phone,
        Title, Role, Department, BranchId,
        LicenseType, LicenseNumber, LicenseExpiryDate,
        IsActive, EmploymentStatus, HireDate, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'John', 'Smith', 'john.smith@agencybinder.com', '(212) 555-1001',
        'Senior Producer', 'Producer', 'Sales', @HeadquartersId,
        'Property & Casualty', 'NPN123001', DATEADD(YEAR, 1, GETUTCDATE()),
        1, 'Active', DATEADD(YEAR, -5, GETUTCDATE()), @UserId
    );

    INSERT INTO Agency.Staff (
        StaffId, TenantId, FirstName, LastName, Email, Phone,
        Title, Role, Department, BranchId,
        LicenseType, LicenseNumber, LicenseExpiryDate,
        IsActive, EmploymentStatus, HireDate, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'Sarah', 'Johnson', 'sarah.johnson@agencybinder.com', '(212) 555-1002',
        'Producer', 'Producer', 'Sales', @NYBranchId,
        'Property & Casualty', 'NPN123002', DATEADD(MONTH, 6, GETUTCDATE()),
        1, 'Active', DATEADD(YEAR, -3, GETUTCDATE()), @UserId
    );

    INSERT INTO Agency.Staff (
        StaffId, TenantId, FirstName, LastName, Email, Phone,
        Title, Role, Department, BranchId,
        LicenseType, LicenseNumber, LicenseExpiryDate,
        IsActive, EmploymentStatus, HireDate, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'Mike', 'Davis', 'mike.davis@agencybinder.com', '(212) 555-1003',
        'CSR', 'CSR', 'Operations', @HeadquartersId,
        'Life', 'NPN123003', DATEADD(MONTH, 3, GETUTCDATE()),
        1, 'Active', DATEADD(YEAR, -2, GETUTCDATE()), @UserId
    );

    INSERT INTO Agency.Staff (
        StaffId, TenantId, FirstName, LastName, Email, Phone,
        Title, Role, Department, BranchId,
        LicenseType, LicenseNumber, LicenseExpiryDate,
        IsActive, EmploymentStatus, HireDate, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'Lisa', 'Anderson', 'lisa.anderson@agencybinder.com', '(212) 555-1004',
        'Claims Manager', 'Manager', 'Claims', @HeadquartersId,
        'Property & Casualty', 'NPN123004', DATEADD(YEAR, 2, GETUTCDATE()),
        1, 'Active', DATEADD(YEAR, -8, GETUTCDATE()), @UserId
    );

    INSERT INTO Agency.Staff (
        StaffId, TenantId, FirstName, LastName, Email, Phone,
        Title, Role, Department, BranchId,
        LicenseType, LicenseNumber, LicenseExpiryDate,
        IsActive, EmploymentStatus, HireDate, CreatedByUserId
    ) VALUES (
        NEWID(), @TenantId, 'Robert', 'Brown', 'robert.brown@agencybinder.com', '(213) 555-1005',
        'Claims Adjuster', 'CSR', 'Claims', @LAbenchId,
        'Property & Casualty', 'NPN123005', DATEADD(MONTH, -2, GETUTCDATE()),
        0, 'Inactive', DATEADD(YEAR, -1, GETUTCDATE()), @UserId
    );
END

PRINT 'Migration 0045 - Agency Setup Schema - Complete';
