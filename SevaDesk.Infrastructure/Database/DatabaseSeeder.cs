using Dapper;
using Microsoft.Data.Sqlite;

namespace SevaDesk.Infrastructure.Database;

public static class DatabaseSeeder
{
    public static void SeedAll(SqliteConnection connection)
    {
        SeedSettings(connection);
        SeedResources(connection);
        SeedApplicationTemplates(connection);
        SeedServiceRates(connection);
    }

    public static void SeedSettings(SqliteConnection connection)
    {
        var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM settings;");
        if (count > 0) return;

        var defaultSettings = new[]
        {
            new { Key = "shop_name", Value = "SevaDesk Digital Cyber Café" },
            new { Key = "operator_name", Value = "Ramesh Patel (VLE / Operator)" },
            new { Key = "csc_vle_id", Value = "CSC-MH-2024-9842" },
            new { Key = "contact_number", Value = "+91 98765 43210" },
            new { Key = "shop_address", Value = "Shop #4, Panchayat Complex, Main Market" },
            new { Key = "shop_upi_vpa", Value = "sevadesk.csc@upi" },
            new { Key = "shop_upi_id", Value = "sevadesk.csc@upi" },
            new { Key = "payee_name", Value = "SevaDesk Cyber Center" },
            new { Key = "default_bw_printer", Value = "Brother DCP-L2520D series" },
            new { Key = "default_color_printer", Value = "Epson EcoTank L8050 Photo" },
            new { Key = "close_behavior", Value = "0" }
        };

        const string insertSql = @"
            INSERT OR IGNORE INTO settings (key, value)
            VALUES (@Key, @Value);";

        connection.Execute(insertSql, defaultSettings);
    }

    public static void SeedServiceRates(SqliteConnection connection)
    {
        var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM service_rates;");
        if (count > 0) return;

        var seedRates = new[]
        {
            new { Id = Guid.NewGuid().ToString(), ServiceName = "B&W Print (Single)", Rate = 5.0, Unit = "page", Category = "Printing", Glyph = "\uE749", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "B&W Print (Both Sides)", Rate = 10.0, Unit = "page", Category = "Printing", Glyph = "\uE749", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "Color Print", Rate = 20.0, Unit = "page", Category = "Printing", Glyph = "\uE790", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "Document Scan to PDF", Rate = 15.0, Unit = "doc", Category = "Scanning", Glyph = "\uE8A5", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "A4 Lamination", Rate = 30.0, Unit = "sheet", Category = "Finishing", Glyph = "\uE7C3", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "PVC Card (Aadhaar/PAN)", Rate = 70.0, Unit = "card", Category = "Cards", Glyph = "\uE8C7", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "Govt Online Form Fill", Rate = 100.0, Unit = "application", Category = "Services", Glyph = "\uE77B", IsActive = 1 },
            new { Id = Guid.NewGuid().ToString(), ServiceName = "Urgent Typing / Affidavit", Rate = 80.0, Unit = "page", Category = "Services", Glyph = "\uE8C1", IsActive = 1 }
        };

        const string insertSeedSql = @"
            INSERT INTO service_rates (id, service_name, rate, unit, category, glyph, is_active)
            VALUES (@Id, @ServiceName, @Rate, @Unit, @Category, @Glyph, @IsActive);";

        connection.Execute(insertSeedSql, seedRates);
    }

    public static void SeedResources(SqliteConnection connection)
    {
        var resourceCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM resources;");
        if (resourceCount > 0) return;

        var seedResources = new[]
        {
            new { Id = Guid.NewGuid().ToString(), Title = "Form 49A — New PAN Card Physical Form", Category = "Blank Forms", FileType = "PDF", FileSize = "1.2 MB", FilePath = "", Glyph = "\uE8A5", IsFavorite = 1, RequiredDocs = "Aadhaar Card, 2 Passport Photos, Signature", Notes = "Standard physical application form for NSDL/UTIITSL PAN center submission.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Income Declaration Affidavit (Standard Format ₹10/₹100 Stamp)", Category = "Affidavits", FileType = "DOCX", FileSize = "45 KB", FilePath = "", Glyph = "\uE8C1", IsFavorite = 1, RequiredDocs = "Aadhaar Card, Ration Card / Parivar Register, Land / Salary Slip", Notes = "Must be executed on ₹10 or ₹100 non-judicial stamp paper and notarized.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Educational Gap Year Affidavit (College/Job)", Category = "Affidavits", FileType = "DOCX", FileSize = "38 KB", FilePath = "", Glyph = "\uE8C1", IsFavorite = 1, RequiredDocs = "Last Qualification Marksheet, Aadhaar Card", Notes = "Required for university, college, and government job document verification.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Name Correction / Alias Affidavit (Govt Gazette)", Category = "Affidavits", FileType = "DOCX", FileSize = "52 KB", FilePath = "", Glyph = "\uE8C1", IsFavorite = 0, RequiredDocs = "High School Certificate, Aadhaar Card, ID with Old Name", Notes = "Standard format for newspaper advertisement and state gazette notification.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Caste Certificate Application Form (State Standard)", Category = "Blank Forms", FileType = "PDF", FileSize = "890 KB", FilePath = "", Glyph = "\uE8A5", IsFavorite = 0, RequiredDocs = "Aadhaar Card, Father's Caste Proof / Land Record, Ration Card", Notes = "Verification form submitted to Lekhpal / Revenue Inspector.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Lost Marksheet / Certificate Police Intimation Format", Category = "Affidavits", FileType = "DOCX", FileSize = "34 KB", FilePath = "", Glyph = "\uE8C1", IsFavorite = 0, RequiredDocs = "Marksheet Copy / Roll Number, Aadhaar Card", Notes = "Police DDR application format before applying for duplicate marksheet.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Domicile / Residence Certificate Proforma", Category = "Blank Forms", FileType = "PDF", FileSize = "620 KB", FilePath = "", Glyph = "\uE8A5", IsFavorite = 0, RequiredDocs = "Electricity Bill, Voter ID / Aadhaar, 3 Years Residence Proof", Notes = "Submitted to Tehsildar office for resident certificate issuance.", LastModified = DateTime.Now.ToString("o") },
            new { Id = Guid.NewGuid().ToString(), Title = "Character Certificate Proforma (Gazetted Officer)", Category = "Blank Forms", FileType = "DOCX", FileSize = "28 KB", FilePath = "", Glyph = "\uE8C1", IsFavorite = 0, RequiredDocs = "Aadhaar Card, Passport Photo", Notes = "To be signed by a Gazetted Officer, Principal, or Village Pradhan.", LastModified = DateTime.Now.ToString("o") }
        };

        const string insertSeedSql = @"
            INSERT INTO resources (id, title, category, file_type, file_size, file_path, glyph, is_favorite, required_docs, notes, last_modified)
            VALUES (@Id, @Title, @Category, @FileType, @FileSize, @FilePath, @Glyph, @IsFavorite, @RequiredDocs, @Notes, @LastModified);";

        connection.Execute(insertSeedSql, seedResources);
    }

    public static void SeedApplicationTemplates(SqliteConnection connection)
    {
        var templateCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM application_templates;");
        if (templateCount > 0) return;

        var seedTemplates = new[]
        {
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "SSC CGL / CHSL 2026",
                Category = "Jobs & Exams",
                PortalUrl = "https://ssc.gov.in",
                DefaultServiceFee = 100.0,
                DefaultGovtFee = 100.0,
                RequiredDocs = "Photo, Signature, Aadhaar, Graduation Marksheet",
                Notes = "Combined Graduate Level Examination — SSC Official Portal",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            },
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "PAN Card (Form 49A)",
                Category = "Identity & Tax",
                PortalUrl = "https://www.onlineservices.nsdl.com/paam/endUserRegisterContact.html",
                DefaultServiceFee = 100.0,
                DefaultGovtFee = 107.0,
                RequiredDocs = "Aadhaar, Passport Photo, Signature, Mobile Linked OTP",
                Notes = "New PAN Card physical delivery with e-PAN",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            },
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "State Scholarship (Pre / Post Matric)",
                Category = "Scholarships",
                PortalUrl = "https://scholarship.up.gov.in",
                DefaultServiceFee = 120.0,
                DefaultGovtFee = 0.0,
                RequiredDocs = "Income Certificate, Caste Certificate, Marksheet, Bank Passbook, Aadhaar, Fee Receipt",
                Notes = "Government scholarship for school & college students",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            },
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "PM Kisan Samman Nidhi (E-KYC / New Registration)",
                Category = "Govt Schemes",
                PortalUrl = "https://pmkisan.gov.in",
                DefaultServiceFee = 50.0,
                DefaultGovtFee = 0.0,
                RequiredDocs = "Aadhaar, Land Record (Khatauni), Bank Passbook, Mobile OTP",
                Notes = "Farmer annual support scheme e-KYC & status check",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            },
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "Railway RRB Recruitment",
                Category = "Jobs & Exams",
                PortalUrl = "https://www.rrbapply.gov.in",
                DefaultServiceFee = 100.0,
                DefaultGovtFee = 500.0,
                RequiredDocs = "Photo, Signature, 10th / ITI Marksheet, Aadhaar, Community Certificate",
                Notes = "Railway Recruitment Board online application",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            },
            new {
                Id = Guid.NewGuid().ToString(),
                Title = "Passport Seva Online",
                Category = "Online Services",
                PortalUrl = "https://www.passportindia.gov.in",
                DefaultServiceFee = 200.0,
                DefaultGovtFee = 1500.0,
                RequiredDocs = "Aadhaar, Birth Certificate / 10th Marksheet, PAN, Bank Passbook / Electricity Bill",
                Notes = "Normal / Tatkaal fresh passport application & appointment booking",
                IsActive = 1,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            }
        };

        const string insertSeedTemplateSql = @"
            INSERT INTO application_templates (id, title, category, portal_url, default_service_fee, default_govt_fee, required_docs, notes, is_active, created_at, updated_at)
            VALUES (@Id, @Title, @Category, @PortalUrl, @DefaultServiceFee, @DefaultGovtFee, @RequiredDocs, @Notes, @IsActive, @CreatedAt, @UpdatedAt);";

        connection.Execute(insertSeedTemplateSql, seedTemplates);
    }
}
