using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Tests;

public sealed class PaymentServiceTests
{
    private static PaymentService CreateService(FakePaymentRepository repo, FakeIntelliPayGateway gateway, FakeApplicationRepository? appRepo = null, FakePermitNotificationService? notifications = null) =>
        new(repo, appRepo ?? new FakeApplicationRepository(), gateway, notifications ?? new FakePermitNotificationService(), NullLogger<PaymentService>.Instance);

    [Fact]
    public async Task PreAuthorizeAsync_Cc_SendsApplicationConfirmationAfterVault()
    {
        // Once the card is vaulted, the CC application is truly submitted — this is where the
        // applicant confirmation is sent (deferred out of submit so it never fires while the lightbox
        // popup is still open).
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application
            {
                AppId = "1234567890123",
                StatusCode = "PENDS",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" }
            }
        };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repo, new FakeIntelliPayGateway(), appRepo, notifications);

        await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "87654321",
            PaymentType = "CC",
            AuthorizedAmount = 660m
        });

        Assert.Contains(notifications.Sent, s => s.Scenario == "confirmation" && s.AppId == "1234567890123");
    }

    [Fact]
    public async Task PreAuthorizeAsync_StaffCardChange_DoesNotSendConfirmation()
    {
        // Intra staff "change card" re-vaults a new card for an already-submitted application. The
        // applicant was already confirmed at the original ecomm submission, so no confirmation email
        // must be re-sent (SendConfirmationEmail = false).
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application
            {
                AppId = "1234567890123",
                StatusCode = "PENDS",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" }
            }
        };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repo, new FakeIntelliPayGateway(), appRepo, notifications);

        await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "87654321",
            PaymentType = "CC",
            AuthorizedAmount = 660m,
            RequestedBy = "staff-portal",
            SendConfirmationEmail = false
        });

        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "confirmation");
    }

    [Fact]
    public async Task PreAuthorizeAsync_NoConfirmationWhenCustomerIdMissing()
    {
        var repo = new FakePaymentRepository();
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application { AppId = "1234567890123", StatusCode = "PENDS" }
        };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repo, new FakeIntelliPayGateway(), appRepo, notifications);

        await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "",
            PaymentType = "CC",
            AuthorizedAmount = 445m
        });

        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "confirmation");
    }

    [Fact]
    public async Task PreAuthorizeAsync_PreservesRealFeeCapturedAtSubmit()
    {
        // Submit stored the real permit fee in auth_amount; the $0 vault must not overwrite it,
        // matching legacy BeanAppPay so the intra app charges the correct amount later.
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "CC",
                AuthAmount = 660m,
                StatusCode = "PEND"
            }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        var result = await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "87654321",
            PaymentType = "CC",
            AuthorizedAmount = 0m
        });

        Assert.Equal(660m, repo.Saved!.AuthAmount);
        Assert.Equal(0m, repo.Saved.PaidAmount);
        Assert.Equal("PEND", repo.Saved.StatusCode);
        Assert.Equal("87654321", repo.Saved.AuthIdEncr);
        Assert.Equal(660m, result.AuthAmount);
    }

    [Fact]
    public async Task PreAuthorizeAsync_FallsBackToRequestedAmountWhenNoFeeOnRecord()
    {
        var repo = new FakePaymentRepository(); // no existing record
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "11112222",
            PaymentType = "CC",
            AuthorizedAmount = 445m
        });

        Assert.Equal(445m, repo.Saved!.AuthAmount);
        Assert.Equal("PEND", repo.Saved.StatusCode);
    }

    [Fact]
    public async Task PreAuthorizeAsync_WritesPciSafeVaultSnapshotToPaymentHistory()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.PreAuthorizeAsync(new PreAuthRequest
        {
            AppId = "1234567890123",
            CustomerId = "87654321",
            PaymentType = "CC",
            AuthorizedAmount = 660m,
            Response = new IntelliPayVaultSnapshot
            {
                Call = "SECTOK1",
                Status = "vaulted",
                AuthCode = "AC01",
                CardBrand = "VISA",
                Amount = "0.00"
            }
        });

        Assert.Single(repo.HistoryEntries);
        var entry = repo.HistoryEntries[0];
        Assert.Contains("\"custid\":\"87654321\"", entry);
        Assert.Contains("\"authcode\":\"AC01\"", entry);
        Assert.Contains("\"cardbrand\":\"VISA\"", entry);
        Assert.Contains("\"account\":\"1234567890123\"", entry);
        Assert.Contains("\"fee\":\"660.00\"", entry);
        // PCI fields must never be present.
        Assert.DoesNotContain("nonce", entry);
        Assert.DoesNotContain("hmac", entry);
    }

    [Fact]
    public async Task ChargeAsync_ExemptStaysZeroAndPaid()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "EXMPT",
                AuthAmount = 0m,
                StatusCode = "EXMPT"
            }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        var result = await service.ChargeAsync(new ChargeRequest
        {
            AppId = "1234567890123",
            CaptureAmount = 0m
        });

        Assert.Equal("PAID", result.StatusCode);
        Assert.Equal(0m, repo.Saved!.PaidAmount);
    }

    [Fact]
    public async Task ChargeAsync_CcApproved_CapturesIdentifiersReceiptAndHistory()
    {
        var repo = new FakePaymentRepository
        {
            NextReceipt = "WR2026-0042",
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 660m,
                StatusCode = "PEND"
            }
        };
        var gateway = new FakeIntelliPayGateway
        {
            ChargeResponse = "{\"response\":\"A\",\"paymentid\":\"PID777\",\"authcode\":\"AC12\",\"cardnumdisplay\":\"411111******1111\",\"nonce\":\"abc\"}"
        };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repo, gateway, notifications: notifications);

        var result = await service.ChargeAsync(new ChargeRequest { AppId = "1234567890123", CaptureAmount = 660m });

        Assert.Equal("PAID", result.StatusCode);
        Assert.Equal("PID777", repo.Saved!.PaymentId);
        Assert.Equal("AC12", repo.Saved.PnRefNum);
        Assert.Equal("WR2026-0042", repo.Saved.ReceiptNum);
        Assert.Equal(660m, repo.Saved.PaidAmount);
        Assert.Equal(660m, repo.Saved.AuthAmount); // real fee preserved, not overwritten
        Assert.NotNull(repo.Saved.PaidDate);
        // Two history entries appended (card_payment + payment_details), PCI fields stripped.
        Assert.Equal(2, repo.HistoryEntries.Count);
        Assert.Contains("\"source\":\"card_payment\"", repo.HistoryEntries[0]);
        Assert.DoesNotContain("cardnumdisplay", repo.HistoryEntries[0]);
        Assert.DoesNotContain("nonce", repo.HistoryEntries[0]);
        Assert.Contains("\"source\":\"payment_details\"", repo.HistoryEntries[1]);
        Assert.Equal("PID777", gateway.LastReadPaymentId);
        // A successful/approved CC charge must NOT send an audit email.
        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "ccChargeApprovedAudit");
    }

    [Fact]
    public async Task ChargeAsync_CcDeclined_SetsPayflAndThrowsWithoutReceipt()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 660m,
                StatusCode = "PEND"
            }
        };
        var gateway = new FakeIntelliPayGateway
        {
            ChargeResponse = "{\"response\":\"D\",\"declinereason\":\"Insufficient funds\"}"
        };
        var notifications = new FakePermitNotificationService();
        var service = CreateService(repo, gateway, notifications: notifications);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChargeAsync(new ChargeRequest { AppId = "1234567890123", CaptureAmount = 660m }));

        Assert.Equal("PAYFL", repo.Saved!.StatusCode);
        Assert.Null(repo.Saved.ReceiptNum);
        Assert.Empty(repo.HistoryEntries);
        // A declined/failed charge IS audit-emailed (payment error).
        Assert.Contains(notifications.Sent, s => s.Scenario == "ccChargeFailedAudit");
    }

    [Fact]
    public async Task ChargeAsync_CcApproved_RecalculatesFeeByWellCountAndAddsFine()
    {
        // Two wells at $660/well, plus a $50 fine — the charge must be the live recalculated total
        // (660 × 2 + 50 = 1370), NOT the stale stored auth_amount of 660. auth_amount self-heals to
        // the recomputed base (1320) and fine_amount is persisted even though no separate "Update
        // Payment" call was made.
        var repo = new FakePaymentRepository
        {
            NextReceipt = "WR2026-0054",
            Existing = new AppPayment
            {
                AppId = "1786471521951",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 660m,       // stale: was stored for 1 well
                FineAmount = 0m,
                StatusCode = "PEND"
            }
        };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application
            {
                AppId = "1786471521951",
                StatusCode = "PEND",
                Works =
                {
                    new ApplicationWork
                    {
                        WorkId = 1,
                        WorkFeeRate = 660m,
                        WorkFeeUnit = "well",
                        StatusCode = "PEND",
                        Specs =
                        {
                            new ApplicationWorkSpec { WorkSpecsId = 1, DrillCount = 1, StatusCode = "PEND" },
                            new ApplicationWorkSpec { WorkSpecsId = 2, DrillCount = 1, StatusCode = "PEND" }
                        }
                    }
                }
            }
        };
        var gateway = new FakeIntelliPayGateway
        {
            ChargeResponse = "{\"response\":\"A\",\"paymentid\":\"PID778\",\"authcode\":\"AC13\"}"
        };
        var service = CreateService(repo, gateway, appRepo);

        var result = await service.ChargeAsync(new ChargeRequest
        {
            AppId = "1786471521951",
            CaptureAmount = 660m, // stale client value — must be ignored in favour of the recalc
            FineAmount = 50m
        });

        Assert.Equal(1370m, gateway.LastChargeAmount); // charged the recalculated total
        Assert.Equal(1370m, repo.Saved!.PaidAmount);
        Assert.Equal(1320m, repo.Saved.AuthAmount);    // recomputed base fee (2 wells)
        Assert.Equal(50m, repo.Saved.FineAmount);      // fine persisted
        Assert.Equal("PAID", result.StatusCode);
        Assert.Equal("WR2026-0054", repo.Saved.ReceiptNum);
    }

    [Fact]
    public async Task ChargeAsync_UsesStoredFineWhenRequestOmitsIt()
    {
        // One well at $445, existing fine $25 already saved, request omits the fine (null) — the
        // stored fine is retained and folded into the charge (445 + 25 = 470).
        var repo = new FakePaymentRepository
        {
            NextReceipt = "WR2026-0055",
            Existing = new AppPayment
            {
                AppId = "1786471521951",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 445m,
                FineAmount = 25m,
                StatusCode = "PEND"
            }
        };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application
            {
                AppId = "1786471521951",
                StatusCode = "PEND",
                Works =
                {
                    new ApplicationWork
                    {
                        WorkId = 1,
                        WorkFeeRate = 445m,
                        WorkFeeUnit = "well",
                        StatusCode = "PEND",
                        Specs = { new ApplicationWorkSpec { WorkSpecsId = 1, DrillCount = 1, StatusCode = "PEND" } }
                    }
                }
            }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway(), appRepo);

        await service.ChargeAsync(new ChargeRequest { AppId = "1786471521951", CaptureAmount = 445m });

        Assert.Equal(470m, repo.Saved!.PaidAmount);
        Assert.Equal(25m, repo.Saved.FineAmount);
        Assert.Equal(445m, repo.Saved.AuthAmount);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UpdatesMethodAndFinePreservingVaultAndAmount()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 660m,
                StatusCode = "PEND"
            }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        var result = await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", CheckNum = "10231", AcctName = "John Doe", PaidAmount = 785.50m, FineAmount = 125.50m },
            "staff@acgov.org");

        Assert.Equal("CHECK", repo.Saved!.PaymentType);
        Assert.Equal(125.50m, repo.Saved.FineAmount);
        Assert.Equal("87654321", repo.Saved.AuthIdEncr);   // vault preserved
        Assert.Equal(660m, repo.Saved.AuthAmount);          // real fee preserved
        Assert.Equal("staff@acgov.org", repo.Saved.AddBy);  // audit actor recorded
        Assert.Equal("CHECK", result.PaymentType);
    }

    [Fact]
    public async Task UpdateDetailsAsync_CreatesMinimalRecordWhenNoneExists()
    {
        var repo = new FakePaymentRepository(); // no existing record
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "EXMPT", FineAmount = null },
            "staff@acgov.org");

        Assert.Equal("EXMPT", repo.Saved!.PaymentType);
        Assert.Equal(0m, repo.Saved.FineAmount);
        Assert.Equal("staff@acgov.org", repo.Saved.AddBy);
    }

    [Fact]
    public async Task UpdateDetailsAsync_RejectsChangeAfterApproval()
    {
        // Once the application is approved the payment is locked: the update must be refused and the
        // stored payment record left untouched.
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment
            {
                AppId = "1234567890123",
                PaymentType = "CC",
                AuthIdEncr = "87654321",
                AuthAmount = 660m,
                StatusCode = "PEND"
            }
        };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new PWA.PermitsApi.Domain.Models.Application { AppId = "1234567890123", StatusCode = "APPRV" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway(), appRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", FineAmount = 10m },
            "staff@acgov.org"));

        Assert.Null(repo.Saved); // nothing persisted
    }

    [Fact]
    public async Task UpdateDetailsAsync_Check_WithDetails_MarksPendAndCapturesFields()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        var result = await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", CheckNum = "10231", AcctName = "John Doe", PaidAmount = 660m, FineAmount = 0m },
            "staff@acgov.org");

        Assert.Equal("CHECK", repo.Saved!.PaymentType);
        Assert.Equal("10231", repo.Saved.CheckNum);
        Assert.Equal("John Doe", repo.Saved.AcctName);
        Assert.Equal(660m, repo.Saved.PaidAmount);
        Assert.Equal("PEND", repo.Saved.StatusCode);   // check received
        Assert.Equal("John Doe", result.AcctName);
    }

    [Fact]
    public async Task UpdateDetailsAsync_Check_MissingCheckNumber_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        // Check Number Received is mandatory for a check payment (legacy intra form parity).
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", AcctName = "John Doe", PaidAmount = 660m, FineAmount = 0m },
            "staff@acgov.org"));

        Assert.Null(repo.Saved); // rejected before persist
    }

    [Fact]
    public async Task UpdateDetailsAsync_Cash_MarksPendAndCapturesPayer()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CASH", AcctName = "Jane Payer", PaidAmount = 660m, FineAmount = 0m },
            "staff@acgov.org");

        Assert.Equal("CASH", repo.Saved!.PaymentType);
        Assert.Equal("Jane Payer", repo.Saved.AcctName);
        Assert.Equal(660m, repo.Saved.PaidAmount);
        Assert.Null(repo.Saved.CheckNum);
        Assert.Equal("PEND", repo.Saved.StatusCode);
    }

    [Fact]
    public async Task UpdateDetailsAsync_Cash_AddsFineToAmountDue()
    {
        // Due = base (660) + fine (40) = 700; the received amount must match the fine-inclusive total.
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CASH", AcctName = "Jane Payer", PaidAmount = 700m, FineAmount = 40m },
            "staff@acgov.org");

        Assert.Equal(700m, repo.Saved!.PaidAmount);
        Assert.Equal(40m, repo.Saved.FineAmount);
        Assert.Equal("PEND", repo.Saved.StatusCode);
    }

    [Fact]
    public async Task UpdateDetailsAsync_Check_AmountMismatch_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", CheckNum = "10231", AcctName = "John Doe", PaidAmount = 500m },
            "staff@acgov.org"));

        Assert.Null(repo.Saved); // rejected before persist
    }

    [Fact]
    public async Task UpdateDetailsAsync_Check_NumberWithoutName_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", CheckNum = "10231", AcctName = "", PaidAmount = 660m },
            "staff@acgov.org"));
    }

    [Fact]
    public async Task UpdateDetailsAsync_Check_MissingAmount_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        // Amount Received is mandatory for a check payment (legacy parity).
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CHECK", CheckNum = "10231", AcctName = "John Doe" },
            "staff@acgov.org"));

        Assert.Null(repo.Saved); // rejected before persist
    }

    [Fact]
    public async Task UpdateDetailsAsync_Cash_MissingAmount_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        // Amount Received is mandatory for a cash payment (legacy parity).
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CASH", AcctName = "Jane Payer" },
            "staff@acgov.org"));

        Assert.Null(repo.Saved); // rejected before persist
    }

    [Fact]
    public async Task UpdateDetailsAsync_Cash_MissingPayer_Throws()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "CASH", AcctName = "", PaidAmount = 660m },
            "staff@acgov.org"));
    }

    [Fact]
    public async Task UpdateDetailsAsync_Exempt_ClearsDetailAndZeroesPaid()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CHECK", CheckNum = "10231", AcctName = "John Doe", PaidAmount = 660m, AuthAmount = 660m, StatusCode = "PEND" }
        };
        var service = CreateService(repo, new FakeIntelliPayGateway());

        await service.UpdateDetailsAsync("1234567890123",
            new UpdatePaymentRequest { PaymentType = "EXMPT", FineAmount = 0m },
            "staff@acgov.org");

        Assert.Equal("EXMPT", repo.Saved!.PaymentType);
        Assert.Equal("EXMPT", repo.Saved.StatusCode);
        Assert.Equal(0m, repo.Saved.PaidAmount);
        Assert.Null(repo.Saved.CheckNum);
        Assert.Null(repo.Saved.AcctName);
    }
}
