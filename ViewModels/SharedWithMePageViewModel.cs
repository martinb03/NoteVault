namespace NoteVault.ViewModels;

public class SharedWithMePageViewModel
{
    public List<SharedItemViewModel> Items { get; set; } = new();
 
    // Filter dropdown data
    public List<UserFilterOption> SharedByOptions { get; set; } = new();
    public List<UserFilterOption> OwnedByOptions { get; set; } = new();
}
 
public class SharedItemViewModel
{
    public string ItemType { get; set; } = string.Empty;  // "Note" or "Folder"
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string SharedByName { get; set; } = string.Empty;
    public string SharedByUserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }        // resource's last modification, for display
    public DateTime SharedAt { get; set; }         // share record's creation, for the "Date shared" filter
    public List<TagListViewModel> PrivateTags { get; set; } = new();  // recipient's own tags on this item
}
 
public class UserFilterOption
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
 
// ------ Request bodies ------
 
public class SaveCopyRequest
{
    public int NoteId { get; set; }
}
 
public class RemoveFromSharedRequest
{
    public string ItemType { get; set; } = string.Empty;  // "Note" or "Folder"
    public int ItemId { get; set; }
}