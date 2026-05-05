namespace CourseInventory.Web.ViewModels;

public class AdminUsersViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public int TotalUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int OfflineUsers { get; set; }
    public int BlockedUsers { get; set; }
    public int LoginSessionsToday { get; set; }
    public string BusiestDayLabel { get; set; } = string.Empty;
    public AdminUserRowViewModel? LatestLoginUser { get; set; }
    public IReadOnlyList<AdminLoginActivityPointViewModel> LoginActivity { get; set; } = [];
    public IReadOnlyList<AdminUserRowViewModel> Users { get; set; } = [];
}

public class AdminLoginActivityPointViewModel
{
    public string Label { get; set; } = string.Empty;
    public string FullLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public IReadOnlyList<AdminLoginActivityEntryViewModel> Entries { get; set; } = [];
}

public class AdminLoginActivityEntryViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string TimeLabel { get; set; } = string.Empty;
}

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsOnline { get; set; }
    public bool IsCurrentUser { get; set; }
    public bool CanBlock { get; set; }
    public bool CanDelete { get; set; }
    public bool CanToggleAdmin { get; set; }
    public string? DisabledActionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
