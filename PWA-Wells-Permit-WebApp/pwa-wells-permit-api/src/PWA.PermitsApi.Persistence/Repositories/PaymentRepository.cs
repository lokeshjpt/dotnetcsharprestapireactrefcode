using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    // Legacy vault passphrase (see spec 3); the stored custid is salted with the appId.
    private const string Passphrase = "PWA Wells is map based";

    private readonly DapperContext _context;
    private readonly ILogger<PaymentRepository> _logger;
    private readonly string _schema;

    public PaymentRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions, ILogger<PaymentRepository> logger)
    {
        _context = context;
        _logger = logger;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<AppPayment?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT application_id AS AppId, RTRIM(payment_type) AS PaymentType,
       CONVERT(varchar(50), DecryptByPassPhrase(@Passphrase, AUTH_ID_ENCR, 1, CONVERT(varbinary(13), application_id))) AS AuthIdEncr,
       CONVERT(varchar(50), DecryptByPassPhrase(@Passphrase, payment_id, 1, CONVERT(varbinary(13), application_id))) AS PaymentId,
       CONVERT(varchar(50), DecryptByPassPhrase(@Passphrase, pn_ref_num_encr, 1, CONVERT(varbinary(13), application_id))) AS PnRefNum,
       CONVERT(varchar(max), DecryptByPassPhrase(@Passphrase, payment_history, 1, CONVERT(varbinary(13), application_id))) AS PaymentHistory,
       RTRIM(receipt_num) AS ReceiptNum, paid_date AS PaidDate,
       paid_amount AS PaidAmount, fine_amount AS FineAmount,
       service_charge AS ServiceCharge, auth_amount AS AuthAmount,
       check_num AS CheckNum,
       CONVERT(varchar(50), DecryptByPassPhrase(@Passphrase, acct_name_encr, 1, CONVERT(varbinary(13), application_id))) AS AcctName,
       RTRIM(status_code) AS StatusCode, add_by AS AddBy
FROM [{_schema}].[APP_PAYMENT_INFO]
WHERE application_id = @AppId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AppPayment>(new CommandDefinition(sql, new { AppId = appId, Passphrase }, cancellationToken: cancellationToken));
    }

    public async Task<AppPayment> UpsertAsync(AppPayment payment, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{_schema}].[APP_PAYMENT_INFO] WHERE application_id = @AppId;",
            new { payment.AppId }, cancellationToken: cancellationToken));

        var parameters = new
        {
            payment.AppId,
            payment.PaymentType,
            payment.AuthIdEncr,
            payment.PaymentId,
            payment.PnRefNum,
            payment.ReceiptNum,
            payment.PaidDate,
            payment.PaidAmount,
            payment.FineAmount,
            payment.ServiceCharge,
            payment.AuthAmount,
            payment.CheckNum,
            payment.AcctName,
            payment.StatusCode,
            payment.AddBy,
            Passphrase,
            Ts = DateTime.UtcNow
        };

        if (exists > 0)
        {
            var updateSql = $@"
UPDATE [{_schema}].[APP_PAYMENT_INFO]
SET payment_type = @PaymentType,
    AUTH_ID_ENCR = CASE WHEN @AuthIdEncr IS NULL THEN AUTH_ID_ENCR
                        ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @AuthIdEncr), 1, CONVERT(varbinary(13), application_id)) END,
    payment_id = CASE WHEN @PaymentId IS NULL THEN payment_id
                      WHEN @PaymentId = '' THEN NULL
                      ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @PaymentId), 1, CONVERT(varbinary(13), application_id)) END,
    pn_ref_num_encr = CASE WHEN @PnRefNum IS NULL THEN pn_ref_num_encr
                           WHEN @PnRefNum = '' THEN NULL
                           ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @PnRefNum), 1, CONVERT(varbinary(13), application_id)) END,
    receipt_num = COALESCE(@ReceiptNum, receipt_num),
    paid_date = COALESCE(@PaidDate, paid_date),
    paid_amount = @PaidAmount,
    fine_amount = @FineAmount,
    service_charge = @ServiceCharge,
    auth_amount = @AuthAmount,
    check_num = @CheckNum,
    acct_name_encr = CASE WHEN @AcctName IS NULL OR @AcctName = '' THEN NULL
                          ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @AcctName), 1, CONVERT(varbinary(13), application_id)) END,
    status_code = @StatusCode,
    update_by = @AddBy,
    update_ts = @Ts
WHERE application_id = @AppId;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, parameters, cancellationToken: cancellationToken));
        }
        else
        {
            var insertSql = $@"
INSERT INTO [{_schema}].[APP_PAYMENT_INFO]
    (application_id, payment_type, AUTH_ID_ENCR, payment_id, pn_ref_num_encr, receipt_num, paid_date,
     paid_amount, fine_amount, service_charge,
     auth_amount, check_num, acct_name_encr, status_code, add_by, add_ts)
VALUES
    (@AppId, @PaymentType,
     CASE WHEN @AuthIdEncr IS NULL THEN NULL
          ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @AuthIdEncr), 1, CONVERT(varbinary(13), CONVERT(varchar(13), @AppId))) END,
     CASE WHEN @PaymentId IS NULL OR @PaymentId = '' THEN NULL
          ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @PaymentId), 1, CONVERT(varbinary(13), CONVERT(varchar(13), @AppId))) END,
     CASE WHEN @PnRefNum IS NULL OR @PnRefNum = '' THEN NULL
          ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @PnRefNum), 1, CONVERT(varbinary(13), CONVERT(varchar(13), @AppId))) END,
     @ReceiptNum, @PaidDate,
     @PaidAmount, @FineAmount, @ServiceCharge,
     @AuthAmount, @CheckNum,
     CASE WHEN @AcctName IS NULL OR @AcctName = '' THEN NULL
          ELSE EncryptByPassPhrase(@Passphrase, CONVERT(varchar(50), @AcctName), 1, CONVERT(varbinary(13), CONVERT(varchar(13), @AppId))) END,
     @StatusCode, @AddBy, @Ts);";
            await connection.ExecuteAsync(new CommandDefinition(insertSql, parameters, cancellationToken: cancellationToken));
        }

        _logger.LogInformation("Upserted payment data for application {AppId}", payment.AppId);
        return payment;
    }

    public async Task<string> GetNextReceiptAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.Now.Year;
        var initReceipt = $"WR{year}-0001";
        var likePattern = $"WR{year}%";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var maxReceipt = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT MAX(receipt_num) FROM [{_schema}].[APP_PAYMENT_INFO] WHERE receipt_num LIKE @Like;",
            new { Like = likePattern }, cancellationToken: cancellationToken));

        var next = IncrementReceipt(maxReceipt);
        return string.IsNullOrEmpty(next) ? initReceipt : next;
    }

    // Mirrors legacy incrementReceiptNum: split on '-', increment the numeric part, zero-pad to 4.
    private static string IncrementReceipt(string? receiptNumber)
    {
        if (string.IsNullOrEmpty(receiptNumber))
        {
            return string.Empty;
        }

        var trimmed = receiptNumber.Trim();
        var indx = trimmed.IndexOf('-');
        if (indx <= 0 || indx >= trimmed.Length - 1)
        {
            return string.Empty;
        }

        var yrId = trimmed.Substring(0, indx + 1);
        if (!int.TryParse(trimmed.Substring(indx + 1), out var num))
        {
            return string.Empty;
        }

        num++;
        var pstr = "0000" + num;
        return yrId + pstr.Substring(pstr.Length - 4);
    }

    public async Task AppendPaymentHistoryAsync(string appId, string entryJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entryJson))
        {
            return;
        }

        // Append oldest-first: when payment_history is null start a new '[entry]' array; otherwise
        // strip the trailing ']' from the decrypted array and add ',entry]'. The whole plaintext is
        // CONVERTed to varchar(max) BEFORE encrypting so the ciphertext holds single-byte (ANSI)
        // bytes — the legacy Java writes varchar and the recon SELECT reads CONVERT(varchar(max), ...),
        // so an nvarchar plaintext (Dapper's default string binding) would decrypt as NUL-interleaved
        // garbage. Re-encrypt keyed on application_id. Mirrors legacy BeanAppPay.updatePaymentHistory.
        var decryptExpr = "CONVERT(varchar(max), DecryptByPassPhrase(@Passphrase, payment_history, 1, CONVERT(varbinary(13), application_id)))";
        var sql = $@"
UPDATE [{_schema}].[APP_PAYMENT_INFO]
SET payment_history = EncryptByPassPhrase(@Passphrase,
        CONVERT(varchar(max),
            CASE WHEN payment_history IS NULL THEN '[' + @Entry + ']'
                 ELSE SUBSTRING({decryptExpr}, 1, LEN({decryptExpr}) - 1) + ',' + @Entry + ']'
            END), 1, CONVERT(varbinary(13), application_id))
WHERE application_id = @AppId;";

        var parameters = new
        {
            AppId = appId,
            Entry = new DbString { Value = entryJson, IsAnsi = true, IsFixedLength = false, Length = -1 },
            Passphrase
        };

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
