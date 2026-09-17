using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<ApplicationRepository> _logger;
    private readonly string _schema;

    public ApplicationRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions, ILogger<ApplicationRepository> logger)
    {
        _context = context;
        _logger = logger;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    private string SelectColumns => $@"
SELECT application_id AS AppId, status_code AS StatusCode, add_ts AS AddDate, add_by AS AddBy,
       project_site_city_code AS SiteCityCode,
       (SELECT TOP 1 cc.city_name FROM [{_schema}].[CITY_CODES] cc WHERE cc.city_code = [{_schema}].[APPLICATION_INFO].project_site_city_code) AS SiteCityName,
       project_site_location AS SiteLocation,
       project_site_lat AS SiteLat, project_site_long AS SiteLong,
       project_start_date AS ProjStartDate, project_end_date AS ProjEndDate,
       SITE_HAZARD_REQUIRED AS SiteHazardRequired, site_visit_type AS SiteVisitType,
       sitemap_filename AS SitemapFilename, sitemap_received_date AS SitemapReceivedDate,
       extend_start_date AS ExtendStartDate, extend_end_date AS ExtendEndDate,
       extend_count AS ExtendCount, extend_by AS ExtendBy,
       app_business_name AS AppBusinessName, app_last_name AS AppLastName, app_first_name AS AppFirstName,
       app_email_addr AS AppEmailAddr, app_addr_street AS AppAddrStreet, app_addr_street2 AS AppAddrStreet2,
       app_addr_city_name AS AppAddrCity, app_addr_state_code AS AppAddrState, app_addr_zip AS AppAddrZip,
       app_phone_num AS AppPhone, app_phone_fax AS AppFax,
       contact_last_name AS ContactLastName, contact_first_name AS ContactFirstName,
       contact_email_addr AS ContactEmail, contact_phone_num AS ContactPhone, contact_phone_cell AS ContactCell,
       owner_last_name AS OwnerLastName, owner_first_name AS OwnerFirstName, owner_addr_street AS OwnerAddrStreet,
       owner_addr_city_name AS OwnerAddrCity, owner_addr_state_code AS OwnerAddrState, owner_addr_zip AS OwnerAddrZip,
       owner_phone_num AS OwnerPhone, owner_email_addr AS OwnerEmail,
       client_last_name AS ClientLastName, client_first_name AS ClientFirstName, client_addr_street AS ClientAddrStreet,
       client_addr_city_name AS ClientAddrCity, client_addr_state_code AS ClientAddrState, client_addr_zip AS ClientAddrZip,
       client_phone_num AS ClientPhone, client_email_addr AS ClientEmail
FROM [{_schema}].[APPLICATION_INFO]";

    public async Task<DomainApplication?> GetByIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $"{SelectColumns}\nWHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ApplicationRow>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var works = (await connection.QueryAsync<WorkRow>(new CommandDefinition($@"
SELECT wrk.application_id AS AppId, wrk.work_id AS WorkId, wrk.work_category AS WorkCategory, wrk.work_type AS WorkType,
       wrk.well_use_type AS WellUseType, wrk.driller_name AS DrillerName, wrk.driller_license_num AS DrillerLicenseNum,
       wrk.drill_method_type AS DrillMethodType, wrk.drill_method_other_desc AS DrillMethodOtherDesc,
       wrk.work_fee_rate AS WorkFeeRate, wrk.work_fee_unit AS WorkFeeUnit, wrk.work_site_max AS WorkSiteMax,
       wrk.work_site_extra_rate AS WorkSiteExtraRate, wrk.status_code AS StatusCode,
       wca.work_cat_desc AS WorkCategoryDesc, wtp.work_desc AS WorkTypeDesc,
       wus.well_use_desc AS WellUseDesc, dmt.drill_method_name AS DrillMethodName
FROM [{_schema}].[APP_WORKS] wrk
LEFT JOIN [{_schema}].[WORK_CATEGORIES] wca ON wrk.work_category = wca.work_category
LEFT JOIN [{_schema}].[WORK_TYPES] wtp ON wrk.work_category = wtp.work_category AND wrk.work_type = wtp.work_type
LEFT JOIN [{_schema}].[DRILL_METHOD_TYPES] dmt ON wrk.drill_method_type = dmt.drill_method_type
LEFT JOIN [{_schema}].[WORK_USE_TYPES] wus ON wrk.work_category = wus.work_category AND wrk.work_type = wus.work_type AND wrk.well_use_type = wus.well_use_type
WHERE wrk.application_id = @AppId
ORDER BY wrk.work_id;", new { AppId = appId }, cancellationToken: cancellationToken))).ToList();

        var specs = (await connection.QueryAsync<SpecRow>(new CommandDefinition($@"
SELECT application_id AS AppId, work_id AS WorkId, work_specs_id AS WorkSpecsId, owner_well_num AS OwnerWellNum,
       drill_count AS DrillCount, hole_diam_in AS HoleDiamIn, casing_diam_in AS CasingDiamIn,
       seal_depth_ft AS SealDepthFt, max_depth_ft AS MaxDepthFt, latitude AS Latitude, longitude AS Longitude,
       state_well_id AS StateWellId, dwr_num AS DwrNum, permit_num AS PermitNum,
       compl_well_dwr_num AS ComplWellDwrNum, dwr_num_shared AS DwrNumShared, dwr_image AS DwrImage, geolog_file AS GeologFile,
       status_code AS StatusCode
FROM [{_schema}].[APP_WORK_SPECS]
WHERE application_id = @AppId
ORDER BY work_id, work_specs_id;", new { AppId = appId }, cancellationToken: cancellationToken))).ToList();

        var domainWorks = works.Select(w =>
        {
            var work = w.ToDomain();
            work.Specs = specs.Where(s => s.WorkId == w.WorkId).Select(s => s.ToDomain()).ToList();
            return work;
        }).ToList();

        var application = row.ToDomain(domainWorks);
        application.Hazard = await ReadHazardAsync(connection, appId, cancellationToken);

        application.Documents = (await connection.QueryAsync<DocumentRow>(new CommandDefinition($@"
SELECT APPLICATION_ID AS AppId, SEQ_NUM AS SeqNum, DOCUMENT_TYPE AS DocumentType,
       OTHER_TYPE_DESC AS OtherTypeDesc, DOCUMENT_FILENAME AS DocumentFilename
FROM [{_schema}].[APP_DOCUMENT_LINKS]
WHERE APPLICATION_ID = @AppId
ORDER BY SEQ_NUM;", new { AppId = appId }, cancellationToken: cancellationToken)))
            .Select(d => d.ToDomain()).ToList();

        application.EmailCcs = (await connection.QueryAsync<EmailCcRow>(new CommandDefinition($@"
SELECT application_id AS AppId, email_cc_id AS EmailCcId, email_addr_cc AS EmailAddrCc
FROM [{_schema}].[APP_EMAIL_CC]
WHERE application_id = @AppId
ORDER BY email_cc_id;", new { AppId = appId }, cancellationToken: cancellationToken)))
            .Select(e => e.ToDomain()).ToList();

        application.Notes = (await connection.QueryAsync<NoteRow>(new CommandDefinition($@"
SELECT application_id AS AppId, note_id AS NoteId, add_by AS AddBy, add_ts AS AddTs, notes_text AS NotesText
FROM [{_schema}].[APP_NOTES]
WHERE application_id = @AppId
ORDER BY note_id;", new { AppId = appId }, cancellationToken: cancellationToken)))
            .Select(n => n.ToDomain()).ToList();

        return application;
    }

    private async Task<HazardInfo?> ReadHazardAsync(Microsoft.Data.SqlClient.SqlConnection connection, string appId, CancellationToken cancellationToken)
    {
        var hazardRow = await connection.QuerySingleOrDefaultAsync<HazardRow>(new CommandDefinition($@"
SELECT APPLICATION_ID AS AppId, ONSITE_CONSULTANT_LAST_NAME AS ConsultantLastName,
       ONSITE_CONSULTANT_FIRST_NAME AS ConsultantFirstName, ONSITE_CONSULTANT_PHONE_NUM AS ConsultantPhone,
       ONSITE_CONSULTANT_CELL_NUM AS ConsultantCell, SAFETY_OFFICER_LAST_NAME AS SafetyOfficerLastName,
       SAFETY_OFFICER_FIRST_NAME AS SafetyOfficerFirstName, SAFETY_OFFICER_PHONE_NUM AS SafetyOfficerPhone,
       SAFETY_OFFICER_CELL_NUM AS SafetyOfficerCell, FACILITY_TYPE AS FacilityType,
       SITE_SAFETY_MEETING_DATETIME AS SiteSafetyMeetingDateTime,
       PERSONAL_PROTECTION_EQUIP_LEVEL_A AS PpeLevelA, PERSONAL_PROTECTION_EQUIP_LEVEL_B AS PpeLevelB,
       PERSONAL_PROTECTION_EQUIP_LEVEL_C AS PpeLevelC, PERSONAL_PROTECTION_EQUIP_LEVEL_D AS PpeLevelD,
       EQUIP_HARD_HAT_FLAG AS EquipHardHatFlag, EQUIP_SAFETY_SHOES_FLAG AS EquipSafetyShoesFlag,
       EQUIP_ORANGE_VEST_FLAG AS EquipOrangeVestFlag, EQUIP_HEARING_PROT_FLAG AS EquipHearingProtFlag,
       EQUIP_SAFETY_EYEWEAR_FLAG AS EquipSafetyEyewearFlag, EQUIP_CLOTHING_FLAG AS EquipClothingFlag,
       EQUIP_CLOTHING_DESC AS EquipClothingDesc, EQUIP_RESPIRATOR_FLAG AS EquipRespiratorFlag,
       EQUIP_RESPIRATOR_DESC AS EquipRespiratorDesc, EQUIP_CARTRIDGE_FLAG AS EquipCartridgeFlag,
       EQUIP_CARTRIDGE_DESC AS EquipCartridgeDesc, EQUIP_GLOVES_FLAG AS EquipGlovesFlag,
       EQUIP_GLOVES_DESC AS EquipGlovesDesc, EQUIP_OTHER_FLAG AS EquipOtherFlag,
       EQUIP_OTHER_DESC AS EquipOtherDesc, INFO_PROVIDED_BY_COMPANY_NAME AS InfoProvidedByCompanyName,
       INFO_PROVIDED_BY_LAST_NAME AS InfoProvidedByLastName, INFO_PROVIDED_BY_FIRST_NAME AS InfoProvidedByFirstName,
       INFO_PROVIDED_BY_TITLE AS InfoProvidedByTitle, INFO_PROVIDED_BY_PHONE_NUM AS InfoProvidedByPhone,
       ACKNOWLEDGEMENT AS Acknowledgement, ADD_TS AS AddTs
FROM [{_schema}].[APP_HAZARD_INFO]
WHERE APPLICATION_ID = @AppId;", new { AppId = appId }, cancellationToken: cancellationToken));

        if (hazardRow is null)
        {
            return null;
        }

        var hazard = hazardRow.ToDomain();

        hazard.Contaminations = (await connection.QueryAsync<string>(new CommandDefinition($@"
SELECT CONTAMINATION FROM [{_schema}].[APP_SITE_CONTAMINATIONS]
WHERE APPLICATION_ID = @AppId
ORDER BY CONTAMINATION;", new { AppId = appId }, cancellationToken: cancellationToken))).ToList();

        hazard.Substances = (await connection.QueryAsync<SubstanceRow>(new CommandDefinition($@"
SELECT APPLICATION_ID AS AppId, HAZARD_SEQ AS HazardSeq, CONCENTRATIONS_PPM AS ConcentrationsPpm,
       PEL_PPM AS PelPpm, HEALTH_EFFECTS AS HealthEffects
FROM [{_schema}].[APP_HAZARD_SUBSTANCE]
WHERE APPLICATION_ID = @AppId
ORDER BY HAZARD_SEQ;", new { AppId = appId }, cancellationToken: cancellationToken)))
            .Select(s => s.ToDomain()).ToList();

        return hazard;
    }

    public async Task<DomainApplication> CreateAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sql = $@"
INSERT INTO [{_schema}].[APPLICATION_INFO]
    (application_id, status_code, add_ts, add_by, project_site_city_code, project_site_location,
     project_site_lat, project_site_long, project_start_date, project_end_date, SITE_HAZARD_REQUIRED,
     sitemap_filename, app_business_name, app_last_name, app_first_name, app_email_addr,
     app_addr_street, app_addr_street2, app_addr_city_name, app_addr_state_code, app_addr_zip,
     app_phone_num, app_phone_fax, contact_last_name, contact_first_name, contact_email_addr,
     contact_phone_num, contact_phone_cell, owner_last_name, owner_first_name, owner_addr_street,
     owner_addr_city_name, owner_addr_state_code, owner_addr_zip, owner_phone_num, owner_email_addr,
     client_last_name, client_first_name, client_addr_street, client_addr_city_name, client_addr_state_code,
     client_addr_zip, client_phone_num, client_email_addr)
VALUES
    (@AppId, @StatusCode, @AddDate, @AddBy, @SiteCityCode, @SiteLocation,
     @SiteLat, @SiteLong, @ProjStartDate, @ProjEndDate, @SiteHazardRequired,
     @SitemapFilename, @AppBusinessName, @AppLastName, @AppFirstName, @AppEmailAddr,
     @AppAddrStreet, @AppAddrStreet2, @AppAddrCity, @AppAddrState, @AppAddrZip,
     @AppPhone, @AppFax, @ContactLastName, @ContactFirstName, @ContactEmail,
     @ContactPhone, @ContactCell, @OwnerLastName, @OwnerFirstName, @OwnerAddrStreet,
     @OwnerAddrCity, @OwnerAddrState, @OwnerAddrZip, @OwnerPhone, @OwnerEmail,
     @ClientLastName, @ClientFirstName, @ClientAddrStreet, @ClientAddrCity, @ClientAddrState,
     @ClientAddrZip, @ClientPhone, @ClientEmail);";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            application.AppId,
            application.StatusCode,
            application.AddDate,
            application.AddBy,
            application.SiteCityCode,
            application.SiteLocation,
            application.SiteLat,
            application.SiteLong,
            application.ProjStartDate,
            application.ProjEndDate,
            application.SiteHazardRequired,
            application.SitemapFilename,
            application.Applicant.AppBusinessName,
            application.Applicant.AppLastName,
            application.Applicant.AppFirstName,
            application.Applicant.AppEmailAddr,
            application.Applicant.AppAddrStreet,
            application.Applicant.AppAddrStreet2,
            application.Applicant.AppAddrCity,
            application.Applicant.AppAddrState,
            application.Applicant.AppAddrZip,
            application.Applicant.AppPhone,
            application.Applicant.AppFax,
            application.Contact.ContactLastName,
            application.Contact.ContactFirstName,
            application.Contact.ContactEmail,
            application.Contact.ContactPhone,
            application.Contact.ContactCell,
            OwnerLastName = application.Owner.LastName,
            OwnerFirstName = application.Owner.FirstName,
            OwnerAddrStreet = application.Owner.AddrStreet,
            OwnerAddrCity = application.Owner.AddrCity,
            OwnerAddrState = application.Owner.AddrState,
            OwnerAddrZip = application.Owner.AddrZip,
            OwnerPhone = application.Owner.Phone,
            OwnerEmail = application.Owner.Email,
            ClientLastName = application.Client.LastName,
            ClientFirstName = application.Client.FirstName,
            ClientAddrStreet = application.Client.AddrStreet,
            ClientAddrCity = application.Client.AddrCity,
            ClientAddrState = application.Client.AddrState,
            ClientAddrZip = application.Client.AddrZip,
            ClientPhone = application.Client.Phone,
            ClientEmail = application.Client.Email
        }, transaction, cancellationToken: cancellationToken));

        foreach (var work in application.Works)
        {
            var workSql = $@"
INSERT INTO [{_schema}].[APP_WORKS]
    (application_id, work_id, work_category, work_type, well_use_type, driller_name, driller_license_num,
     drill_method_type, drill_method_other_desc, work_fee_rate, work_fee_unit, work_site_max,
     work_site_extra_rate, status_code, add_by, add_ts)
VALUES
    (@AppId, @WorkId, @WorkCategory, @WorkType, @WellUseType, @DrillerName, @DrillerLicenseNum,
     @DrillMethodType, @DrillMethodOtherDesc, @WorkFeeRate, @WorkFeeUnit, @WorkSiteMax,
     @WorkSiteExtraRate, @StatusCode, @AddBy, @AddTs);";

            await connection.ExecuteAsync(new CommandDefinition(workSql, new
            {
                work.AppId,
                work.WorkId,
                work.WorkCategory,
                work.WorkType,
                work.WellUseType,
                work.DrillerName,
                work.DrillerLicenseNum,
                work.DrillMethodType,
                work.DrillMethodOtherDesc,
                work.WorkFeeRate,
                work.WorkFeeUnit,
                work.WorkSiteMax,
                work.WorkSiteExtraRate,
                work.StatusCode,
                AddBy = application.AddBy,
                AddTs = application.AddDate
            }, transaction, cancellationToken: cancellationToken));

            foreach (var spec in work.Specs)
            {
                var specSql = $@"
INSERT INTO [{_schema}].[APP_WORK_SPECS]
    (application_id, work_id, work_specs_id, owner_well_num, drill_count, hole_diam_in, casing_diam_in,
     seal_depth_ft, max_depth_ft, latitude, longitude, state_well_id, dwr_num, permit_num, status_code, add_by, add_ts)
VALUES
    (@AppId, @WorkId, @WorkSpecsId, @OwnerWellNum, @DrillCount, @HoleDiamIn, @CasingDiamIn,
     @SealDepthFt, @MaxDepthFt, @Latitude, @Longitude, @StateWellId, @DwrNum, @PermitNum, @StatusCode, @AddBy, @AddTs);";

                await connection.ExecuteAsync(new CommandDefinition(specSql, new
                {
                    spec.AppId,
                    spec.WorkId,
                    spec.WorkSpecsId,
                    spec.OwnerWellNum,
                    spec.DrillCount,
                    spec.HoleDiamIn,
                    spec.CasingDiamIn,
                    spec.SealDepthFt,
                    spec.MaxDepthFt,
                    spec.Latitude,
                    spec.Longitude,
                    spec.StateWellId,
                    spec.DwrNum,
                    spec.PermitNum,
                    spec.StatusCode,
                    AddBy = application.AddBy,
                    AddTs = application.AddDate
                }, transaction, cancellationToken: cancellationToken));
            }
        }

        await InsertHazardAsync(connection, transaction, application, cancellationToken);
        await InsertDocumentsAsync(connection, transaction, application, cancellationToken);
        await InsertEmailCcsAsync(connection, transaction, application, cancellationToken);
        await InsertNotesAsync(connection, transaction, application, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Inserted application {AppId} with {WorkCount} work records", application.AppId, application.Works.Count);
        return application;
    }

    private async Task InsertHazardAsync(Microsoft.Data.SqlClient.SqlConnection connection, System.Data.Common.DbTransaction transaction, DomainApplication application, CancellationToken cancellationToken)
    {
        var hazard = application.Hazard;
        if (hazard is null)
        {
            return;
        }

        var hazardSql = $@"
INSERT INTO [{_schema}].[APP_HAZARD_INFO]
    (APPLICATION_ID, ONSITE_CONSULTANT_LAST_NAME, ONSITE_CONSULTANT_FIRST_NAME, ONSITE_CONSULTANT_PHONE_NUM,
     ONSITE_CONSULTANT_CELL_NUM, SAFETY_OFFICER_LAST_NAME, SAFETY_OFFICER_FIRST_NAME, SAFETY_OFFICER_PHONE_NUM,
     SAFETY_OFFICER_CELL_NUM, FACILITY_TYPE, SITE_SAFETY_MEETING_DATETIME,
     PERSONAL_PROTECTION_EQUIP_LEVEL_A, PERSONAL_PROTECTION_EQUIP_LEVEL_B, PERSONAL_PROTECTION_EQUIP_LEVEL_C,
     PERSONAL_PROTECTION_EQUIP_LEVEL_D, EQUIP_HARD_HAT_FLAG, EQUIP_SAFETY_SHOES_FLAG, EQUIP_ORANGE_VEST_FLAG,
     EQUIP_HEARING_PROT_FLAG, EQUIP_SAFETY_EYEWEAR_FLAG, EQUIP_CLOTHING_FLAG, EQUIP_CLOTHING_DESC,
     EQUIP_RESPIRATOR_FLAG, EQUIP_RESPIRATOR_DESC, EQUIP_CARTRIDGE_FLAG, EQUIP_CARTRIDGE_DESC,
     EQUIP_GLOVES_FLAG, EQUIP_GLOVES_DESC, EQUIP_OTHER_FLAG, EQUIP_OTHER_DESC,
     INFO_PROVIDED_BY_COMPANY_NAME, INFO_PROVIDED_BY_LAST_NAME, INFO_PROVIDED_BY_FIRST_NAME,
     INFO_PROVIDED_BY_TITLE, INFO_PROVIDED_BY_PHONE_NUM, ACKNOWLEDGEMENT, ADD_TS, ADD_BY)
VALUES
    (@AppId, @ConsultantLastName, @ConsultantFirstName, @ConsultantPhone,
     @ConsultantCell, @SafetyOfficerLastName, @SafetyOfficerFirstName, @SafetyOfficerPhone,
     @SafetyOfficerCell, @FacilityType, @SiteSafetyMeetingDateTime,
     @PpeLevelA, @PpeLevelB, @PpeLevelC,
     @PpeLevelD, @EquipHardHatFlag, @EquipSafetyShoesFlag, @EquipOrangeVestFlag,
     @EquipHearingProtFlag, @EquipSafetyEyewearFlag, @EquipClothingFlag, @EquipClothingDesc,
     @EquipRespiratorFlag, @EquipRespiratorDesc, @EquipCartridgeFlag, @EquipCartridgeDesc,
     @EquipGlovesFlag, @EquipGlovesDesc, @EquipOtherFlag, @EquipOtherDesc,
     @InfoProvidedByCompanyName, @InfoProvidedByLastName, @InfoProvidedByFirstName,
     @InfoProvidedByTitle, @InfoProvidedByPhone, @Acknowledgement, @AddTs, @AddBy);";

        await connection.ExecuteAsync(new CommandDefinition(hazardSql, new
        {
            application.AppId,
            hazard.ConsultantLastName,
            hazard.ConsultantFirstName,
            hazard.ConsultantPhone,
            hazard.ConsultantCell,
            hazard.SafetyOfficerLastName,
            hazard.SafetyOfficerFirstName,
            hazard.SafetyOfficerPhone,
            hazard.SafetyOfficerCell,
            hazard.FacilityType,
            hazard.SiteSafetyMeetingDateTime,
            hazard.PpeLevelA,
            hazard.PpeLevelB,
            hazard.PpeLevelC,
            hazard.PpeLevelD,
            hazard.EquipHardHatFlag,
            hazard.EquipSafetyShoesFlag,
            hazard.EquipOrangeVestFlag,
            hazard.EquipHearingProtFlag,
            hazard.EquipSafetyEyewearFlag,
            hazard.EquipClothingFlag,
            hazard.EquipClothingDesc,
            hazard.EquipRespiratorFlag,
            hazard.EquipRespiratorDesc,
            hazard.EquipCartridgeFlag,
            hazard.EquipCartridgeDesc,
            hazard.EquipGlovesFlag,
            hazard.EquipGlovesDesc,
            hazard.EquipOtherFlag,
            hazard.EquipOtherDesc,
            hazard.InfoProvidedByCompanyName,
            hazard.InfoProvidedByLastName,
            hazard.InfoProvidedByFirstName,
            hazard.InfoProvidedByTitle,
            hazard.InfoProvidedByPhone,
            hazard.Acknowledgement,
            AddTs = application.AddDate,
            AddBy = application.AddBy
        }, transaction, cancellationToken: cancellationToken));

        foreach (var contamination in hazard.Contaminations)
        {
            var contamSql = $@"
INSERT INTO [{_schema}].[APP_SITE_CONTAMINATIONS] (APPLICATION_ID, CONTAMINATION)
VALUES (@AppId, @Contamination);";
            await connection.ExecuteAsync(new CommandDefinition(contamSql, new { application.AppId, Contamination = contamination }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var substance in hazard.Substances)
        {
            var substanceSql = $@"
INSERT INTO [{_schema}].[APP_HAZARD_SUBSTANCE] (APPLICATION_ID, HAZARD_SEQ, CONCENTRATIONS_PPM, PEL_PPM, HEALTH_EFFECTS)
VALUES (@AppId, @HazardSeq, @ConcentrationsPpm, @PelPpm, @HealthEffects);";
            await connection.ExecuteAsync(new CommandDefinition(substanceSql, new
            {
                application.AppId,
                substance.HazardSeq,
                substance.ConcentrationsPpm,
                substance.PelPpm,
                substance.HealthEffects
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    private async Task InsertDocumentsAsync(Microsoft.Data.SqlClient.SqlConnection connection, System.Data.Common.DbTransaction transaction, DomainApplication application, CancellationToken cancellationToken)
    {
        foreach (var document in application.Documents)
        {
            var sql = $@"
INSERT INTO [{_schema}].[APP_DOCUMENT_LINKS]
    (APPLICATION_ID, SEQ_NUM, DOCUMENT_TYPE, OTHER_TYPE_DESC, DOCUMENT_FILENAME, ADD_BY, ADD_TS)
VALUES
    (@AppId, @SeqNum, @DocumentType, @OtherTypeDesc, @DocumentFilename, @AddBy, @AddTs);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                application.AppId,
                document.SeqNum,
                document.DocumentType,
                document.OtherTypeDesc,
                document.DocumentFilename,
                AddBy = application.AddBy,
                AddTs = application.AddDate
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    private async Task InsertEmailCcsAsync(Microsoft.Data.SqlClient.SqlConnection connection, System.Data.Common.DbTransaction transaction, DomainApplication application, CancellationToken cancellationToken)
    {
        foreach (var emailCc in application.EmailCcs)
        {
            var sql = $@"
INSERT INTO [{_schema}].[APP_EMAIL_CC] (application_id, email_cc_id, email_addr_cc)
VALUES (@AppId, @EmailCcId, @EmailAddrCc);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                application.AppId,
                emailCc.EmailCcId,
                emailCc.EmailAddrCc
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    private async Task InsertNotesAsync(Microsoft.Data.SqlClient.SqlConnection connection, System.Data.Common.DbTransaction transaction, DomainApplication application, CancellationToken cancellationToken)
    {
        foreach (var note in application.Notes)
        {
            var sql = $@"
INSERT INTO [{_schema}].[APP_NOTES] (application_id, note_id, add_by, add_ts, notes_text)
VALUES (@AppId, @NoteId, @AddBy, @AddTs, @NotesText);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                application.AppId,
                note.NoteId,
                note.AddBy,
                note.AddTs,
                note.NotesText
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    // Shared WHERE-clause builder so the page query (SearchAsync) and the matching-row count
    // (CountAsync) always apply identical filters. The caller supplies a StringBuilder already
    // seeded with "... WHERE 1 = 1".
    private void AppendSearchFilters(StringBuilder sql, DynamicParameters parameters, ApplicationSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.AppId))
        {
            sql.Append(" AND application_id = @AppId");
            parameters.Add("AppId", request.AppId);
        }

        if (!string.IsNullOrWhiteSpace(request.ApplicantLastName))
        {
            sql.Append(" AND app_last_name LIKE @ApplicantLastName");
            parameters.Add("ApplicantLastName", $"%{request.ApplicantLastName}%");
        }

        if (!string.IsNullOrWhiteSpace(request.AppFirstName))
        {
            sql.Append(" AND app_first_name LIKE @AppFirstName");
            parameters.Add("AppFirstName", $"%{request.AppFirstName}%");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            sql.Append(" AND app_email_addr = @Email");
            parameters.Add("Email", request.Email);
        }

        if (!string.IsNullOrWhiteSpace(request.AppBusinessName))
        {
            sql.Append(" AND app_business_name LIKE @AppBusinessName");
            parameters.Add("AppBusinessName", $"%{request.AppBusinessName}%");
        }

        if (!string.IsNullOrWhiteSpace(request.DrillerName))
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORKS] w WHERE w.application_id = [{_schema}].[APPLICATION_INFO].application_id AND w.driller_name LIKE @DrillerName)");
            parameters.Add("DrillerName", $"%{request.DrillerName}%");
        }

        if (!string.IsNullOrWhiteSpace(request.StatusCode))
        {
            sql.Append(" AND status_code = @StatusCode");
            parameters.Add("StatusCode", request.StatusCode);
        }

        if (!string.IsNullOrWhiteSpace(request.SiteCityName))
        {
            sql.Append(" AND project_site_city_code = @SiteCityCode");
            parameters.Add("SiteCityCode", request.SiteCityName);
        }

        if (request.AddedAfter.HasValue)
        {
            sql.Append(" AND add_ts >= @AddedAfter");
            parameters.Add("AddedAfter", request.AddedAfter.Value);
        }

        if (request.AddedBefore.HasValue)
        {
            sql.Append(" AND add_ts <= @AddedBefore");
            parameters.Add("AddedBefore", request.AddedBefore.Value);
        }

        // ---- Legacy search_form.jsp parity filters (all optional / additive) ----

        if (!string.IsNullOrWhiteSpace(request.ProjectLocation))
        {
            sql.Append(" AND project_site_location LIKE @ProjectLocation");
            parameters.Add("ProjectLocation", $"%{request.ProjectLocation}%");
        }

        if (request.AppAddedOn.HasValue)
        {
            sql.Append(" AND add_ts >= @AppAddedOn AND add_ts < DATEADD(day, 1, @AppAddedOn)");
            parameters.Add("AppAddedOn", request.AppAddedOn.Value.Date);
        }

        if (request.AppYear.HasValue)
        {
            sql.Append(" AND YEAR(add_ts) = @AppYear");
            parameters.Add("AppYear", request.AppYear.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.PermitNum))
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORK_PERMITS] p WHERE p.application_id = [{_schema}].[APPLICATION_INFO].application_id AND p.permit_number LIKE @PermitNum)");
            parameters.Add("PermitNum", $"%{request.PermitNum}%");
        }

        if (request.PermitYear.HasValue)
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORK_PERMITS] p WHERE p.application_id = [{_schema}].[APPLICATION_INFO].application_id AND YEAR(p.permit_issued_date) = @PermitYear)");
            parameters.Add("PermitYear", request.PermitYear.Value);
        }

        if (request.PermitIssuedFrom.HasValue || request.PermitIssuedTo.HasValue)
        {
            var conditions = new List<string>();
            if (request.PermitIssuedFrom.HasValue)
            {
                conditions.Add("p.permit_issued_date >= @PermitIssuedFrom");
                parameters.Add("PermitIssuedFrom", request.PermitIssuedFrom.Value.Date);
            }

            if (request.PermitIssuedTo.HasValue)
            {
                conditions.Add("p.permit_issued_date < DATEADD(day, 1, @PermitIssuedTo)");
                parameters.Add("PermitIssuedTo", request.PermitIssuedTo.Value.Date);
            }

            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORK_PERMITS] p WHERE p.application_id = [{_schema}].[APPLICATION_INFO].application_id AND {string.Join(" AND ", conditions)})");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkCategory))
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORKS] w WHERE w.application_id = [{_schema}].[APPLICATION_INFO].application_id AND w.work_category = @WorkCategory)");
            parameters.Add("WorkCategory", request.WorkCategory);
        }

        if (!string.IsNullOrWhiteSpace(request.WorkType))
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORKS] w WHERE w.application_id = [{_schema}].[APPLICATION_INFO].application_id AND w.work_type = @WorkType)");
            parameters.Add("WorkType", request.WorkType);
        }

        if (!string.IsNullOrWhiteSpace(request.DrillerLicense))
        {
            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_WORKS] w WHERE w.application_id = [{_schema}].[APPLICATION_INFO].application_id AND w.driller_license_num LIKE @DrillerLicense)");
            parameters.Add("DrillerLicense", $"%{request.DrillerLicense}%");
        }

        if (request.ApprovedFrom.HasValue || request.ApprovedTo.HasValue)
        {
            var conditions = new List<string> { $"pay.application_id = [{_schema}].[APPLICATION_INFO].application_id" };
            if (request.ApprovedFrom.HasValue)
            {
                conditions.Add("pay.update_ts >= @ApprovedFrom");
                parameters.Add("ApprovedFrom", request.ApprovedFrom.Value.Date);
            }

            if (request.ApprovedTo.HasValue)
            {
                conditions.Add("pay.update_ts < DATEADD(day, 1, @ApprovedTo)");
                parameters.Add("ApprovedTo", request.ApprovedTo.Value.Date);
            }

            sql.Append($" AND EXISTS (SELECT 1 FROM [{_schema}].[APP_PAYMENT_INFO] pay WHERE {string.Join(" AND ", conditions)})");
        }
    }

    // Whitelist of sortable grid columns → the SQL expression to ORDER BY. Only these keys are ever
    // interpolated into the query, so an arbitrary SortBy value can never reach the SQL text.
    private static readonly IReadOnlyDictionary<string, string> SortableColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["appId"] = "application_id",
        ["addDate"] = "add_ts",
        ["statusCode"] = "status_code",
        ["siteCity"] = "project_site_city_code",
        ["applicantName"] = "COALESCE(NULLIF(app_business_name, ''), app_last_name, app_first_name)",
    };

    // Builds a safe ORDER BY body from the whitelisted SortBy key and a validated direction, always
    // ending with a unique tiebreaker (application_id) so OFFSET/FETCH paging stays deterministic
    // when the primary sort value ties. Unknown/blank keys fall back to the legacy newest-first order.
    private string BuildOrderBy(ApplicationSearchRequest request)
    {
        var direction = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        string column;
        if (string.Equals(request.SortBy, "drillerName", StringComparison.OrdinalIgnoreCase))
        {
            column = $"(SELECT MIN(w.driller_name) FROM [{_schema}].[APP_WORKS] w WHERE w.application_id = [{_schema}].[APPLICATION_INFO].application_id)";
        }
        else if (!string.IsNullOrWhiteSpace(request.SortBy) && SortableColumns.TryGetValue(request.SortBy, out var mapped))
        {
            column = mapped;
        }
        else
        {
            return "add_ts DESC, application_id DESC";
        }

        return SqlOrderBy.WithTiebreaker(column, direction, "application_id");
    }

    // Total number of applications matching the search filters, ignoring paging. Drives the
    // server-side pagination controls (total pages / "N applications found").
    public async Task<int> CountAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var sql = new StringBuilder($"SELECT COUNT(*) FROM [{_schema}].[APPLICATION_INFO]\nWHERE 1 = 1");
        AppendSearchFilters(sql, parameters, request);
        sql.Append(';');

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DomainApplication>> SearchAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var sql = new StringBuilder($"{SelectColumns}\nWHERE 1 = 1");
        AppendSearchFilters(sql, parameters, request);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        sql.Append($" ORDER BY {BuildOrderBy(request)} OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ApplicationRow>(new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken))).ToList();

        // Populate the primary driller name per application so the tracking/search list can display it
        // (matches the legacy Track results "Driller Name" column, which shows the driller from APP_WORKS).
        var appIds = rows.Select(r => r.AppId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var drillersByApp = new Dictionary<string, string?>();
        if (appIds.Count > 0)
        {
            var drillerRows = await connection.QueryAsync<DrillerLookupRow>(new CommandDefinition($@"
SELECT application_id AS AppId, driller_name AS DrillerName
FROM [{_schema}].[APP_WORKS] w
WHERE application_id IN @AppIds
  AND work_id = (SELECT MIN(work_id) FROM [{_schema}].[APP_WORKS] i WHERE i.application_id = w.application_id);",
                new { AppIds = appIds }, cancellationToken: cancellationToken));
            foreach (var d in drillerRows)
            {
                if (d.AppId != null)
                {
                    drillersByApp[d.AppId] = d.DrillerName;
                }
            }
        }

        return rows.Select(row =>
        {
            var works = new List<ApplicationWork>();
            if (row.AppId != null && drillersByApp.TryGetValue(row.AppId, out var driller) && !string.IsNullOrWhiteSpace(driller))
            {
                works.Add(new ApplicationWork { AppId = row.AppId, DrillerName = driller });
            }
            return row.ToDomain(works);
        }).ToList();
    }

    public async Task UpdateStatusAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET status_code = @StatusCode,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { AppId = appId, StatusCode = statusCode, UpdatedBy = updatedBy, UpdatedTs = DateTime.UtcNow }, cancellationToken: cancellationToken));
    }

    public async Task UpdateApprovalAsync(string appId, string statusCode, string approvedBy, CancellationToken cancellationToken = default)
    {
        // Mirrors legacy BeanApp.updateApprove: records the approver and approval date alongside the
        // status change. Uses GETDATE() (SQL Server local time) like the legacy app so approved_date
        // lines up with the timestamps the intra reports read.
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET status_code = @StatusCode,
    approved_by = @ApprovedBy,
    approved_date = GETDATE(),
    update_by = @ApprovedBy,
    update_ts = GETDATE()
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { AppId = appId, StatusCode = statusCode, ApprovedBy = approvedBy }, cancellationToken: cancellationToken));
    }

    public async Task UpdateWorkStatusesAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[APP_WORKS]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;

UPDATE [{_schema}].[APP_WORK_SPECS]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { AppId = appId, StatusCode = statusCode, UpdatedBy = updatedBy, UpdatedTs = DateTime.UtcNow }, cancellationToken: cancellationToken));
    }

    public async Task CancelApplicationAsync(string appId, string cancelledBy, CancellationToken cancellationToken = default)
    {
        // Mirrors legacy ApplicationBean.cancelApplication: flip the payment, works (and specs) and the
        // application itself to CAN in a single transaction so a partial cancel can never be left behind.
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updatedTs = DateTime.UtcNow;
        var args = new { AppId = appId, StatusCode = "CAN", UpdatedBy = cancelledBy, UpdatedTs = updatedTs };

        await connection.ExecuteAsync(new CommandDefinition($@"
UPDATE [{_schema}].[APP_PAYMENT_INFO]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;", args, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition($@"
UPDATE [{_schema}].[APP_WORKS]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;

UPDATE [{_schema}].[APP_WORK_SPECS]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;", args, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition($@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET status_code = @StatusCode, update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId;", args, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Cancelled application {AppId} by {CancelledBy}", appId, cancelledBy);
    }

    // Global per-well extra rate for tiered "site" work types, held in the special WORK_TYPES row
    // (work_category='system', work_type='siteExtra'). Legacy parity: BeanCodesWorkType.getWorkTypeSiteExtra.
    private string SiteExtraRateSql => $@"
SELECT CAST(ISNULL(fee_rate_amt, 0) AS decimal(19,2))
FROM [{_schema}].[WORK_TYPES]
WHERE work_category = 'system' AND work_type = 'siteExtra';";

    public async Task<decimal> GetSiteExtraRateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(SiteExtraRateSql, cancellationToken: cancellationToken)) ?? 0m;
    }

    public async Task<PermitInfoDto?> GetPermitInfoAsync(string appId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var header = await connection.QuerySingleOrDefaultAsync<PermitHeaderRow>(new CommandDefinition($@"
SELECT application_id AS AppId, approved_by AS ApprovedBy, approved_date AS ApprovedDate
FROM [{_schema}].[APPLICATION_INFO]
WHERE application_id = @AppId;", new { AppId = appId }, cancellationToken: cancellationToken));

        if (header is null)
        {
            return null;
        }

        var permits = (await connection.QueryAsync<PermitLineRow>(new CommandDefinition($@"
SELECT permit_number AS PermitNumber, work_id AS WorkId, work_specs_id AS WorkSpecsId,
       permit_issued_date AS IssuedDate, permit_expire_date AS ExpireDate, STATUS_CODE AS StatusCode
FROM [{_schema}].[APP_WORK_PERMITS]
WHERE application_id = @AppId
ORDER BY work_id, work_specs_id, permit_number;", new { AppId = appId }, cancellationToken: cancellationToken)))
            .Select(p => new PermitLineDto(p.PermitNumber ?? string.Empty, p.WorkId, p.WorkSpecsId, p.IssuedDate, p.ExpireDate, p.StatusCode ?? string.Empty))
            .ToList();

        return new PermitInfoDto(header.AppId ?? appId, header.ApprovedBy, header.ApprovedDate, permits);
    }

    private sealed class PermitHeaderRow
    {
        public string? AppId { get; init; }
        public string? ApprovedBy { get; init; }
        public DateTime? ApprovedDate { get; init; }
    }

    private sealed class PermitLineRow
    {
        public string? PermitNumber { get; init; }
        public int WorkId { get; init; }
        public int? WorkSpecsId { get; init; }
        public DateTime? IssuedDate { get; init; }
        public DateTime? ExpireDate { get; init; }
        public string? StatusCode { get; init; }
    }

    public async Task UpdateSitemapAsync(string appId, string sitemapFilename, string updatedBy, CancellationToken cancellationToken = default)
    {
        // Recording a sitemap advances the application out of "Pending Sitemap" (PENDS) into
        // "Pending Approval" (PEND), mirroring the legacy ProcessFileUploadServlet which sets
        // status_code = 'PEND' on updateSitemap. Guarded so a re-upload never regresses an
        // application that has already moved past PENDS (e.g. PEND/PENDC/PENDP/APPRV).
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET sitemap_filename = @SitemapFilename,
    sitemap_received_date = @ReceivedTs,
    status_code = CASE WHEN status_code = 'PENDS' THEN 'PEND' ELSE status_code END,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppId = appId,
            SitemapFilename = sitemapFilename,
            ReceivedTs = DateTime.UtcNow,
            UpdatedBy = updatedBy,
            UpdatedTs = DateTime.UtcNow
        }, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} was not found for the sitemap update.");
        }

        _logger.LogInformation("Recorded sitemap {FileName} for application {AppId}", sitemapFilename, appId);
    }

    public async Task UpdateApprovalDetailsAsync(string appId, UpdateApprovalDetailsRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        // The Approval Wizard owns both values and sends the complete picture on every change, so both
        // columns are set authoritatively (a null clears — e.g. when the staff unchecks "sitemap received").
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET site_visit_type = @SiteVisitType,
    sitemap_received_date = @SitemapReceivedDate,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppId = appId,
            SiteVisitType = NormalizeSiteVisitType(request.SiteVisitType),
            request.SitemapReceivedDate,
            UpdatedBy = updatedBy,
            UpdatedTs = DateTime.UtcNow
        }, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} was not found for the approval-details update.");
        }

        _logger.LogInformation("Updated approval details (visit type / sitemap date) for application {AppId}", appId);
    }

    // Maps any site-visit-type token to the exact strings the legacy Java intra stores and compares
    // against (Constants.siteVisitInspection = "Inspection", Constants.siteVisitReview =
    // "Field Technician Review"). The legacy search_detail.jsp pre-selects its radios by an exact,
    // case-insensitive match on these strings, so storing a raw token like "INSPECT"/"REVIEW" would
    // leave the option unselected (while still showing the "has a value" checkmark). Unknown non-empty
    // values pass through trimmed so we never silently drop data.
    private static string? NormalizeSiteVisitType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "INSPECT" or "INSPECTION" or "I" => "Inspection",
            "REVIEW" or "FIELD TECHNICIAN REVIEW" or "F" => "Field Technician Review",
            _ => trimmed
        };
    }

    public async Task UpdateProjectInfoAsync(string appId, UpdateProjectInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET project_site_city_code = @SiteCityCode,
    project_site_location = @SiteLocation,
    project_site_lat = @SiteLat,
    project_site_long = @SiteLong,
    project_start_date = @ProjStartDate,
    project_end_date = @ProjEndDate,
    owner_first_name = @OwnerFirstName,
    owner_last_name = @OwnerLastName,
    owner_addr_street = @OwnerAddrStreet,
    owner_addr_city_name = @OwnerAddrCity,
    owner_addr_state_code = @OwnerAddrState,
    owner_addr_zip = @OwnerAddrZip,
    owner_phone_num = @OwnerPhone,
    owner_email_addr = @OwnerEmail,
    client_first_name = @ClientFirstName,
    client_last_name = @ClientLastName,
    client_addr_street = @ClientAddrStreet,
    client_addr_city_name = @ClientAddrCity,
    client_addr_state_code = @ClientAddrState,
    client_addr_zip = @ClientAddrZip,
    client_phone_num = @ClientPhone,
    client_email_addr = @ClientEmail,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        var parameters = new DynamicParameters(request);
        parameters.Add("AppId", appId);
        parameters.Add("UpdatedBy", updatedBy);
        parameters.Add("UpdatedTs", DateTime.UtcNow);

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} was not found for the project-info update.");
        }

        _logger.LogInformation("Updated project info for application {AppId}", appId);
    }

    public async Task UpdateApplicantInfoAsync(string appId, UpdateApplicantInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET app_business_name = @AppBusinessName,
    app_first_name = @AppFirstName,
    app_last_name = @AppLastName,
    app_email_addr = @AppEmailAddr,
    app_addr_street = @AppAddrStreet,
    app_addr_street2 = @AppAddrStreet2,
    app_addr_city_name = @AppAddrCity,
    app_addr_state_code = @AppAddrState,
    app_addr_zip = @AppAddrZip,
    app_phone_num = @AppPhone,
    app_phone_fax = @AppFax,
    contact_first_name = @ContactFirstName,
    contact_last_name = @ContactLastName,
    contact_email_addr = @ContactEmail,
    contact_phone_num = @ContactPhone,
    contact_phone_cell = @ContactCell,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        var parameters = new DynamicParameters();
        parameters.Add("AppBusinessName", request.AppBusinessName);
        parameters.Add("AppFirstName", request.AppFirstName);
        parameters.Add("AppLastName", request.AppLastName);
        parameters.Add("AppEmailAddr", request.AppEmailAddr);
        parameters.Add("AppAddrStreet", request.AppAddrStreet);
        parameters.Add("AppAddrStreet2", request.AppAddrStreet2);
        parameters.Add("AppAddrCity", request.AppAddrCity);
        parameters.Add("AppAddrState", request.AppAddrState);
        parameters.Add("AppAddrZip", request.AppAddrZip);
        parameters.Add("AppPhone", request.AppPhone);
        parameters.Add("AppFax", request.AppFax);
        parameters.Add("ContactFirstName", request.ContactFirstName);
        parameters.Add("ContactLastName", request.ContactLastName);
        parameters.Add("ContactEmail", request.ContactEmail);
        parameters.Add("ContactPhone", request.ContactPhone);
        parameters.Add("ContactCell", request.ContactCell);
        parameters.Add("AppId", appId);
        parameters.Add("UpdatedBy", updatedBy);
        parameters.Add("UpdatedTs", DateTime.UtcNow);

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} was not found for the applicant-info update.");
        }

        // Replace the "Other Emails" CC recipients wholesale (mirrors the legacy save that rebuilds
        // APP_EMAIL_CC from the posted rows).
        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[APP_EMAIL_CC] WHERE application_id = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        var ccId = 1;
        foreach (var email in request.EmailCcs.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"INSERT INTO [{_schema}].[APP_EMAIL_CC] (application_id, email_cc_id, email_addr_cc) VALUES (@AppId, @EmailCcId, @EmailAddrCc);",
                new { AppId = appId, EmailCcId = ccId++, EmailAddrCc = email.Trim() }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Updated applicant info for application {AppId}", appId);
    }

    public async Task UpdateHazardAsync(string appId, UpdateHazardRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[APP_HAZARD_INFO]
SET ONSITE_CONSULTANT_FIRST_NAME = @ConsultantFirstName,
    ONSITE_CONSULTANT_LAST_NAME = @ConsultantLastName,
    ONSITE_CONSULTANT_PHONE_NUM = @ConsultantPhone,
    ONSITE_CONSULTANT_CELL_NUM = @ConsultantCell,
    SAFETY_OFFICER_FIRST_NAME = @SafetyOfficerFirstName,
    SAFETY_OFFICER_LAST_NAME = @SafetyOfficerLastName,
    SAFETY_OFFICER_PHONE_NUM = @SafetyOfficerPhone,
    SAFETY_OFFICER_CELL_NUM = @SafetyOfficerCell,
    FACILITY_TYPE = @FacilityType,
    SITE_SAFETY_MEETING_DATETIME = @SiteSafetyMeetingDateTime,
    PERSONAL_PROTECTION_EQUIP_LEVEL_A = @PpeLevelA,
    PERSONAL_PROTECTION_EQUIP_LEVEL_B = @PpeLevelB,
    PERSONAL_PROTECTION_EQUIP_LEVEL_C = @PpeLevelC,
    PERSONAL_PROTECTION_EQUIP_LEVEL_D = @PpeLevelD,
    EQUIP_HARD_HAT_FLAG = @EquipHardHatFlag,
    EQUIP_SAFETY_SHOES_FLAG = @EquipSafetyShoesFlag,
    EQUIP_ORANGE_VEST_FLAG = @EquipOrangeVestFlag,
    EQUIP_HEARING_PROT_FLAG = @EquipHearingProtFlag,
    EQUIP_SAFETY_EYEWEAR_FLAG = @EquipSafetyEyewearFlag,
    EQUIP_CLOTHING_FLAG = @EquipClothingFlag,
    EQUIP_CLOTHING_DESC = @EquipClothingDesc,
    EQUIP_RESPIRATOR_FLAG = @EquipRespiratorFlag,
    EQUIP_RESPIRATOR_DESC = @EquipRespiratorDesc,
    EQUIP_CARTRIDGE_FLAG = @EquipCartridgeFlag,
    EQUIP_CARTRIDGE_DESC = @EquipCartridgeDesc,
    EQUIP_GLOVES_FLAG = @EquipGlovesFlag,
    EQUIP_GLOVES_DESC = @EquipGlovesDesc,
    EQUIP_OTHER_FLAG = @EquipOtherFlag,
    EQUIP_OTHER_DESC = @EquipOtherDesc,
    INFO_PROVIDED_BY_FIRST_NAME = @InfoProvidedByFirstName,
    INFO_PROVIDED_BY_LAST_NAME = @InfoProvidedByLastName,
    INFO_PROVIDED_BY_TITLE = @InfoProvidedByTitle,
    INFO_PROVIDED_BY_PHONE_NUM = @InfoProvidedByPhone,
    UPDATE_BY = @UpdatedBy,
    UPDATE_TS = @UpdatedTs
WHERE APPLICATION_ID = @AppId;";

        var parameters = new DynamicParameters();
        parameters.Add("AppId", appId);
        parameters.Add("ConsultantFirstName", request.ConsultantFirstName);
        parameters.Add("ConsultantLastName", request.ConsultantLastName);
        parameters.Add("ConsultantPhone", request.ConsultantPhone);
        parameters.Add("ConsultantCell", request.ConsultantCell);
        parameters.Add("SafetyOfficerFirstName", request.SafetyOfficerFirstName);
        parameters.Add("SafetyOfficerLastName", request.SafetyOfficerLastName);
        parameters.Add("SafetyOfficerPhone", request.SafetyOfficerPhone);
        parameters.Add("SafetyOfficerCell", request.SafetyOfficerCell);
        parameters.Add("FacilityType", request.FacilityType);
        parameters.Add("SiteSafetyMeetingDateTime", request.SiteSafetyMeetingDateTime);
        parameters.Add("PpeLevelA", request.PpeLevelA);
        parameters.Add("PpeLevelB", request.PpeLevelB);
        parameters.Add("PpeLevelC", request.PpeLevelC);
        parameters.Add("PpeLevelD", request.PpeLevelD);
        parameters.Add("EquipHardHatFlag", request.EquipHardHatFlag);
        parameters.Add("EquipSafetyShoesFlag", request.EquipSafetyShoesFlag);
        parameters.Add("EquipOrangeVestFlag", request.EquipOrangeVestFlag);
        parameters.Add("EquipHearingProtFlag", request.EquipHearingProtFlag);
        parameters.Add("EquipSafetyEyewearFlag", request.EquipSafetyEyewearFlag);
        parameters.Add("EquipClothingFlag", request.EquipClothingFlag);
        parameters.Add("EquipClothingDesc", request.EquipClothingDesc);
        parameters.Add("EquipRespiratorFlag", request.EquipRespiratorFlag);
        parameters.Add("EquipRespiratorDesc", request.EquipRespiratorDesc);
        parameters.Add("EquipCartridgeFlag", request.EquipCartridgeFlag);
        parameters.Add("EquipCartridgeDesc", request.EquipCartridgeDesc);
        parameters.Add("EquipGlovesFlag", request.EquipGlovesFlag);
        parameters.Add("EquipGlovesDesc", request.EquipGlovesDesc);
        parameters.Add("EquipOtherFlag", request.EquipOtherFlag);
        parameters.Add("EquipOtherDesc", request.EquipOtherDesc);
        parameters.Add("InfoProvidedByFirstName", request.InfoProvidedByFirstName);
        parameters.Add("InfoProvidedByLastName", request.InfoProvidedByLastName);
        parameters.Add("InfoProvidedByTitle", request.InfoProvidedByTitle);
        parameters.Add("InfoProvidedByPhone", request.InfoProvidedByPhone);
        parameters.Add("UpdatedBy", updatedBy);
        parameters.Add("UpdatedTs", DateTime.UtcNow);

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} has no site-hazard record to update.");
        }

        // Replace the child collections wholesale (mirrors the legacy save, which rebuilds both
        // APP_SITE_CONTAMINATIONS and APP_HAZARD_SUBSTANCE from the posted rows).
        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[APP_SITE_CONTAMINATIONS] WHERE APPLICATION_ID = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        foreach (var contamination in request.Contaminants.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"INSERT INTO [{_schema}].[APP_SITE_CONTAMINATIONS] (APPLICATION_ID, CONTAMINATION) VALUES (@AppId, @Contamination);",
                new { AppId = appId, Contamination = contamination.Trim() }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[APP_HAZARD_SUBSTANCE] WHERE APPLICATION_ID = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        var seq = 0;
        foreach (var substance in request.Substances)
        {
            if (string.IsNullOrWhiteSpace(substance.Concentration)
                && string.IsNullOrWhiteSpace(substance.PelPpm)
                && string.IsNullOrWhiteSpace(substance.HealthEffects))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                $@"INSERT INTO [{_schema}].[APP_HAZARD_SUBSTANCE] (APPLICATION_ID, HAZARD_SEQ, CONCENTRATIONS_PPM, PEL_PPM, HEALTH_EFFECTS)
VALUES (@AppId, @HazardSeq, @Concentration, @PelPpm, @HealthEffects);",
                new { AppId = appId, HazardSeq = seq++, substance.Concentration, substance.PelPpm, substance.HealthEffects },
                transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Updated site-hazard info for application {AppId}", appId);
    }

    public async Task UpdateWorkAsync(string appId, int workId, UpdateWorkRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var updatedTs = DateTime.UtcNow;

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Keep the site-extra rate in step with the (possibly edited) fee unit / site_max so the
        // recharge at approval stays correct. Non-tiered works clear the extra rate to 0.
        var updSiteExtraRate = string.Equals(request.WorkFeeUnit?.Trim(), "site", StringComparison.OrdinalIgnoreCase) && (request.WorkSiteMax ?? 0) > 0
            ? await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(SiteExtraRateSql, transaction: transaction, cancellationToken: cancellationToken)) ?? 0m
            : 0m;

        var workSql = $@"
UPDATE [{_schema}].[APP_WORKS]
SET well_use_type = @WellUseType,
    driller_name = @DrillerName,
    driller_license_num = @DrillerLicenseNum,
    drill_method_type = @DrillMethodType,
    drill_method_other_desc = @DrillMethodOtherDesc,
    work_fee_rate = @WorkFeeRate,
    work_fee_unit = @WorkFeeUnit,
    work_site_max = @WorkSiteMax,
    work_site_extra_rate = @WorkSiteExtraRate,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId;";

        var rows = await connection.ExecuteAsync(new CommandDefinition(workSql, new
        {
            AppId = appId,
            WorkId = workId,
            request.WellUseType,
            request.DrillerName,
            request.DrillerLicenseNum,
            request.DrillMethodType,
            request.DrillMethodOtherDesc,
            request.WorkFeeRate,
            request.WorkFeeUnit,
            request.WorkSiteMax,
            WorkSiteExtraRate = updSiteExtraRate,
            UpdatedBy = updatedBy,
            UpdatedTs = updatedTs
        }, transaction, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException($"Work {workId} was not found for application {appId}.");
        }

        var specSql = $@"
UPDATE [{_schema}].[APP_WORK_SPECS]
SET owner_well_num = @OwnerWellNum,
    drill_count = @DrillCount,
    hole_diam_in = @HoleDiamIn,
    casing_diam_in = @CasingDiamIn,
    seal_depth_ft = @SealDepthFt,
    max_depth_ft = @MaxDepthFt,
    latitude = @Latitude,
    longitude = @Longitude,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId AND work_specs_id = @WorkSpecsId;";

        var insertSpecSql = $@"
INSERT INTO [{_schema}].[APP_WORK_SPECS]
    (application_id, work_id, work_specs_id, owner_well_num, drill_count, hole_diam_in, casing_diam_in,
     seal_depth_ft, max_depth_ft, latitude, longitude, state_well_id, dwr_num, permit_num, status_code, add_by, add_ts)
VALUES
    (@AppId, @WorkId, @WorkSpecsId, @OwnerWellNum, @DrillCount, @HoleDiamIn, @CasingDiamIn,
     @SealDepthFt, @MaxDepthFt, @Latitude, @Longitude, NULL, NULL, NULL, 'PEND', @UpdatedBy, @UpdatedTs);";

        // Reconcile the well-spec rows: update existing rows, insert new ones (WorkSpecsId <= 0), and
        // delete any existing rows the caller dropped. Mirrors the legacy specsu/specsb/specsd handlers
        // (new ids via MAX+1; the last remaining spec cannot be deleted).
        if (request.Specs.Count == 0)
        {
            throw new InvalidOperationException("A work request must keep at least one well specification.");
        }

        var existingSpecIds = (await connection.QueryAsync<int>(new CommandDefinition(
            $"SELECT work_specs_id FROM [{_schema}].[APP_WORK_SPECS] WHERE application_id = @AppId AND work_id = @WorkId;",
            new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken))).ToList();

        var nextSpecId = existingSpecIds.Count == 0 ? 1 : existingSpecIds.Max() + 1;
        var keptSpecIds = new List<int>();

        foreach (var spec in request.Specs)
        {
            var isExisting = spec.WorkSpecsId > 0 && existingSpecIds.Contains(spec.WorkSpecsId);
            if (isExisting)
            {
                keptSpecIds.Add(spec.WorkSpecsId);
                await connection.ExecuteAsync(new CommandDefinition(specSql, new
                {
                    AppId = appId,
                    WorkId = workId,
                    spec.WorkSpecsId,
                    spec.OwnerWellNum,
                    DrillCount = spec.DrillCount ?? 1,
                    spec.HoleDiamIn,
                    spec.CasingDiamIn,
                    spec.SealDepthFt,
                    spec.MaxDepthFt,
                    spec.Latitude,
                    spec.Longitude,
                    UpdatedBy = updatedBy,
                    UpdatedTs = updatedTs
                }, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                var newId = nextSpecId++;
                keptSpecIds.Add(newId);
                await connection.ExecuteAsync(new CommandDefinition(insertSpecSql, new
                {
                    AppId = appId,
                    WorkId = workId,
                    WorkSpecsId = newId,
                    spec.OwnerWellNum,
                    DrillCount = spec.DrillCount ?? 1,
                    spec.HoleDiamIn,
                    spec.CasingDiamIn,
                    spec.SealDepthFt,
                    spec.MaxDepthFt,
                    spec.Latitude,
                    spec.Longitude,
                    UpdatedBy = updatedBy,
                    UpdatedTs = updatedTs
                }, transaction, cancellationToken: cancellationToken));
            }
        }

        foreach (var removedId in existingSpecIds.Where(id => !keptSpecIds.Contains(id)))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM [{_schema}].[APP_WORK_SPECS] WHERE application_id = @AppId AND work_id = @WorkId AND work_specs_id = @WorkSpecsId;",
                new { AppId = appId, WorkId = workId, WorkSpecsId = removedId }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Updated work {WorkId} ({SpecCount} specs) for application {AppId}", workId, request.Specs.Count, appId);
    }

    public async Task AddWorkAsync(string appId, AddWorkRequest request, string addedBy, CancellationToken cancellationToken = default)
    {
        var addedTs = DateTime.UtcNow;

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Seed the fee columns from the WORK_TYPES lookup so the new row matches what the applicant
        // would have been quoted for that category + type (legacy upd_work_type.jsp carries these
        // hidden fields through from the work-type dropdown). Mirror the work-type dropdown query
        // exactly (active rows only, take the first match) so the fee resolves to the same row the
        // reviewer picked — WORK_TYPES can hold duplicate/inactive rows for a category+type, which
        // would otherwise make a single-row lookup throw and fail the whole "Add Work".
        var feeSql = $@"
SELECT CAST(ISNULL(fee_rate_amt, 0) AS decimal(19,2)) AS FeeRate,
       LTRIM(RTRIM(ISNULL(fee_unit, ''))) AS FeeUnit,
       ISNULL(site_max, 0) AS SiteMax
FROM [{_schema}].[WORK_TYPES]
WHERE active_flag = 'Y' AND work_category = @WorkCategory AND work_type = @WorkType
ORDER BY work_desc;";

        var fee = await connection.QueryFirstOrDefaultAsync<WorkTypeFeeRow>(new CommandDefinition(feeSql, new
        {
            request.WorkCategory,
            request.WorkType
        }, transaction, cancellationToken: cancellationToken));

        if (fee is null)
        {
            throw new InvalidOperationException($"Work type {request.WorkCategory}/{request.WorkType} was not found.");
        }

        // Tiered "site" work types charge the global site-extra rate per well beyond site_max; other
        // types never carry an extra rate. Looked up server-side (never trusted from the client).
        var addSiteExtraRate = string.Equals(fee.FeeUnit, "site", StringComparison.OrdinalIgnoreCase) && fee.SiteMax > 0
            ? await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(SiteExtraRateSql, transaction: transaction, cancellationToken: cancellationToken)) ?? 0m
            : 0m;

        var nextIdSql = $@"
SELECT ISNULL(MAX(work_id), 0) + 1
FROM [{_schema}].[APP_WORKS]
WHERE application_id = @AppId;";

        var nextWorkId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(nextIdSql, new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        var insertSql = $@"
INSERT INTO [{_schema}].[APP_WORKS]
    (application_id, work_id, work_category, work_type, well_use_type, driller_name, driller_license_num,
     drill_method_type, drill_method_other_desc, work_fee_rate, work_fee_unit, work_site_max,
     work_site_extra_rate, status_code, add_by, add_ts)
VALUES
    (@AppId, @WorkId, @WorkCategory, @WorkType, @WellUseType, @DrillerName, @DrillerLicenseNum,
     @DrillMethodType, @DrillMethodOtherDesc, @WorkFeeRate, @WorkFeeUnit, @WorkSiteMax,
     @WorkSiteExtraRate, @StatusCode, @AddBy, @AddTs);";

        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            AppId = appId,
            WorkId = nextWorkId,
            request.WorkCategory,
            request.WorkType,
            request.WellUseType,
            request.DrillerName,
            request.DrillerLicenseNum,
            request.DrillMethodType,
            request.DrillMethodOtherDesc,
            WorkFeeRate = fee.FeeRate,
            WorkFeeUnit = fee.FeeUnit,
            WorkSiteMax = fee.SiteMax,
            WorkSiteExtraRate = addSiteExtraRate,
            StatusCode = "PENDC",
            AddBy = addedBy,
            AddTs = addedTs
        }, transaction, cancellationToken: cancellationToken));

        // Persist the wells captured on the Add Work form as APP_WORK_SPECS rows (ids assigned
        // sequentially from 1). New wells start at status PEND and carry a drill_count of at least 1
        // (the fee is fee_rate × Σ drill_count).
        var insertSpecSql = $@"
INSERT INTO [{_schema}].[APP_WORK_SPECS]
    (application_id, work_id, work_specs_id, owner_well_num, drill_count, hole_diam_in, casing_diam_in,
     seal_depth_ft, max_depth_ft, latitude, longitude, state_well_id, dwr_num, permit_num, status_code, add_by, add_ts)
VALUES
    (@AppId, @WorkId, @WorkSpecsId, @OwnerWellNum, @DrillCount, @HoleDiamIn, @CasingDiamIn,
     @SealDepthFt, @MaxDepthFt, @Latitude, @Longitude, NULL, NULL, NULL, 'PEND', @AddBy, @AddTs);";

        var specId = 1;
        foreach (var spec in request.Specs)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertSpecSql, new
            {
                AppId = appId,
                WorkId = nextWorkId,
                WorkSpecsId = specId++,
                spec.OwnerWellNum,
                DrillCount = spec.DrillCount ?? 1,
                spec.HoleDiamIn,
                spec.CasingDiamIn,
                spec.SealDepthFt,
                spec.MaxDepthFt,
                spec.Latitude,
                spec.Longitude,
                AddBy = addedBy,
                AddTs = addedTs
            }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Added work {WorkId} ({Category}/{Type}, {SpecCount} specs) to application {AppId}", nextWorkId, request.WorkCategory, request.WorkType, request.Specs.Count, appId);
    }

    public async Task<bool> CancelWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default)
    {
        var updatedTs = DateTime.UtcNow;

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var workSql = $@"
UPDATE [{_schema}].[APP_WORKS]
SET status_code = 'CAN', update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId;";

        var rows = await connection.ExecuteAsync(new CommandDefinition(workSql, new
        {
            AppId = appId,
            WorkId = workId,
            UpdatedBy = updatedBy,
            UpdatedTs = updatedTs
        }, transaction, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var specSql = $@"
UPDATE [{_schema}].[APP_WORK_SPECS]
SET status_code = 'CAN', update_by = @UpdatedBy, update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId;";

        await connection.ExecuteAsync(new CommandDefinition(specSql, new
        {
            AppId = appId,
            WorkId = workId,
            UpdatedBy = updatedBy,
            UpdatedTs = updatedTs
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Cancelled work {WorkId} for application {AppId}", workId, appId);
        return true;
    }

    public async Task<bool> DeleteWorkAsync(string appId, int workId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var specSql = $@"DELETE FROM [{_schema}].[APP_WORK_SPECS] WHERE application_id = @AppId AND work_id = @WorkId;";
        await connection.ExecuteAsync(new CommandDefinition(specSql, new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken));

        var condSql = $@"DELETE FROM [{_schema}].[APP_WORK_CONDITIONS] WHERE application_id = @AppId AND work_id = @WorkId;";
        await connection.ExecuteAsync(new CommandDefinition(condSql, new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken));

        var workSql = $@"DELETE FROM [{_schema}].[APP_WORKS] WHERE application_id = @AppId AND work_id = @WorkId;";
        var rows = await connection.ExecuteAsync(new CommandDefinition(workSql, new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Deleted work {WorkId} for application {AppId}", workId, appId);
        return true;
    }

    private sealed class WorkTypeFeeRow
    {
        public decimal FeeRate { get; set; }
        public string FeeUnit { get; set; } = string.Empty;
        public int SiteMax { get; set; }
    }

    public async Task UpdateWcrAsync(string appId, int workId, WcrUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var updatedTs = DateTime.UtcNow;
        var sharedFlag = request.DwrNumShared ? "Y" : "N";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sql = $@"
UPDATE [{_schema}].[APP_WORK_SPECS]
SET state_well_id = @StateWellId,
    compl_well_dwr_num = @ComplWellDwrNum,
    dwr_num_shared = @DwrNumShared,
    permit_num = @PermitNum,
    dwr_num = @DwrNum,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId AND work_specs_id = @WorkSpecsId;";

        foreach (var spec in request.Specs)
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                AppId = appId,
                WorkId = workId,
                spec.WorkSpecsId,
                StateWellId = Upper(spec.StateWellId),
                ComplWellDwrNum = Upper(spec.ComplWellDwrNum),
                DwrNumShared = sharedFlag,
                PermitNum = Upper(spec.PermitNum),
                DwrNum = Upper(spec.DwrNum),
                UpdatedBy = updatedBy,
                UpdatedTs = updatedTs
            }, transaction, cancellationToken: cancellationToken));

            if (rows == 0)
            {
                throw new InvalidOperationException($"Well spec {spec.WorkSpecsId} was not found for work {workId} of application {appId}.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Updated WCR details for work {WorkId} ({SpecCount} specs) of application {AppId}", workId, request.Specs.Count, appId);
    }

    public async Task UpdateSpecFileAsync(string appId, int workId, int workSpecsId, string column, string fileName, string updatedBy, CancellationToken cancellationToken = default)
    {
        // Only two callers, both trusted internal literals — keep an allow-list guard anyway.
        var dbColumn = column switch
        {
            "geolog_file" => "geolog_file",
            "dwr_image" => "dwr_image",
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unsupported work-spec file column.")
        };

        var sql = $@"
UPDATE [{_schema}].[APP_WORK_SPECS]
SET {dbColumn} = @FileName,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId AND work_id = @WorkId AND work_specs_id = @WorkSpecsId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppId = appId,
            WorkId = workId,
            WorkSpecsId = workSpecsId,
            FileName = fileName,
            UpdatedBy = updatedBy,
            UpdatedTs = DateTime.UtcNow
        }, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException($"Well spec {workSpecsId} was not found for work {workId} of application {appId}.");
        }

        _logger.LogInformation("Updated {Column} for work spec {WorkSpecsId} (work {WorkId}) of application {AppId}", dbColumn, workSpecsId, workId, appId);
    }

    private static string Upper(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public async Task<int> AddDocumentLinkAsync(AppDocumentLink link, string addedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var seqNum = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT ISNULL(MAX(SEQ_NUM), 0) + 1 FROM [{_schema}].[APP_DOCUMENT_LINKS] WHERE APPLICATION_ID = @AppId;",
            new { AppId = link.AppId }, transaction, cancellationToken: cancellationToken));

        var sql = $@"
INSERT INTO [{_schema}].[APP_DOCUMENT_LINKS]
    (APPLICATION_ID, SEQ_NUM, DOCUMENT_TYPE, OTHER_TYPE_DESC, DOCUMENT_FILENAME, ADD_BY, ADD_TS)
VALUES
    (@AppId, @SeqNum, @DocumentType, @OtherTypeDesc, @DocumentFilename, @AddBy, @AddTs);";
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            link.AppId,
            SeqNum = seqNum,
            link.DocumentType,
            link.OtherTypeDesc,
            link.DocumentFilename,
            AddBy = addedBy,
            AddTs = DateTime.UtcNow
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Inserted document link seq {SeqNum} for application {AppId}", seqNum, link.AppId);
        return seqNum;
    }

    public async Task<IReadOnlyList<AppDocumentDto>> GetDocumentsAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT d.SEQ_NUM AS SeqNum, d.DOCUMENT_TYPE AS DocumentType, dt.DOCUMENT_DESC AS DocumentDesc,
       d.OTHER_TYPE_DESC AS OtherTypeDesc, d.DOCUMENT_FILENAME AS FileName,
       d.ADD_BY AS AddBy, d.ADD_TS AS AddTs
FROM [{_schema}].[APP_DOCUMENT_LINKS] d
LEFT JOIN [{_schema}].[DOCUMENT_TYPES] dt ON d.DOCUMENT_TYPE = dt.DOCUMENT_TYPE
WHERE d.APPLICATION_ID = @AppId
ORDER BY d.DOCUMENT_TYPE, d.OTHER_TYPE_DESC, d.SEQ_NUM;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppDocumentDto>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> DeleteDocumentLinkAsync(string appId, int seqNum, CancellationToken cancellationToken = default)
    {
        var sql = $@"DELETE FROM [{_schema}].[APP_DOCUMENT_LINKS] WHERE APPLICATION_ID = @AppId AND SEQ_NUM = @SeqNum;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { AppId = appId, SeqNum = seqNum }, cancellationToken: cancellationToken));
        _logger.LogInformation("Deleted {Rows} document link(s) seq {SeqNum} for application {AppId}", rows, seqNum, appId);
        return rows > 0;
    }

    public async Task<IReadOnlyList<AppNoteDto>> GetNotesAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT SEQ_NUM AS SeqNum, NOTES_TEXT AS NotesText, ADD_BY AS AddBy, ADD_TS AS AddTs
FROM [{_schema}].[INSPECTION_NOTES]
WHERE APPLICATION_ID = @AppId
ORDER BY SEQ_NUM DESC;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppNoteDto>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AppNoteDto> AddNoteAsync(string appId, string notesText, string addedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var seqNum = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT ISNULL(MAX(SEQ_NUM), 0) + 1 FROM [{_schema}].[INSPECTION_NOTES] WHERE APPLICATION_ID = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        var addTs = DateTime.UtcNow;
        var sql = $@"
INSERT INTO [{_schema}].[INSPECTION_NOTES] (APPLICATION_ID, SEQ_NUM, NOTES_TEXT, ADD_BY, ADD_TS)
VALUES (@AppId, @SeqNum, @NotesText, @AddBy, @AddTs);";
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppId = appId,
            SeqNum = seqNum,
            NotesText = notesText,
            AddBy = addedBy,
            AddTs = addTs
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Inserted note seq {SeqNum} for application {AppId}", seqNum, appId);
        return new AppNoteDto(seqNum, notesText, addedBy, addTs);
    }

    public async Task UpdateExtensionAsync(string appId, DateTime startDate, DateTime endDate, bool isEdit, string updatedBy, CancellationToken cancellationToken = default)
    {
        var updatedTs = DateTime.UtcNow;

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var currentCount = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            $"SELECT extend_count FROM [{_schema}].[APPLICATION_INFO] WHERE application_id = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken)) ?? 0;

        // New extension increments the count; an edit keeps the current window's count (legacy proc=extendu).
        var newCount = isEdit ? currentCount : currentCount + 1;

        var updateSql = $@"
UPDATE [{_schema}].[APPLICATION_INFO]
SET extend_start_date = @StartDate,
    extend_end_date = @EndDate,
    extend_count = @Count,
    extend_by = @UpdatedBy,
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId;";

        var rows = await connection.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            AppId = appId,
            StartDate = startDate,
            EndDate = endDate,
            Count = newCount,
            UpdatedBy = updatedBy,
            UpdatedTs = updatedTs
        }, transaction, cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException($"Application {appId} was not found.");
        }

        // Mirror the legacy inspection-note trail: "Ext. Count: X   From: mm/dd/yyyy   To: mm/dd/yyyy".
        var seqNum = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT ISNULL(MAX(SEQ_NUM), 0) + 1 FROM [{_schema}].[INSPECTION_NOTES] WHERE APPLICATION_ID = @AppId;",
            new { AppId = appId }, transaction, cancellationToken: cancellationToken));

        var noteText = $"Ext. Count: {newCount}      From: {startDate:MM/dd/yyyy}      To: {endDate:MM/dd/yyyy}";
        await connection.ExecuteAsync(new CommandDefinition(
            $@"INSERT INTO [{_schema}].[INSPECTION_NOTES] (APPLICATION_ID, SEQ_NUM, NOTES_TEXT, ADD_BY, ADD_TS)
VALUES (@AppId, @SeqNum, @NotesText, @AddBy, @AddTs);",
            new { AppId = appId, SeqNum = seqNum, NotesText = noteText, AddBy = updatedBy, AddTs = updatedTs },
            transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Recorded permit extension (count {Count}) for application {AppId}", newCount, appId);
    }

    private sealed class HazardRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? ConsultantLastName { get; init; }
        public string? ConsultantFirstName { get; init; }
        public string? ConsultantPhone { get; init; }
        public string? ConsultantCell { get; init; }
        public string? SafetyOfficerLastName { get; init; }
        public string? SafetyOfficerFirstName { get; init; }
        public string? SafetyOfficerPhone { get; init; }
        public string? SafetyOfficerCell { get; init; }
        public string? FacilityType { get; init; }
        public DateTime? SiteSafetyMeetingDateTime { get; init; }
        public string? PpeLevelA { get; init; }
        public string? PpeLevelB { get; init; }
        public string? PpeLevelC { get; init; }
        public string? PpeLevelD { get; init; }
        public string? EquipHardHatFlag { get; init; }
        public string? EquipSafetyShoesFlag { get; init; }
        public string? EquipOrangeVestFlag { get; init; }
        public string? EquipHearingProtFlag { get; init; }
        public string? EquipSafetyEyewearFlag { get; init; }
        public string? EquipClothingFlag { get; init; }
        public string? EquipClothingDesc { get; init; }
        public string? EquipRespiratorFlag { get; init; }
        public string? EquipRespiratorDesc { get; init; }
        public string? EquipCartridgeFlag { get; init; }
        public string? EquipCartridgeDesc { get; init; }
        public string? EquipGlovesFlag { get; init; }
        public string? EquipGlovesDesc { get; init; }
        public string? EquipOtherFlag { get; init; }
        public string? EquipOtherDesc { get; init; }
        public string? InfoProvidedByCompanyName { get; init; }
        public string? InfoProvidedByLastName { get; init; }
        public string? InfoProvidedByFirstName { get; init; }
        public string? InfoProvidedByTitle { get; init; }
        public string? InfoProvidedByPhone { get; init; }
        public string? Acknowledgement { get; init; }
        public DateTime? AddTs { get; init; }

        public HazardInfo ToDomain() => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            ConsultantLastName = ConsultantLastName,
            ConsultantFirstName = ConsultantFirstName,
            ConsultantPhone = ConsultantPhone,
            ConsultantCell = ConsultantCell,
            SafetyOfficerLastName = SafetyOfficerLastName,
            SafetyOfficerFirstName = SafetyOfficerFirstName,
            SafetyOfficerPhone = SafetyOfficerPhone,
            SafetyOfficerCell = SafetyOfficerCell,
            FacilityType = FacilityType,
            SiteSafetyMeetingDateTime = SiteSafetyMeetingDateTime,
            PpeLevelA = Trim(PpeLevelA),
            PpeLevelB = Trim(PpeLevelB),
            PpeLevelC = Trim(PpeLevelC),
            PpeLevelD = Trim(PpeLevelD),
            EquipHardHatFlag = Trim(EquipHardHatFlag),
            EquipSafetyShoesFlag = Trim(EquipSafetyShoesFlag),
            EquipOrangeVestFlag = Trim(EquipOrangeVestFlag),
            EquipHearingProtFlag = Trim(EquipHearingProtFlag),
            EquipSafetyEyewearFlag = Trim(EquipSafetyEyewearFlag),
            EquipClothingFlag = Trim(EquipClothingFlag),
            EquipClothingDesc = EquipClothingDesc,
            EquipRespiratorFlag = Trim(EquipRespiratorFlag),
            EquipRespiratorDesc = EquipRespiratorDesc,
            EquipCartridgeFlag = Trim(EquipCartridgeFlag),
            EquipCartridgeDesc = EquipCartridgeDesc,
            EquipGlovesFlag = Trim(EquipGlovesFlag),
            EquipGlovesDesc = EquipGlovesDesc,
            EquipOtherFlag = Trim(EquipOtherFlag),
            EquipOtherDesc = EquipOtherDesc,
            InfoProvidedByCompanyName = InfoProvidedByCompanyName,
            InfoProvidedByLastName = InfoProvidedByLastName,
            InfoProvidedByFirstName = InfoProvidedByFirstName,
            InfoProvidedByTitle = InfoProvidedByTitle,
            InfoProvidedByPhone = InfoProvidedByPhone,
            Acknowledgement = Trim(Acknowledgement),
            AddTs = AddTs
        };

        private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private sealed class SubstanceRow
    {
        public string AppId { get; init; } = string.Empty;
        public int HazardSeq { get; init; }
        public string? ConcentrationsPpm { get; init; }
        public string? PelPpm { get; init; }
        public string? HealthEffects { get; init; }

        public HazardSubstance ToDomain() => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            HazardSeq = HazardSeq,
            ConcentrationsPpm = ConcentrationsPpm,
            PelPpm = PelPpm,
            HealthEffects = HealthEffects
        };
    }

    private sealed class DocumentRow
    {
        public string AppId { get; init; } = string.Empty;
        public int SeqNum { get; init; }
        public string? DocumentType { get; init; }
        public string? OtherTypeDesc { get; init; }
        public string? DocumentFilename { get; init; }

        public AppDocumentLink ToDomain() => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            SeqNum = SeqNum,
            DocumentType = DocumentType?.Trim(),
            OtherTypeDesc = OtherTypeDesc,
            DocumentFilename = DocumentFilename
        };
    }

    private sealed class EmailCcRow
    {
        public string AppId { get; init; } = string.Empty;
        public int EmailCcId { get; init; }
        public string EmailAddrCc { get; init; } = string.Empty;

        public AppEmailCc ToDomain() => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            EmailCcId = EmailCcId,
            EmailAddrCc = EmailAddrCc
        };
    }

    private sealed class NoteRow
    {
        public string AppId { get; init; } = string.Empty;
        public int NoteId { get; init; }
        public string AddBy { get; init; } = string.Empty;
        public DateTime AddTs { get; init; }
        public string? NotesText { get; init; }

        public AppNote ToDomain() => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            NoteId = NoteId,
            AddBy = AddBy,
            AddTs = AddTs,
            NotesText = NotesText
        };
    }

    private sealed class WorkRow
    {
        public string AppId { get; init; } = string.Empty;
        public int WorkId { get; init; }
        public string WorkCategory { get; init; } = string.Empty;
        public string WorkType { get; init; } = string.Empty;
        public string? WellUseType { get; init; }
        public string? DrillerName { get; init; }
        public string? DrillerLicenseNum { get; init; }
        public string? DrillMethodType { get; init; }
        public string? DrillMethodOtherDesc { get; init; }
        public decimal? WorkFeeRate { get; init; }
        public string? WorkFeeUnit { get; init; }
        public int? WorkSiteMax { get; init; }
        public decimal? WorkSiteExtraRate { get; init; }
        public string? StatusCode { get; init; }
        public string? WorkCategoryDesc { get; init; }
        public string? WorkTypeDesc { get; init; }
        public string? WellUseDesc { get; init; }
        public string? DrillMethodName { get; init; }

        public ApplicationWork ToDomain() => new()
        {
            AppId = AppId,
            WorkId = WorkId,
            WorkCategory = (WorkCategory ?? string.Empty).Trim(),
            WorkType = (WorkType ?? string.Empty).Trim(),
            WellUseType = WellUseType?.Trim(),
            WorkCategoryDesc = WorkCategoryDesc?.Trim(),
            WorkTypeDesc = WorkTypeDesc?.Trim(),
            WellUseDesc = WellUseDesc?.Trim(),
            DrillMethodName = DrillMethodName?.Trim(),
            DrillerName = DrillerName,
            DrillerLicenseNum = DrillerLicenseNum,
            DrillMethodType = DrillMethodType?.Trim(),
            DrillMethodOtherDesc = DrillMethodOtherDesc,
            WorkFeeRate = WorkFeeRate,
            WorkFeeUnit = WorkFeeUnit?.Trim(),
            WorkSiteMax = WorkSiteMax,
            WorkSiteExtraRate = WorkSiteExtraRate,
            StatusCode = (StatusCode ?? string.Empty).Trim()
        };
    }

    private sealed class SpecRow
    {
        public string AppId { get; init; } = string.Empty;
        public int WorkId { get; init; }
        public int WorkSpecsId { get; init; }
        public string? OwnerWellNum { get; init; }
        public int? DrillCount { get; init; }
        public decimal? HoleDiamIn { get; init; }
        public decimal? CasingDiamIn { get; init; }
        public decimal? SealDepthFt { get; init; }
        public decimal? MaxDepthFt { get; init; }
        public string? Latitude { get; init; }
        public string? Longitude { get; init; }
        public string? StateWellId { get; init; }
        public string? DwrNum { get; init; }
        public string? PermitNum { get; init; }
        public string? ComplWellDwrNum { get; init; }
        public string? DwrNumShared { get; init; }
        public string? DwrImage { get; init; }
        public string? GeologFile { get; init; }
        public string? StatusCode { get; init; }

        public ApplicationWorkSpec ToDomain() => new()
        {
            AppId = AppId,
            WorkId = WorkId,
            WorkSpecsId = WorkSpecsId,
            OwnerWellNum = OwnerWellNum,
            DrillCount = DrillCount,
            HoleDiamIn = HoleDiamIn,
            CasingDiamIn = CasingDiamIn,
            SealDepthFt = SealDepthFt,
            MaxDepthFt = MaxDepthFt,
            Latitude = Latitude,
            Longitude = Longitude,
            StateWellId = StateWellId,
            DwrNum = DwrNum,
            PermitNum = PermitNum,
            ComplWellDwrNum = ComplWellDwrNum,
            DwrNumShared = DwrNumShared,
            DwrImage = DwrImage,
            GeologFile = GeologFile,
            StatusCode = (StatusCode ?? string.Empty).Trim()
        };
    }

    private sealed class DrillerLookupRow
    {
        public string? AppId { get; init; }
        public string? DrillerName { get; init; }
    }

    private sealed class ApplicationRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? StatusCode { get; init; }
        public DateTime? AddDate { get; init; }
        public string? AddBy { get; init; }
        public string? SiteCityCode { get; init; }
        public string? SiteCityName { get; init; }
        public string? SiteLocation { get; init; }
        public string? SiteLat { get; init; }
        public string? SiteLong { get; init; }
        public DateTime? ProjStartDate { get; init; }
        public DateTime? ProjEndDate { get; init; }
        public string? SiteHazardRequired { get; init; }
        public string? SiteVisitType { get; init; }
        public string? SitemapFilename { get; init; }
        public DateTime? SitemapReceivedDate { get; init; }
        public DateTime? ExtendStartDate { get; init; }
        public DateTime? ExtendEndDate { get; init; }
        public int? ExtendCount { get; init; }
        public string? ExtendBy { get; init; }
        public string? AppBusinessName { get; init; }
        public string? AppLastName { get; init; }
        public string? AppFirstName { get; init; }
        public string? AppEmailAddr { get; init; }
        public string? AppAddrStreet { get; init; }
        public string? AppAddrStreet2 { get; init; }
        public string? AppAddrCity { get; init; }
        public string? AppAddrState { get; init; }
        public string? AppAddrZip { get; init; }
        public string? AppPhone { get; init; }
        public string? AppFax { get; init; }
        public string? ContactLastName { get; init; }
        public string? ContactFirstName { get; init; }
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? ContactCell { get; init; }
        public string? OwnerLastName { get; init; }
        public string? OwnerFirstName { get; init; }
        public string? OwnerAddrStreet { get; init; }
        public string? OwnerAddrCity { get; init; }
        public string? OwnerAddrState { get; init; }
        public string? OwnerAddrZip { get; init; }
        public string? OwnerPhone { get; init; }
        public string? OwnerEmail { get; init; }
        public string? ClientLastName { get; init; }
        public string? ClientFirstName { get; init; }
        public string? ClientAddrStreet { get; init; }
        public string? ClientAddrCity { get; init; }
        public string? ClientAddrState { get; init; }
        public string? ClientAddrZip { get; init; }
        public string? ClientPhone { get; init; }
        public string? ClientEmail { get; init; }

        public DomainApplication ToDomain(IList<ApplicationWork> works) => new()
        {
            AppId = (AppId ?? string.Empty).Trim(),
            StatusCode = (StatusCode ?? string.Empty).Trim(),
            AddDate = AddDate ?? default,
            AddBy = AddBy ?? string.Empty,
            SiteCityCode = SiteCityCode?.Trim(),
            SiteCityName = SiteCityName?.Trim(),
            SiteLocation = SiteLocation,
            SiteLat = SiteLat,
            SiteLong = SiteLong,
            ProjStartDate = ProjStartDate,
            ProjEndDate = ProjEndDate,
            SiteHazardRequired = SiteHazardRequired?.Trim(),
            SiteVisitType = SiteVisitType?.Trim(),
            SitemapFilename = SitemapFilename,
            SitemapReceivedDate = SitemapReceivedDate,
            ExtendStartDate = ExtendStartDate,
            ExtendEndDate = ExtendEndDate,
            ExtendCount = ExtendCount,
            ExtendBy = ExtendBy?.Trim(),
            Applicant = new Applicant
            {
                AppBusinessName = AppBusinessName,
                AppLastName = AppLastName,
                AppFirstName = AppFirstName,
                AppEmailAddr = AppEmailAddr,
                AppAddrStreet = AppAddrStreet,
                AppAddrStreet2 = AppAddrStreet2,
                AppAddrCity = AppAddrCity,
                AppAddrState = AppAddrState?.Trim(),
                AppAddrZip = AppAddrZip,
                AppPhone = AppPhone,
                AppFax = AppFax
            },
            Contact = new ContactInfo
            {
                ContactLastName = ContactLastName,
                ContactFirstName = ContactFirstName,
                ContactEmail = ContactEmail,
                ContactPhone = ContactPhone,
                ContactCell = ContactCell
            },
            Owner = new PartyInfo
            {
                LastName = OwnerLastName,
                FirstName = OwnerFirstName,
                AddrStreet = OwnerAddrStreet,
                AddrCity = OwnerAddrCity,
                AddrState = OwnerAddrState?.Trim(),
                AddrZip = OwnerAddrZip,
                Phone = OwnerPhone,
                Email = OwnerEmail
            },
            Client = new PartyInfo
            {
                LastName = ClientLastName,
                FirstName = ClientFirstName,
                AddrStreet = ClientAddrStreet,
                AddrCity = ClientAddrCity,
                AddrState = ClientAddrState?.Trim(),
                AddrZip = ClientAddrZip,
                Phone = ClientPhone,
                Email = ClientEmail
            },
            Works = works
        };
    }
}
