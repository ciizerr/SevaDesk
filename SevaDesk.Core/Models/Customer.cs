namespace SevaDesk.Core.Models;

public class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = string.Empty; // e.g. CUST-0001
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? IdType { get; set; }
    public string? IdReference { get; set; }
    public string? Village { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "??";
            var parts = Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                1 => parts[0][0].ToString().ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            };
        }
    }

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Mobile)) parts.Add(Mobile);
            if (!string.IsNullOrWhiteSpace(Village)) parts.Add(Village);
            if (!string.IsNullOrWhiteSpace(Code)) parts.Add(Code);
            return string.Join(" · ", parts);
        }
    }
}
