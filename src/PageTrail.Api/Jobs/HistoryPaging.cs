namespace PageTrail.Api.Jobs;

public static class HistoryPaging
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public const int MaxPage = int.MaxValue / MaxPageSize;

    public static Dictionary<string, string[]> Validate(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > MaxPage)
        {
            errors["page"] = [$"page must be between 1 and {MaxPage}."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        return errors;
    }
}
