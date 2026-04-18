namespace CoffeeApi.DTOs;

public class ExcludedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CreateExcludedDayDto
{
    /// <summary>Local date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
