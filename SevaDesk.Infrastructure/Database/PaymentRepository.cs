using System.Globalization;
using Dapper;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.Database;

public class PaymentRepository : IPaymentRepository
{
    private readonly DatabaseInitializer _db;

    public PaymentRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<string> GenerateNextInvoiceNoAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var invoices = await connection.QueryAsync<string>("SELECT invoice_no FROM payments WHERE invoice_no LIKE 'INV-%'");
        int maxNum = 1000;

        foreach (var inv in invoices)
        {
            if (inv.StartsWith("INV-") && int.TryParse(inv[4..], out var n) && n > maxNum)
            {
                maxNum = n;
            }
        }

        return $"INV-{(maxNum + 1):D4}";
    }

    public async Task<Payment> RecordPaymentAsync(Payment payment)
    {
        if (string.IsNullOrWhiteSpace(payment.InvoiceNo))
        {
            payment.InvoiceNo = await GenerateNextInvoiceNoAsync();
        }

        if (string.IsNullOrWhiteSpace(payment.Id))
        {
            payment.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(payment.CustomerId))
        {
            payment.CustomerId = "walk-in";
        }

        payment.PaymentDate = DateTime.UtcNow;

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO payments (id, invoice_no, customer_id, customer_name, session_id, amount, payment_method, reference_number, payment_date, items_summary, notes)
            VALUES (@Id, @InvoiceNo, @CustomerId, @CustomerName, @SessionId, @Amount, @PaymentMethod, @ReferenceNumber, @PaymentDate, @ItemsSummary, @Notes);";

        await connection.ExecuteAsync(sql, new
        {
            payment.Id,
            payment.InvoiceNo,
            payment.CustomerId,
            payment.CustomerName,
            payment.SessionId,
            payment.Amount,
            payment.PaymentMethod,
            payment.ReferenceNumber,
            PaymentDate = payment.PaymentDate.ToString("o"),
            payment.ItemsSummary,
            payment.Notes
        });

        return payment;
    }

    public async Task<IEnumerable<Payment>> GetRecentPaymentsAsync(int limit = 50)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT 
                id AS Id,
                invoice_no AS InvoiceNo,
                customer_id AS CustomerId,
                customer_name AS CustomerName,
                session_id AS SessionId,
                amount AS Amount,
                payment_method AS PaymentMethod,
                reference_number AS ReferenceNumber,
                payment_date AS PaymentDate,
                items_summary AS ItemsSummary,
                notes AS Notes
            FROM payments
            ORDER BY payment_date DESC
            LIMIT @Limit;";

        var rows = await connection.QueryAsync(sql, new { Limit = limit });
        var list = new List<Payment>();

        foreach (var r in rows)
        {
            var p = new Payment
            {
                Id = r.Id,
                InvoiceNo = r.InvoiceNo ?? string.Empty,
                CustomerId = r.CustomerId,
                CustomerName = string.IsNullOrWhiteSpace((string?)r.CustomerName) ? "Walk-in Customer" : (string)r.CustomerName,
                SessionId = r.SessionId,
                Amount = (decimal)(double)r.Amount,
                PaymentMethod = r.PaymentMethod ?? "Cash",
                ReferenceNumber = r.ReferenceNumber,
                ItemsSummary = r.ItemsSummary,
                Notes = r.Notes
            };

            if (DateTime.TryParse((string?)r.PaymentDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                p.PaymentDate = dt.ToLocalTime();
            }

            list.Add(p);
        }

        return list;
    }

    public async Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetTodaySalesSummaryAsync()
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        // Check records created today (in local date)
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        const string sql = @"
            SELECT 
                COALESCE(SUM(amount), 0) AS TotalSales,
                COALESCE(SUM(CASE WHEN UPPER(payment_method) = 'CASH' THEN amount ELSE 0 END), 0) AS CashTotal,
                COALESCE(SUM(CASE WHEN UPPER(payment_method) = 'UPI' THEN amount ELSE 0 END), 0) AS UpiTotal
            FROM payments
            WHERE substr(payment_date, 1, 10) = @Today;";

        var row = await connection.QuerySingleOrDefaultAsync(sql, new { Today = today });
        if (row != null)
        {
            decimal total = (decimal)(double)row.TotalSales;
            decimal cash = (decimal)(double)row.CashTotal;
            decimal upi = (decimal)(double)row.UpiTotal;
            return (total, cash, upi);
        }

        return (0, 0, 0);
    }
}
