using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Tests;

public sealed class ApplicationServiceTests
{
    private static ApplicationService CreateService(
        FakeApplicationRepository repository,
        FakePaymentRepository paymentRepository,
        FakeInspectionRepository inspectionRepository,
        FakeIntelliPayGateway gateway,
        FakePermitNotificationService? notifications = null) =>
        new(repository, paymentRepository, inspectionRepository, gateway, notifications ?? new FakePermitNotificationService(), NullLogger<ApplicationService>.Instance);

    [Fact]
    public async Task SubmitAsync_GeneratesTimestampStyleApplicationId()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        var result = await service.SubmitAsync(new SubmitApplicationRequest
        {
            AppFirstName = "Tina",
            AppLastName = "Lopez",
            AppEmailAddr = "tina@example.com",
            SiteLocation = "123 Main St",
            SiteCityCode = "OAK",
            SiteCityName = "Oakland",
            PaymentType = "CC",
            Works = new List<SubmitWorkRequest>
            {
                new()
                {
                    WorkCategory = "con",
                    WorkType = "conwell",
                    WorkFeeRate = 250m,
                    WorkFeeUnit = "EA",
                    Specs = new List<SubmitWorkSpecRequest>
                    {
                        new() { OwnerWellNum = "W-1" }
                    }
                }
            }
        });

        Assert.Equal(13, result.AppId.Length);
        Assert.Equal("PENDS", result.StatusCode);
        Assert.Single(result.WorkTypes);
        Assert.Equal("conwell", result.WorkTypes[0]);
    }

    [Fact]
    public async Task GetByIdAsync_SurfacesWorkAndWellUseDescriptions()
    {
        // The work record carries the resolved lookup descriptions (from the repository join) so the
        // intra detail page and permit PDF can show "Construction - Domestic Water Well" / "Domestic"
        // rather than the raw codes "con" / "dom".
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PENDS",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" },
                Works = new List<ApplicationWork>
                {
                    new()
                    {
                        WorkId = 1,
                        WorkCategory = "con",
                        WorkType = "conwell",
                        WellUseType = "dom",
                        WorkCategoryDesc = "Construction",
                        WorkTypeDesc = "Domestic Water Well",
                        WellUseDesc = "Domestic",
                        DrillMethodType = "MUD",
                        DrillMethodName = "Mud Rotary",
                        StatusCode = "PENDC"
                    }
                }
            }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.GetByIdAsync("1234567890123");

        Assert.NotNull(result);
        var work = Assert.Single(result!.Works);
        Assert.Equal("Construction", work.WorkCategoryDesc);
        Assert.Equal("Domestic Water Well", work.WorkTypeDesc);
        Assert.Equal("Domestic", work.WellUseDesc);
        Assert.Equal("Mud Rotary", work.DrillMethodName);
    }

    [Fact]
    public async Task SubmitAsync_DefaultsWorkSpecNumericsForLegacyIntranetCompatibility()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CC",
            Works = new List<SubmitWorkRequest>
            {
                new()
                {
                    WorkCategory = "con",
                    WorkType = "conwell",
                    WorkFeeRate = null,
                    WorkFeeUnit = "well",
                    WorkSiteMax = null,
                    Specs = new List<SubmitWorkSpecRequest>
                    {
                        new() { OwnerWellNum = "W-1", DrillCount = null }
                    }
                }
            }
        });

        var work = repository.Created!.Works[0];
        Assert.Equal(0m, work.WorkFeeRate);
        Assert.Equal(0, work.WorkSiteMax);
        Assert.Equal(0m, work.WorkSiteExtraRate);
        Assert.Equal(1, work.Specs[0].DrillCount);
    }

    [Fact]
    public async Task SubmitAsync_LeavesPaymentPendingWithComputedAuthAmount()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CC",
            Works = new List<SubmitWorkRequest>
            {
                new()
                {
                    WorkCategory = "con",
                    WorkType = "conwell",
                    WorkFeeRate = 100m,
                    WorkFeeUnit = "EA",
                    Specs = new List<SubmitWorkSpecRequest>
                    {
                        new() { OwnerWellNum = "W-1" },
                        new() { OwnerWellNum = "W-2" }
                    }
                }
            }
        });

        Assert.NotNull(paymentRepository.Saved);
        Assert.Equal("PEND", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.Equal(200m, paymentRepository.Saved.AuthAmount);
    }

    [Fact]
    public async Task SubmitAsync_AppliesSiteExtraRateForTieredSiteWorkBeyondSiteMax()
    {
        var repository = new FakeApplicationRepository { SiteExtraRate = 85m };
        var paymentRepository = new FakePaymentRepository();
        var service = CreateService(repository, paymentRepository, new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CK",
            CheckNum = "1001",
            Works = new List<SubmitWorkRequest>
            {
                new()
                {
                    WorkCategory = "inv",
                    WorkType = "contam",
                    WorkFeeRate = 445m,
                    WorkFeeUnit = "site",
                    WorkSiteMax = 4,
                    // 6 wells: flat 445 covers 4, then 2 extra × 85 = 170. Total 615.
                    Specs = new List<SubmitWorkSpecRequest>
                    {
                        new() { OwnerWellNum = "W-1", DrillCount = 6 }
                    }
                }
            }
        });

        var work = repository.Created!.Works[0];
        Assert.Equal(85m, work.WorkSiteExtraRate);
        Assert.Equal(4, work.WorkSiteMax);
        Assert.Equal(615m, paymentRepository.Saved!.AuthAmount);
    }

    [Fact]
    public async Task SubmitAsync_VaultsCardForCreditCardAndLeavesStatusPending()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CC",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.Equal(1, gateway.VaultCalls);
        Assert.NotNull(paymentRepository.Saved);
        Assert.Equal("PEND", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.Equal(125m, paymentRepository.Saved.AuthAmount);
        // Vaulted custid is captured for later encryption into AUTH_ID_ENCR.
        Assert.False(string.IsNullOrWhiteSpace(paymentRepository.Saved.AuthIdEncr));
    }

    [Fact]
    public async Task SubmitAsync_DoesNotVaultForCheckPayment()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CHECK",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.Equal(0, gateway.VaultCalls);
        Assert.Equal("PENDP", paymentRepository.Saved!.StatusCode);
        Assert.Null(paymentRepository.Saved.AuthIdEncr);
    }

    [Fact]
    public async Task SubmitAsync_CheckWithCheckNumberIsPending()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CHECK",
            CheckNum = "10456",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.Equal(0, gateway.VaultCalls);
        Assert.Equal("PEND", paymentRepository.Saved!.StatusCode);
        Assert.Equal("10456", paymentRepository.Saved.CheckNum);
    }

    [Fact]
    public async Task SubmitAsync_ExemptPaymentIsExemptWithZeroPaid()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "EXMPT",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.Equal(0, gateway.VaultCalls);
        Assert.Equal("EXMPT", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.Null(paymentRepository.Saved.AuthIdEncr);
    }

    [Fact]
    public async Task SubmitAsync_CashPaymentIsPendingPayment()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CASH",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        // A public cash application collects no amount/payer up front, so it stays "Pending Payment"
        // (PENDP) until intra staff record the received cash, which advances it to PEND.
        Assert.Equal(0, gateway.VaultCalls);
        Assert.Equal("PENDP", paymentRepository.Saved!.StatusCode);
        Assert.Equal(0m, paymentRepository.Saved.PaidAmount);
        Assert.Null(paymentRepository.Saved.AuthIdEncr);
    }

    [Fact]
    public async Task SubmitAsync_PersistsSelectedInspectionSlot()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        var inspectionDate = new DateTime(2026, 8, 10);
        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CC",
            InspectionDate = inspectionDate,
            InspectionSlotId = 2,
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 125m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.NotNull(inspectionRepository.Added);
        Assert.Equal(inspectionDate, inspectionRepository.Added!.InspectionDate);
        Assert.Equal(2, inspectionRepository.Added.SlotId);
        Assert.Equal("IRSRV", inspectionRepository.Added.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_MapsHazardAndDocumentsIntoPersistedApplication()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CHECK",
            SiteHazardRequired = "Y",
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 100m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            },
            Hazard = new SubmitHazardRequest
            {
                Present = true,
                ConsultantFirstName = "Sam",
                ConsultantLastName = "Ng",
                ConsultantPhone = "510-555-1000",
                SafetyOfficerFirstName = "Pat",
                SafetyOfficerLastName = "Kim",
                FacilityType = "Gas station",
                SiteSafetyMeetingDate = new DateTime(2026, 8, 4),
                SiteSafetyMeetingTime = "14:30",
                Contaminants = new List<string> { "Benzene", "Lead", "Benzene" },
                Substances = new List<SubmitHazardSubstanceRequest>
                {
                    new() { Concentration = "5", PelPpm = "1", HealthEffects = "Carcinogen" }
                }
            },
            Documents = new List<SubmitDocumentRequest>
            {
                new() { FileName = "sitemap.pdf", FileSize = 1024, ContentType = "application/pdf", DocumentType = "SITEMAP" }
            },
            EmailCcs = new List<string> { "cc@example.com", "" },
            Notes = new List<string> { "Handle with care" }
        });

        var saved = repository.Created;
        Assert.NotNull(saved);
        Assert.NotNull(saved!.Hazard);
        Assert.Equal("Sam", saved.Hazard!.ConsultantFirstName);
        Assert.Equal("Kim", saved.Hazard.SafetyOfficerLastName);
        Assert.Equal(new DateTime(2026, 8, 4, 14, 30, 0), saved.Hazard.SiteSafetyMeetingDateTime);
        Assert.Equal(2, saved.Hazard.Contaminations.Count); // deduped
        Assert.Single(saved.Hazard.Substances);
        Assert.Equal(1, saved.Hazard.Substances[0].HazardSeq);
        Assert.Equal("Carcinogen", saved.Hazard.Substances[0].HealthEffects);

        Assert.Single(saved.Documents);
        Assert.Equal("sitemap.pdf", saved.Documents[0].DocumentFilename);
        Assert.Equal("SITEMAP", saved.Documents[0].DocumentType);
        Assert.Equal(1, saved.Documents[0].SeqNum);

        Assert.Single(saved.EmailCcs); // blank skipped
        Assert.Equal("cc@example.com", saved.EmailCcs[0].EmailAddrCc);
        Assert.Single(saved.Notes);
        Assert.Equal("Handle with care", saved.Notes[0].NotesText);
    }

    [Fact]
    public async Task SubmitAsync_OmitsHazardWhenNotPresent()
    {
        var repository = new FakeApplicationRepository();
        var paymentRepository = new FakePaymentRepository();
        var inspectionRepository = new FakeInspectionRepository();
        var gateway = new FakeIntelliPayGateway();
        var service = CreateService(repository, paymentRepository, inspectionRepository, gateway);

        await service.SubmitAsync(new SubmitApplicationRequest
        {
            PaymentType = "CC",
            Hazard = new SubmitHazardRequest { Present = false, ConsultantFirstName = "Ignored" },
            Works = new List<SubmitWorkRequest>
            {
                new() { WorkCategory = "con", WorkType = "conwell", WorkFeeRate = 100m, WorkFeeUnit = "EA", Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1" } } }
            }
        });

        Assert.Null(repository.Created!.Hazard);
        Assert.Empty(repository.Created.Documents);
    }

    [Fact]
    public async Task UpdateProjectInfoAsync_ForwardsRequestAndActingUserToRepository()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateProjectInfoAsync("1234567890123", new UpdateProjectInfoRequest
        {
            SiteLocation = "500 Elm St",
            SiteCityCode = "OAK",
            OwnerFirstName = "Ada",
            OwnerLastName = "Lovelace"
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.ProjectUpdates);
        Assert.Equal("1234567890123", captured.AppId);
        Assert.Equal("500 Elm St", captured.Request.SiteLocation);
        Assert.Equal("Ada", captured.Request.OwnerFirstName);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task UpdateApplicantInfoAsync_ForwardsRequestAndActingUserToRepository()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateApplicantInfoAsync("1234567890123", new UpdateApplicantInfoRequest
        {
            AppBusinessName = "Acme Wells",
            AppEmailAddr = "ops@acme.test",
            ContactPhone = "510-555-2000",
            EmailCcs = new[] { "cc1@acme.test", "  ", "cc2@acme.test" }
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.ApplicantUpdates);
        Assert.Equal("Acme Wells", captured.Request.AppBusinessName);
        // Phone is normalized to digits-only before persistence (legacy Java re-formats by fixed offsets).
        Assert.Equal("5105552000", captured.Request.ContactPhone);
        Assert.Equal(new[] { "cc1@acme.test", "  ", "cc2@acme.test" }, captured.Request.EmailCcs);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task UpdateExtensionAsync_ForwardsDatesAndActingUserToRepository()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateExtensionAsync("1234567890123", new UpdateExtensionRequest
        {
            ExtensionStartDate = new DateTime(2024, 6, 1),
            ExtensionEndDate = new DateTime(2024, 9, 1),
            IsEdit = false
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.Extensions);
        Assert.Equal("1234567890123", captured.AppId);
        Assert.Equal(new DateTime(2024, 6, 1), captured.StartDate);
        Assert.Equal(new DateTime(2024, 9, 1), captured.EndDate);
        Assert.False(captured.IsEdit);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task UpdateExtensionAsync_ThrowsWhenEndBeforeStart()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateExtensionAsync("1234567890123", new UpdateExtensionRequest
        {
            ExtensionStartDate = new DateTime(2024, 9, 1),
            ExtensionEndDate = new DateTime(2024, 6, 1)
        }, "staff@acgov.org"));

        Assert.Empty(repository.Extensions);
    }

    [Fact]
    public async Task UpdateExtensionAsync_ThrowsWhenStartMissing()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateExtensionAsync("1234567890123", new UpdateExtensionRequest
        {
            ExtensionEndDate = new DateTime(2024, 6, 1)
        }, "staff@acgov.org"));

        Assert.Empty(repository.Extensions);
    }

    [Fact]
    public async Task UpdateHazardAsync_ForwardsRequestAndActingUserToRepository()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateHazardAsync("1234567890123", new UpdateHazardRequest
        {
            ConsultantFirstName = "Sam",
            SafetyOfficerLastName = "Kim",
            FacilityType = "Gas station",
            PpeLevelA = "Y",
            EquipHardHatFlag = "R",
            InfoProvidedByPhone = "510-555-3000",
            Contaminants = new[] { "Gasoline", "Diesel" },
            Substances = new[] { new HazardSubstanceDto("100", "50", "Headache") }
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.HazardUpdates);
        Assert.Equal("Sam", captured.Request.ConsultantFirstName);
        Assert.Equal("Gas station", captured.Request.FacilityType);
        Assert.Equal("Y", captured.Request.PpeLevelA);
        Assert.Equal("R", captured.Request.EquipHardHatFlag);
        // Provider phone is normalized to digits-only like the other hazard phones.
        Assert.Equal("5105553000", captured.Request.InfoProvidedByPhone);
        Assert.Equal(new[] { "Gasoline", "Diesel" }, captured.Request.Contaminants);
        Assert.Equal("100", Assert.Single(captured.Request.Substances).Concentration);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task UpdateWorkAsync_ForwardsWorkIdRequestAndActingUserToRepository()
    {
        var repository = new FakeApplicationRepository();
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateWorkAsync("1234567890123", 7, new UpdateWorkRequest
        {
            DrillerName = "Drill Co",
            WorkFeeRate = 300m,
            WorkSiteMax = 4,
            Specs = new List<UpdateWorkSpecRequest>
            {
                new() { WorkSpecsId = 11, OwnerWellNum = "W-1", MaxDepthFt = 120m }
            }
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.WorkUpdates);
        Assert.Equal(7, captured.WorkId);
        Assert.Equal("Drill Co", captured.Request.DrillerName);
        Assert.Equal(300m, captured.Request.WorkFeeRate);
        Assert.Single(captured.Request.Specs);
        Assert.Equal(11, captured.Request.Specs[0].WorkSpecsId);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task AddWorkAsync_WhenPending_ForwardsToRepositoryAndReturnsRefreshedApplication()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", StatusCode = "PENDS", Applicant = new Applicant() }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.AddWorkAsync("1234567890123", new AddWorkRequest
        {
            WorkCategory = "con",
            WorkType = "conwell",
            WellUseType = "dom",
            DrillerName = "Drill Co",
            DrillerLicenseNum = "C57-123",
            DrillMethodType = "rot"
        }, "staff@acgov.org");

        Assert.NotNull(result);
        var captured = Assert.Single(repository.WorkAdds);
        Assert.Equal("con", captured.Request.WorkCategory);
        Assert.Equal("conwell", captured.Request.WorkType);
        Assert.Equal("dom", captured.Request.WellUseType);
        Assert.Equal("Drill Co", captured.Request.DrillerName);
        Assert.Equal("C57-123", captured.Request.DrillerLicenseNum);
        Assert.Equal("rot", captured.Request.DrillMethodType);
        Assert.Equal("staff@acgov.org", captured.AddedBy);
    }

    [Theory]
    [InlineData("APPRV")]
    [InlineData("CAN")]
    public async Task AddWorkAsync_WhenTerminal_ThrowsAndDoesNotForward(string status)
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", StatusCode = status, Applicant = new Applicant() }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddWorkAsync("1234567890123", new AddWorkRequest { WorkCategory = "con", WorkType = "conwell" }, "staff@acgov.org"));

        Assert.Empty(repository.WorkAdds);
    }

    [Fact]
    public async Task AddWorkAsync_WhenApplicationMissing_ReturnsNull()
    {
        var repository = new FakeApplicationRepository { ByIdResult = null };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.AddWorkAsync("1234567890123", new AddWorkRequest { WorkCategory = "con", WorkType = "conwell" }, "staff@acgov.org");

        Assert.Null(result);
        Assert.Empty(repository.WorkAdds);
    }

    [Fact]
    public async Task CancelWorkAsync_WhenPending_ForwardsToRepository()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", StatusCode = "PENDS", Applicant = new Applicant() }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.CancelWorkAsync("1234567890123", 3, "staff@acgov.org");

        Assert.NotNull(result);
        var captured = Assert.Single(repository.WorkCancellations);
        Assert.Equal(3, captured.WorkId);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Fact]
    public async Task DeleteWorkAsync_WhenMoreThanOneWork_ForwardsToRepository()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PENDS",
                Applicant = new Applicant(),
                Works = new List<ApplicationWork> { new() { WorkId = 1 }, new() { WorkId = 2 } }
            }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.DeleteWorkAsync("1234567890123", 2, "staff@acgov.org");

        Assert.NotNull(result);
        var deleted = Assert.Single(repository.WorkDeletions);
        Assert.Equal("1234567890123", deleted.AppId);
        Assert.Equal(2, deleted.WorkId);
    }

    [Fact]
    public async Task DeleteWorkAsync_WhenLastWork_ThrowsAndDoesNotForward()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PENDS",
                Applicant = new Applicant(),
                Works = new List<ApplicationWork> { new() { WorkId = 1 } }
            }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteWorkAsync("1234567890123", 1, "staff@acgov.org"));

        Assert.Empty(repository.WorkDeletions);
    }

    [Fact]
    public async Task UpdateWcrAsync_WhenApproved_ForwardsToRepository()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", StatusCode = "APPRV", Applicant = new Applicant() }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await service.UpdateWcrAsync("1234567890123", 7, new WcrUpdateRequest
        {
            DwrNumShared = true,
            Specs = new List<WcrSpecUpdate>
            {
                new() { WorkSpecsId = 11, StateWellId = "SW-1", ComplWellDwrNum = "WCR-1" }
            }
        }, "staff@acgov.org");

        var captured = Assert.Single(repository.WcrUpdates);
        Assert.Equal(7, captured.WorkId);
        Assert.True(captured.Request.DwrNumShared);
        Assert.Equal("SW-1", Assert.Single(captured.Request.Specs).StateWellId);
        Assert.Equal("staff@acgov.org", captured.UpdatedBy);
    }

    [Theory]
    [InlineData("PEND")]
    [InlineData("PAID")]
    public async Task UpdateWcrAsync_WhenNotApproved_ThrowsAndDoesNotForward(string status)
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", StatusCode = status, Applicant = new Applicant() }
        };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateWcrAsync("1234567890123", 7, new WcrUpdateRequest
        {
            Specs = new List<WcrSpecUpdate> { new() { WorkSpecsId = 11, StateWellId = "SW-1", ComplWellDwrNum = "WCR-1" } }
        }, "staff@acgov.org"));

        Assert.Empty(repository.WcrUpdates);
    }

    [Fact]
    public async Task CancelAsync_CancelsPendingApplicationAndSendsEmail()
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PEND",
                Applicant = new Applicant { AppEmailAddr = "applicant@example.com" }
            }
        };
        var payments = new FakePaymentRepository { Existing = new AppPayment { StatusCode = "PEND" } };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repository, payments, new FakeInspectionRepository(), new FakeIntelliPayGateway(), notifications);

        var result = await service.CancelAsync("1234567890123", "staff@acgov.org");

        Assert.NotNull(result);
        var cancel = Assert.Single(repository.Cancellations);
        Assert.Equal("1234567890123", cancel.AppId);
        Assert.Equal("staff@acgov.org", cancel.CancelledBy);
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal("cancelled", sent.Scenario);
        Assert.Equal("1234567890123", sent.AppId);
    }

    [Fact]
    public async Task CancelAsync_ReturnsNullWhenApplicationMissing()
    {
        var repository = new FakeApplicationRepository { ByIdResult = null };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.CancelAsync("9999999999999", "staff@acgov.org");

        Assert.Null(result);
        Assert.Empty(repository.Cancellations);
    }

    [Theory]
    [InlineData("APPRV", "PEND")]
    [InlineData("CAN", "PEND")]
    public async Task CancelAsync_ThrowsWhenApplicationStatusBlocksCancellation(string appStatus, string payStatus)
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1", StatusCode = appStatus, Applicant = new Applicant() }
        };
        var payments = new FakePaymentRepository { Existing = new AppPayment { StatusCode = payStatus } };
        var service = CreateService(repository, payments, new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync("1", "staff@acgov.org"));
        Assert.Empty(repository.Cancellations);
    }

    [Theory]
    [InlineData("PAID")]
    [InlineData("PAYFL")]
    public async Task CancelAsync_ThrowsWhenPaymentStatusBlocksCancellation(string payStatus)
    {
        var repository = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1", StatusCode = "PEND", Applicant = new Applicant() }
        };
        var payments = new FakePaymentRepository { Existing = new AppPayment { StatusCode = payStatus } };
        var service = CreateService(repository, payments, new FakeInspectionRepository(), new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync("1", "staff@acgov.org"));
        Assert.Empty(repository.Cancellations);
    }

    [Fact]
    public async Task GetPermitInfoAsync_DelegatesToRepository()
    {
        var expected = new PermitInfoDto("1", "staff@acgov.org", new DateTime(2026, 8, 1), new List<PermitLineDto>
        {
            new("4567890-01", 1, 1, new DateTime(2026, 8, 1), new DateTime(2027, 8, 1), "POPEN")
        });
        var repository = new FakeApplicationRepository { PermitInfoResult = expected };
        var service = CreateService(repository, new FakePaymentRepository(), new FakeInspectionRepository(), new FakeIntelliPayGateway());

        var result = await service.GetPermitInfoAsync("1");

        Assert.Same(expected, result);
    }
}
