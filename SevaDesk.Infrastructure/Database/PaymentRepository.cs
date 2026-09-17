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

    public Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetTodaySalesSummaryAsync()
    {
        var startOfToday = DateTime.Today;
        var endOfToday = DateTime.Today.AddDays(1).AddTicks(-1);
        return GetEarningsSummaryAsync(startOfToday, endOfToday);
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime from, DateTime to, string? paymentMethod = null)
    {
        var fromUtc = from.Kind == DateTimeKind.Utc ? from : from.ToUniversalTime();
        var toUtc = to.Kind == DateTimeKind.Utc ? to : to.ToUniversalTime();
        var fromStr = fromUtc.ToString("o");
        var toStr = toUtc.ToString("o");

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
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
            WHERE payment_date >= @FromStr AND payment_date <= @ToStr";

        if (!string.IsNullOrWhiteSpace(paymentMethod) && !paymentMethod.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND UPPER(payment_method) = UPPER(@PaymentMethod)";
        }

        sql += " ORDER BY payment_date DESC;";

        var rows = await connection.QueryAsync(sql, new { FromStr = fromStr, ToStr = toStr, PaymentMethod = paymentMethod });
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

    public async Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetEarningsSummaryAsync(DateTime from, DateTime to, string? paymentMethod = null)
    {
        var fromUtc = from.Kind == DateTimeKind.Utc ? from : from.ToUniversalTime();
        var toUtc = to.Kind == DateTimeKind.Utc ? to : to.ToUniversalTime();
        var fromStr = fromUtc.ToString("o");
        var toStr = toUtc.ToString("o");

        using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                COALESCE(SUM(amount), 0) AS TotalSales,
                COALESCE(SUM(CASE WHEN UPPER(payment_method) = 'CASH' THEN amount ELSE 0 END), 0) AS CashTotal,
                COALESCE(SUM(CASE WHEN UPPER(payment_method) = 'UPI' THEN amount ELSE 0 END), 0) AS UpiTotal
            FROM payments
            WHERE payment_date >= @FromStr AND payment_date <= @ToStr";

        if (!string.IsNullOrWhiteSpace(paymentMethod) && !paymentMethod.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND UPPER(payment_method) = UPPER(@PaymentMethod)";
        }

        var row = await connection.QuerySingleOrDefaultAsync(sql, new { FromStr = fromStr, ToStr = toStr, PaymentMethod = paymentMethod });
        if (row != null)
        {
            decimal total = (decimal)(double)row.TotalSales;
            decimal cash = (decimal)(double)row.CashTotal;
            decimal upi = (decimal)(double)row.UpiTotal;
            return (total, cash, upi);
        }

        return (0, 0, 0);
    }

    public async Task<IEnumerable<(DateTime Date, decimal Total, decimal Cash, decimal Upi)>> GetDailyEarningsAsync(DateTime from, DateTime to)
    {
        var payments = await GetPaymentsByDateRangeAsync(from, to);
        var grouped = payments
            .GroupBy(p => p.PaymentDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => (
                Date: g.Key,
                Total: g.Sum(p => p.Amount),
                Cash: g.Sum(p => string.Equals(p.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) ? p.Amount : 0),
                Upi: g.Sum(p => string.Equals(p.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase) ? p.Amount : 0)
            ))
            .ToList();

        return grouped;
    }

    public async Task<IEnumerable<(int Month, decimal Total, decimal Cash, decimal Upi)>> GetMonthlyEarningsAsync(int year)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var to = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Local);
        var payments = await GetPaymentsByDateRangeAsync(from, to);

        var grouped = payments
            .GroupBy(p => p.PaymentDate.Month)
            .OrderBy(g => g.Key)
            .Select(g => (
                Month: g.Key,
                Total: g.Sum(p => p.Amount),
                Cash: g.Sum(p => string.Equals(p.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) ? p.Amount : 0),
                Upi: g.Sum(p => string.Equals(p.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase) ? p.Amount : 0)
            ))
            .ToList();

        return grouped;
    }

    public async Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(string customerId)
    {
        using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        
        const string sql = @"
            SELECT * FROM payments 
            WHERE customer_id = @CustomerId 
            ORDER BY payment_date DESC";

        return await connection.QueryAsync<Payment>(sql, new { CustomerId = customerId });
    }
}
