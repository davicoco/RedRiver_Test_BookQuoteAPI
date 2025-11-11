namespace BookQuoteAPI.Models;

public class Quote
{
    public int Id { get; set; }
    public string quote { get; set; } = string.Empty;
    public string author { get; set; } = string.Empty;
}