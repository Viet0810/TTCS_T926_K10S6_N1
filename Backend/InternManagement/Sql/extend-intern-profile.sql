IF COL_LENGTH(N'dbo.Interns', N'StudentCode') IS NULL
    ALTER TABLE dbo.Interns ADD [StudentCode] NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Interns', N'ClassName') IS NULL
    ALTER TABLE dbo.Interns ADD [ClassName] NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Faculty') IS NULL
    ALTER TABLE dbo.Interns ADD [Faculty] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'DateOfBirth') IS NULL
    ALTER TABLE dbo.Interns ADD [DateOfBirth] DATE NULL;
IF COL_LENGTH(N'dbo.Interns', N'Address') IS NULL
    ALTER TABLE dbo.Interns ADD [Address] NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Organization') IS NULL
    ALTER TABLE dbo.Interns ADD [Organization] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'OrganizationAddress') IS NULL
    ALTER TABLE dbo.Interns ADD [OrganizationAddress] NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Department') IS NULL
    ALTER TABLE dbo.Interns ADD [Department] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Position') IS NULL
    ALTER TABLE dbo.Interns ADD [Position] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Mentor') IS NULL
    ALTER TABLE dbo.Interns ADD [Mentor] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'MentorEmail') IS NULL
    ALTER TABLE dbo.Interns ADD [MentorEmail] NVARCHAR(254) NULL;
IF COL_LENGTH(N'dbo.Interns', N'MentorPhone') IS NULL
    ALTER TABLE dbo.Interns ADD [MentorPhone] NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Interns', N'AcademicSupervisor') IS NULL
    ALTER TABLE dbo.Interns ADD [AcademicSupervisor] NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Interns', N'StartDate') IS NULL
    ALTER TABLE dbo.Interns ADD [StartDate] DATE NULL;
IF COL_LENGTH(N'dbo.Interns', N'EndDate') IS NULL
    ALTER TABLE dbo.Interns ADD [EndDate] DATE NULL;
IF COL_LENGTH(N'dbo.Interns', N'Status') IS NULL
    ALTER TABLE dbo.Interns ADD [Status] NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Interns', N'InternshipTopic') IS NULL
    ALTER TABLE dbo.Interns ADD [InternshipTopic] NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.Interns', N'Notes') IS NULL
    ALTER TABLE dbo.Interns ADD [Notes] NVARCHAR(2000) NULL;
