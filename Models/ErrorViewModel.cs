namespace CandyShop.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public int? StatusCode { get; set; }

    public string Message { get; set; } = "Something went wrong while processing your request.";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
