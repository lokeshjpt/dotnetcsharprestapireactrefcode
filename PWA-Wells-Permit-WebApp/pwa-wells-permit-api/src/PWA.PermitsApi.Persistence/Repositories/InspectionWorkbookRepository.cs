using Dapper;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Persistence.Repositories;

/// <summary>
/// Minimal Dapper mapping for the X_INSPECTION_WORKBOOK parent and its per-work-type child rows,
/// using the real EEAOWN column names. Only the columns needed for read/insert coverage are mapped.
/// </summary>
public sealed class InspectionWorkbookRepository : IInspectionWorkbookRepository
{
    private readonly DapperContext _context;
    private readonly string _schema;

    public InspectionWorkbookRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<InspectionWorkbook?> GetByApplicationAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var workbook = await connection.QuerySingleOrDefaultAsync<InspectionWorkbook>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, STATUS_CODE AS StatusCode, FINAL_APPROVAL_BY AS FinalApprovalBy,
       FINAL_APPROVAL_DATE AS FinalApprovalDate, ADD_BY AS AddBy, ADD_TS AS AddTs
FROM [{_schema}].[X_INSPECTION_WORKBOOK]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken));

        if (workbook is null)
        {
            return null;
        }

        workbook.Borings = (await connection.QueryAsync<InspectionWorkbookBoring>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, WORK_ID AS WorkId, WORK_SPECS_ID AS WorkSpecsId, WORKBOOK_ID AS WorkbookId,
       BOR_PARCEL_USE AS BorParcelUse, BOR_PARCEL_USE_OTHER_DESC AS BorParcelUseOtherDesc,
       BOR_NUMBER_OF_BORINGS AS BorNumberOfBorings, BOR_BOREHOLE_DIAMETER_IN AS BorBoreholeDiameterIn,
       BOR_BORING_DEPTH_FT AS BorBoringDepthFt, BOR_SEAL_DEPTH_FT AS BorSealDepthFt,
       BOR_STATIC_WATER_LEVEL_FT AS BorStaticWaterLevelFt, BOR_SEAL_MATERIAL_QUANTITY_MIX AS BorSealMaterialQuantityMix
FROM [{_schema}].[X_INSPECTION_WORKBOOK_BORING]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken))).ToList();

        workbook.Constructions = (await connection.QueryAsync<InspectionWorkbookConstruction>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, WORK_ID AS WorkId, WORK_SPECS_ID AS WorkSpecsId, WORKBOOK_ID AS WorkbookId,
       CON_SEQ_ID AS ConSeqId, CON_SMALL_WATER_SYSTEM_TYPE AS ConSmallWaterSystemType, CON_DEPTH_FT AS ConDepthFt,
       CON_BOREHOLE_DIAMETER_IN AS ConBoreholeDiameterIn, CON_WELL_CASING_DIAMETER_IN AS ConWellCasingDiameterIn,
       CON_ANNULAR_SEAL_DEPTH_FT AS ConAnnularSealDepthFt, CON_STATIC_WATER_LEVEL_FT AS ConStaticWaterLevelFt,
       CON_ANNULAR_SEAL_MATERIAL AS ConAnnularSealMaterial
FROM [{_schema}].[X_INSPECTION_WORKBOOK_CONSTRUCTION]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken))).ToList();

        workbook.Destructions = (await connection.QueryAsync<InspectionWorkbookDestruction>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, WORK_ID AS WorkId, WORK_SPECS_ID AS WorkSpecsId, WORKBOOK_ID AS WorkbookId,
       DES_SEQ_ID AS DesSeqId, DES_NUMBER_OF_WELLS AS DesNumberOfWells, DES_METHOD AS DesMethod,
       DES_WELL_DIAMETER_FT AS DesWellDiameterFt, DES_WELL_DEPTH_FT AS DesWellDepthFt, DES_SEAL_DEPTH_FT AS DesSealDepthFt,
       DES_SEALING_MATERIAL AS DesSealingMaterial
FROM [{_schema}].[X_INSPECTION_WORKBOOK_DESTRUCTION]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken))).ToList();

        workbook.Monitorings = (await connection.QueryAsync<InspectionWorkbookMonitoring>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, WORK_ID AS WorkId, WORK_SPECS_ID AS WorkSpecsId, WORKBOOK_ID AS WorkbookId,
       MON_NUMBER_OF_WELLS AS MonNumberOfWells, MON_WELL_DEPTH_FT AS MonWellDepthFt, MON_WELLS_DURATION_MO AS MonWellsDurationMo,
       MON_WELLS_DURATION_YR AS MonWellsDurationYr, MON_SEAL_DEPTH_FT AS MonSealDepthFt,
       MON_STATIC_WATER_LEVEL_FT AS MonStaticWaterLevelFt, MON_SEAL_MATERIAL_QUANTITY_MIX AS MonSealMaterialQuantityMix
FROM [{_schema}].[X_INSPECTION_WORKBOOK_MONITORING]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken))).ToList();

        workbook.MonWellMeasures = (await connection.QueryAsync<InspectionWorkbookMonWellMeasure>(new CommandDefinition($@"
SELECT APPLICATION_ID AS ApplicationId, WORKBOOK_ID AS WorkbookId, SEQ_ID AS SeqId,
       NUMBER_OF_WELLS AS NumberOfWells, WELL_DEPTH AS WellDepth
FROM [{_schema}].[X_INSPECTION_WORKBOOK_MON_WELL_MEASURES]
WHERE APPLICATION_ID = @ApplicationId;", new { ApplicationId = applicationId }, cancellationToken: cancellationToken))).ToList();

        return workbook;
    }

    public async Task CreateAsync(InspectionWorkbook workbook, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK]
    (APPLICATION_ID, STATUS_CODE, FINAL_APPROVAL_BY, FINAL_APPROVAL_DATE, ADD_BY, ADD_TS)
VALUES (@ApplicationId, @StatusCode, @FinalApprovalBy, @FinalApprovalDate, @AddBy, @AddTs);", new
        {
            workbook.ApplicationId,
            workbook.StatusCode,
            workbook.FinalApprovalBy,
            workbook.FinalApprovalDate,
            AddBy = workbook.AddBy,
            AddTs = now
        }, transaction, cancellationToken: cancellationToken));

        foreach (var b in workbook.Borings)
        {
            await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK_BORING]
    (APPLICATION_ID, WORK_ID, WORK_SPECS_ID, WORKBOOK_ID, BOR_PARCEL_USE, BOR_PARCEL_USE_OTHER_DESC,
     BOR_NUMBER_OF_BORINGS, BOR_BOREHOLE_DIAMETER_IN, BOR_BORING_DEPTH_FT, BOR_SEAL_DEPTH_FT,
     BOR_STATIC_WATER_LEVEL_FT, BOR_SEAL_MATERIAL_QUANTITY_MIX, ADD_BY, ADD_TS)
VALUES (@ApplicationId, @WorkId, @WorkSpecsId, @WorkbookId, @BorParcelUse, @BorParcelUseOtherDesc,
        @BorNumberOfBorings, @BorBoreholeDiameterIn, @BorBoringDepthFt, @BorSealDepthFt,
        @BorStaticWaterLevelFt, @BorSealMaterialQuantityMix, @AddBy, @AddTs);", new
            {
                b.ApplicationId, b.WorkId, b.WorkSpecsId, b.WorkbookId, b.BorParcelUse, b.BorParcelUseOtherDesc,
                b.BorNumberOfBorings, b.BorBoreholeDiameterIn, b.BorBoringDepthFt, b.BorSealDepthFt,
                b.BorStaticWaterLevelFt, b.BorSealMaterialQuantityMix, AddBy = workbook.AddBy, AddTs = now
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var c in workbook.Constructions)
        {
            await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK_CONSTRUCTION]
    (APPLICATION_ID, WORK_ID, WORK_SPECS_ID, WORKBOOK_ID, CON_SEQ_ID, CON_SMALL_WATER_SYSTEM_TYPE, CON_DEPTH_FT,
     CON_BOREHOLE_DIAMETER_IN, CON_WELL_CASING_DIAMETER_IN, CON_ANNULAR_SEAL_DEPTH_FT, CON_STATIC_WATER_LEVEL_FT,
     CON_ANNULAR_SEAL_MATERIAL, ADD_BY, ADD_TS)
VALUES (@ApplicationId, @WorkId, @WorkSpecsId, @WorkbookId, @ConSeqId, @ConSmallWaterSystemType, @ConDepthFt,
        @ConBoreholeDiameterIn, @ConWellCasingDiameterIn, @ConAnnularSealDepthFt, @ConStaticWaterLevelFt,
        @ConAnnularSealMaterial, @AddBy, @AddTs);", new
            {
                c.ApplicationId, c.WorkId, c.WorkSpecsId, c.WorkbookId, c.ConSeqId, c.ConSmallWaterSystemType, c.ConDepthFt,
                c.ConBoreholeDiameterIn, c.ConWellCasingDiameterIn, c.ConAnnularSealDepthFt, c.ConStaticWaterLevelFt,
                c.ConAnnularSealMaterial, AddBy = workbook.AddBy, AddTs = now
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var d in workbook.Destructions)
        {
            await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK_DESTRUCTION]
    (APPLICATION_ID, WORK_ID, WORK_SPECS_ID, WORKBOOK_ID, DES_SEQ_ID, DES_NUMBER_OF_WELLS, DES_METHOD,
     DES_WELL_DIAMETER_FT, DES_WELL_DEPTH_FT, DES_SEAL_DEPTH_FT, DES_SEALING_MATERIAL, ADD_BY, ADD_TS)
VALUES (@ApplicationId, @WorkId, @WorkSpecsId, @WorkbookId, @DesSeqId, @DesNumberOfWells, @DesMethod,
        @DesWellDiameterFt, @DesWellDepthFt, @DesSealDepthFt, @DesSealingMaterial, @AddBy, @AddTs);", new
            {
                d.ApplicationId, d.WorkId, d.WorkSpecsId, d.WorkbookId, d.DesSeqId, d.DesNumberOfWells, d.DesMethod,
                d.DesWellDiameterFt, d.DesWellDepthFt, d.DesSealDepthFt, d.DesSealingMaterial, AddBy = workbook.AddBy, AddTs = now
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var m in workbook.Monitorings)
        {
            await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK_MONITORING]
    (APPLICATION_ID, WORK_ID, WORK_SPECS_ID, WORKBOOK_ID, MON_NUMBER_OF_WELLS, MON_WELL_DEPTH_FT, MON_WELLS_DURATION_MO,
     MON_WELLS_DURATION_YR, MON_SEAL_DEPTH_FT, MON_STATIC_WATER_LEVEL_FT, MON_SEAL_MATERIAL_QUANTITY_MIX, ADD_BY, ADD_TS)
VALUES (@ApplicationId, @WorkId, @WorkSpecsId, @WorkbookId, @MonNumberOfWells, @MonWellDepthFt, @MonWellsDurationMo,
        @MonWellsDurationYr, @MonSealDepthFt, @MonStaticWaterLevelFt, @MonSealMaterialQuantityMix, @AddBy, @AddTs);", new
            {
                m.ApplicationId, m.WorkId, m.WorkSpecsId, m.WorkbookId, m.MonNumberOfWells, m.MonWellDepthFt, m.MonWellsDurationMo,
                m.MonWellsDurationYr, m.MonSealDepthFt, m.MonStaticWaterLevelFt, m.MonSealMaterialQuantityMix, AddBy = workbook.AddBy, AddTs = now
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var w in workbook.MonWellMeasures)
        {
            await connection.ExecuteAsync(new CommandDefinition($@"
INSERT INTO [{_schema}].[X_INSPECTION_WORKBOOK_MON_WELL_MEASURES]
    (APPLICATION_ID, WORKBOOK_ID, SEQ_ID, NUMBER_OF_WELLS, WELL_DEPTH)
VALUES (@ApplicationId, @WorkbookId, @SeqId, @NumberOfWells, @WellDepth);", new
            {
                w.ApplicationId, w.WorkbookId, w.SeqId, w.NumberOfWells, w.WellDepth
            }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
