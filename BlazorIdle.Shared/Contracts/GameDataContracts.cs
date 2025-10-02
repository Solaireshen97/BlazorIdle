namespace BlazorIdle.Shared.Contracts;

public class CreateGameDataRequest
{
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
}

public class UpdateGameDataRequest
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
}

public class GameDataResponse
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}