using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

public sealed class HazardMappingTests
{
    private static ApplicationService CreateService(
        FakeApplicationRepository repository,
        FakePaymentRepository paymentRepository,
        FakeInspectionRepository inspectionRepository,
        FakeIntelliPayGateway gateway) =>
        new(repository, paymentRepository, inspectionRepository, gateway, new FakePermitNotificationService(), NullLogger<ApplicationService>.Instance);

    private static SubmitApplicationRequest RequestWith(SubmitHazardRequest? hazard, string paymentType = "CHECK") => new()
    {
        PaymentType = paymentType,
        CheckNum = "1001",
        Hazard = hazard,
        Works = new List<SubmitWorkRequest>
        {
            new()
            {
                WorkCategory = "con",
                WorkType = "conwell",
                WorkFeeRate = 100m,
                WorkFeeUnit = "EA",
                Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } }
            }
        }
    };

    private static async Task<(FakeApplicationRepository Repo, ApplicationDto Dto)> SubmitAsync(SubmitApplicationRequest request)
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);
        var dto = await service.SubmitAsync(request);
        return (repository, dto);
    }

    // 1. PPE checkbox inputs -> "Y"/"N" via NormalizeYn.
    [Theory]
    [InlineData("Y", "Y")]
    [InlineData("y", "Y")]
    [InlineData("N", "N")]
    [InlineData("", "N")]
    [InlineData(null, "N")]
    [InlineData("on", "N")]
    public async Task BuildHazard_NormalizesPpeLevelsToYn(string? input, string expected)
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            PpeLevelA = input,
            PpeLevelB = input,
            PpeLevelC = input,
            PpeLevelD = input
        }));

        var hazard = repo.Created!.Hazard!;
        Assert.Equal(expected, hazard.PpeLevelA);
        Assert.Equal(expected, hazard.PpeLevelB);
        Assert.Equal(expected, hazard.PpeLevelC);
        Assert.Equal(expected, hazard.PpeLevelD);
    }

    [Fact]
    public async Task BuildHazard_MapsDistinctPpeLevelsIndependently()
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            PpeLevelA = "Y",
            PpeLevelB = null,
            PpeLevelC = "y",
            PpeLevelD = "x"
        }));

        var hazard = repo.Created!.Hazard!;
        Assert.Equal("Y", hazard.PpeLevelA);
        Assert.Equal("N", hazard.PpeLevelB);
        Assert.Equal("Y", hazard.PpeLevelC);
        Assert.Equal("N", hazard.PpeLevelD);
    }

    // 2. Equipment flags: "R" -> "R", "A" -> "A", null/empty/other -> null (NormalizeFlag).
    [Theory]
    [InlineData("R", "R")]
    [InlineData("r", "R")]
    [InlineData("A", "A")]
    [InlineData("a", "A")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("Y", null)]
    [InlineData("X", null)]
    public async Task BuildHazard_NormalizesEquipmentFlags(string? input, string? expected)
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            EquipHardHatFlag = input,
            EquipSafetyShoesFlag = input,
            EquipOrangeVestFlag = input,
            EquipHearingProtFlag = input,
            EquipSafetyEyewearFlag = input,
            EquipClothingFlag = input,
            EquipRespiratorFlag = input,
            EquipCartridgeFlag = input,
            EquipGlovesFlag = input,
            EquipOtherFlag = input
        }));

        var hazard = repo.Created!.Hazard!;
        Assert.Equal(expected, hazard.EquipHardHatFlag);
        Assert.Equal(expected, hazard.EquipSafetyShoesFlag);
        Assert.Equal(expected, hazard.EquipOrangeVestFlag);
        Assert.Equal(expected, hazard.EquipHearingProtFlag);
        Assert.Equal(expected, hazard.EquipSafetyEyewearFlag);
        Assert.Equal(expected, hazard.EquipClothingFlag);
        Assert.Equal(expected, hazard.EquipRespiratorFlag);
        Assert.Equal(expected, hazard.EquipCartridgeFlag);
        Assert.Equal(expected, hazard.EquipGlovesFlag);
        Assert.Equal(expected, hazard.EquipOtherFlag);
    }

    // 3. Equipment desc fields flow through unchanged.
    [Fact]
    public async Task BuildHazard_PassesEquipmentDescriptionsThrough()
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            EquipClothingDesc = "Tyvek suit",
            EquipRespiratorDesc = "Half-face",
            EquipCartridgeDesc = "Organic vapor",
            EquipGlovesDesc = "Nitrile",
            EquipOtherDesc = "Boot covers"
        }));

        var hazard = repo.Created!.Hazard!;
        Assert.Equal("Tyvek suit", hazard.EquipClothingDesc);
        Assert.Equal("Half-face", hazard.EquipRespiratorDesc);
        Assert.Equal("Organic vapor", hazard.EquipCartridgeDesc);
        Assert.Equal("Nitrile", hazard.EquipGlovesDesc);
        Assert.Equal("Boot covers", hazard.EquipOtherDesc);
    }

    // 4. Provider fields map through.
    [Fact]
    public async Task BuildHazard_PassesInfoProvidedByFieldsThrough()
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            InfoProvidedByCompanyName = "Acme Enviro",
            InfoProvidedByLastName = "Doe",
            InfoProvidedByFirstName = "Jane",
            InfoProvidedByTitle = "Safety Manager",
            InfoProvidedByPhone = "510-555-2000"
        }));

        var hazard = repo.Created!.Hazard!;
        Assert.Equal("Acme Enviro", hazard.InfoProvidedByCompanyName);
        Assert.Equal("Doe", hazard.InfoProvidedByLastName);
        Assert.Equal("Jane", hazard.InfoProvidedByFirstName);
        Assert.Equal("Safety Manager", hazard.InfoProvidedByTitle);
        Assert.Equal("5105552000", hazard.InfoProvidedByPhone);
    }

    // 5. Acknowledgement bool -> "Y"/"N".
    [Theory]
    [InlineData(true, "Y")]
    [InlineData(false, "N")]
    public async Task BuildHazard_MapsAcknowledgementBoolToYn(bool input, string expected)
    {
        var (repo, _) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            Acknowledgement = input
        }));

        Assert.Equal(expected, repo.Created!.Hazard!.Acknowledgement);
    }

    // 6. MapHazard round-trips the domain values back into the returned HazardDto.
    [Fact]
    public async Task MapHazard_RoundTripsAllNewFieldsIntoDto()
    {
        var (_, dto) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = true,
            PpeLevelA = "Y",
            PpeLevelB = "N",
            PpeLevelC = "y",
            PpeLevelD = null,
            EquipHardHatFlag = "R",
            EquipSafetyShoesFlag = "A",
            EquipOrangeVestFlag = "R",
            EquipHearingProtFlag = "A",
            EquipSafetyEyewearFlag = "R",
            EquipClothingFlag = "A",
            EquipClothingDesc = "Tyvek",
            EquipRespiratorFlag = "R",
            EquipRespiratorDesc = "Full-face",
            EquipCartridgeFlag = "A",
            EquipCartridgeDesc = "OV/AG",
            EquipGlovesFlag = "R",
            EquipGlovesDesc = "Nitrile",
            EquipOtherFlag = "X",
            EquipOtherDesc = "Face shield",
            InfoProvidedByCompanyName = "Acme",
            InfoProvidedByLastName = "Doe",
            InfoProvidedByFirstName = "Jane",
            InfoProvidedByTitle = "SO",
            InfoProvidedByPhone = "510-555-2000",
            Acknowledgement = true
        }));

        var hazard = dto.Hazard!;
        Assert.NotNull(hazard);
        Assert.Equal("Y", hazard.PpeLevelA);
        Assert.Equal("N", hazard.PpeLevelB);
        Assert.Equal("Y", hazard.PpeLevelC);
        Assert.Equal("N", hazard.PpeLevelD);

        Assert.Equal("R", hazard.EquipHardHatFlag);
        Assert.Equal("A", hazard.EquipSafetyShoesFlag);
        Assert.Equal("R", hazard.EquipOrangeVestFlag);
        Assert.Equal("A", hazard.EquipHearingProtFlag);
        Assert.Equal("R", hazard.EquipSafetyEyewearFlag);
        Assert.Equal("A", hazard.EquipClothingFlag);
        Assert.Equal("Tyvek", hazard.EquipClothingDesc);
        Assert.Equal("R", hazard.EquipRespiratorFlag);
        Assert.Equal("Full-face", hazard.EquipRespiratorDesc);
        Assert.Equal("A", hazard.EquipCartridgeFlag);
        Assert.Equal("OV/AG", hazard.EquipCartridgeDesc);
        Assert.Equal("R", hazard.EquipGlovesFlag);
        Assert.Equal("Nitrile", hazard.EquipGlovesDesc);
        Assert.Null(hazard.EquipOtherFlag); // "X" normalizes to null
        Assert.Equal("Face shield", hazard.EquipOtherDesc);

        Assert.Equal("Acme", hazard.InfoProvidedByCompanyName);
        Assert.Equal("Doe", hazard.InfoProvidedByLastName);
        Assert.Equal("Jane", hazard.InfoProvidedByFirstName);
        Assert.Equal("SO", hazard.InfoProvidedByTitle);
        Assert.Equal("5105552000", hazard.InfoProvidedByPhone);
        Assert.Equal("Y", hazard.Acknowledgement);
    }

    // 7a. Hazard null on request -> no hazard persisted / mapped.
    [Fact]
    public async Task Submit_WithNullHazard_LeavesHazardNull()
    {
        var (repo, dto) = await SubmitAsync(RequestWith(null));

        Assert.Null(repo.Created!.Hazard);
        Assert.Null(dto.Hazard);
    }

    // 7b. Hazard present=false -> skipped even if fields are populated.
    [Fact]
    public async Task Submit_WithHazardPresentFalse_SkipsHazard()
    {
        var (repo, dto) = await SubmitAsync(RequestWith(new SubmitHazardRequest
        {
            Present = false,
            PpeLevelA = "Y",
            EquipHardHatFlag = "R",
            Acknowledgement = true,
            InfoProvidedByLastName = "Ignored"
        }));

        Assert.Null(repo.Created!.Hazard);
        Assert.Null(dto.Hazard);
    }

    // 8. Payment status quick assertions (EXMPT / CC-defer) alongside hazard mapping.
    [Fact]
    public async Task Submit_ExemptPayment_ProducesExmptStatusAndZeroPaid()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(RequestWith(new SubmitHazardRequest { Present = true, Acknowledgement = true }, paymentType: "EXMPT"));

        Assert.Equal("EXMPT", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.Null(paymentRepository.Saved.AuthIdEncr);
        Assert.Equal(0, gateway.VaultCalls);
    }

    [Fact]
    public async Task Submit_CreditCard_DefersChargeAndVaultsCard()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(RequestWith(new SubmitHazardRequest { Present = true, Acknowledgement = true }, paymentType: "CC"));

        Assert.Equal(1, gateway.VaultCalls);
        Assert.Equal("PEND", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.False(string.IsNullOrWhiteSpace(paymentRepository.Saved.AuthIdEncr));
    }
}
