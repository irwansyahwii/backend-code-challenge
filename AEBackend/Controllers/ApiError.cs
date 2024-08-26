namespace AEBackend.Controllers;

public class ApiError
{
  public ApiError(string message)
  {
    Message = message;
  }

  public string Message { get; }

  public static ApiError None => new(string.Empty);

  public static implicit operator ApiError(string message) => new(message);

  public static implicit operator string(ApiError error) => error.Message;
}