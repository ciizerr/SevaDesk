using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IPaymentRepository
{
    Task<Payment> RecordPaymentAsync(Payment payment);
    Task<IEnumerable<Payment>> GetRecentPaymentsAsync(int limit = 50);
    Task<string> GenerateNextInvoiceNoAsync();
    Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(string customerId);
    Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetTodaySalesSummaryAsync();
    Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime from, DateTime to, string? paymentMethod = null);
    Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetEarningsSummaryAsync(DateTime from, DateTime to, string? paymentMethod = null);
    Task<IEnumerable<(DateTime Date, decimal Total, decimal Cash, decimal Upi)>> GetDailyEarningsAsync(DateTime from, DateTime to);
    Task<IEnumerable<(int Month, decimal Total, decimal Cash, decimal Upi)>> GetMonthlyEarningsAsync(int year);
}
