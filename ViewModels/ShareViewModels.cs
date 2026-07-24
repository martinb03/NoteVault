namespace NoteVault.ViewModels;

// --- GET response: current sharing state for the modal ---

public class ShareModalViewModel
{
    public string OwnerName { get; set; } = string.Empty;
    public bool IsCurrentUserOwner { get; set; }
    public string CurrentUserPermission { get; set; } = string.Empty;
    public List<ShareEntryViewModel> Shares { get; set; } = new();
    public string OwnerUserId { get; set; } = string.Empty;
}

public class ShareEntryViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public string SharedByUserId { get; set; } = string.Empty;
    public string SharedByName { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public int DownstreamCount { get; set; }
}

// --- POST request: atomic update from the modal ---

public class UpdateSharesRequest
{
    public int ResourceId { get; set; }
    public List<ShareAddRequest> Adds { get; set; } = new();
    public List<ShareUpdateRequest> Updates { get; set; } = new();
    public List<ShareRemovalRequest> Removals { get; set; } = new();
}

public class ShareAddRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

public class ShareUpdateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

public class ShareRemovalRequest
{
    public string UserId { get; set; } = string.Empty;
    public bool CascadeDownstream { get; set; }
}

// --- User search result for the type-ahead ---

public class UserSearchResultViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ExistingPermission { get; set; }
}