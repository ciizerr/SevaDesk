using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IPaymentRepository
{
    Task<Payment> RecordPaymentAsync(Payment payment);
    Task<IEnumerable<Payment>> GetRecentPaymentsAsync(int limit = 50);
    Task<string> GenerateNextInvoiceNoAsync();
    Task<(decimal TotalSales, decimal CashTotal, decimal UpiTotal)> GetTodaySalesSummaryAsync();
}
