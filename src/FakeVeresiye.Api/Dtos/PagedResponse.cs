namespace FakeVeresiye.Api.Dtos;

/// <summary>A single page of <typeparamref name="T"/> plus the totals needed to render a pager.</summary>
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Common paging inputs, clamped to sane bounds.</summary>
public readonly record struct PageRequest(int Page, int PageSize)
{
    public static PageRequest From(int page, int pageSize) =>
        new(Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100));

    public int Skip => (Page - 1) * PageSize;
}
