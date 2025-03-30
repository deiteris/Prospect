using System.Text.Json;
using System.Text.Json.Serialization;
using Prospect.Server.Api.Services.Auth.Extensions;
using Prospect.Server.Api.Services.CloudScript;
using Prospect.Server.Api.Services.CloudScript.Models;
using Prospect.Server.Api.Services.UserData;

// Closed Beta Function

public class CompleteInventoryUpdateRequest
{
    [JsonPropertyName("userId")]
    public string UserID { get; set; }
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
    [JsonPropertyName("newSet")]
    public LoadoutData NewSet { get; set; }
    [JsonPropertyName("itemsToAdd")]
    public FYCustomItemInfo[] ItemsToAdd { get; set; }
    [JsonPropertyName("itemsToUpdateAmount")]
    public FYCustomItemInfo[] ItemsToUpdateAmount { get; set; }
    [JsonPropertyName("itemsToRemove")]
    public HashSet<string> ItemsToRemove { get; set; }
}

public class CompleteInventoryUpdateResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    [JsonPropertyName("error")]
    public string Error { get; set; }
    [JsonPropertyName("newSet")]
    public LoadoutData NewSet { get; set; }
    [JsonPropertyName("itemsToAdd")]
    public FYCustomItemInfo[] ItemsToAdd { get; set; }
    [JsonPropertyName("itemsToUpdateAmount")]
    public FYCustomItemInfo[] ItemsToUpdateAmount { get; set; }
    [JsonPropertyName("itemsToRemove")]
    public HashSet<string> ItemsToRemove { get; set; }
}

[CloudScriptFunction("CompleteInventoryUpdate")]
public class CompleteInventoryUpdateFunction : ICloudScriptFunction<CompleteInventoryUpdateRequest, CompleteInventoryUpdateResponse>
{
    private readonly ILogger<CompleteInventoryUpdateFunction> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserDataService _userDataService;

    public CompleteInventoryUpdateFunction(ILogger<CompleteInventoryUpdateFunction> logger, IHttpContextAccessor httpContextAccessor, UserDataService userDataService)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _userDataService = userDataService;
    }

    public async Task<CompleteInventoryUpdateResponse> ExecuteAsync(CompleteInventoryUpdateRequest request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new CloudScriptException("CloudScript was not called within a http request");
        }
        var userId = context.User.FindAuthUserId();
        var userData = await _userDataService.FindAsync(userId, userId, new List<string>{"Inventory"});
        var inventory = JsonSerializer.Deserialize<List<FYCustomItemInfo>>(userData["Inventory"].Value);

        // TODO: Optimize
        // TODO: Check deleted items to see if other stacks/mods were updated correctly
        // Process inventory update. The player may:
        // 1. Split item stack - creates a new item and updates amount of existing item.
        // 2. Stack existing items - deletes existing items and updates amount of existing item.
        // 3. Change vanity data - updates an existing item.
        // 4. Change weapon mods - updates an existing item.
        // IMPORTANT: The same request is currently used to report the updated inventory in the end of match.
        var newInventory = new List<FYCustomItemInfo>(inventory.Count);
        foreach (var item in inventory) {
            if (!request.ItemsToRemove.Contains(item.ItemId)) {
                newInventory.Add(item);
            }
        }

        foreach (var item in request.ItemsToUpdateAmount) {
            var inventoryItem = newInventory.Find(i => i.ItemId == item.ItemId);
            if (inventoryItem == null) {
                continue;
            }
            inventoryItem.Amount = item.Amount;
            inventoryItem.ModData = item.ModData;
            inventoryItem.PrimaryVanityId = item.PrimaryVanityId;
            inventoryItem.SecondaryVanityId = item.SecondaryVanityId;
            inventoryItem.Durability = item.Durability;
        }

        // TODO: Check updated items and validate new items
        foreach (var item in request.ItemsToAdd) {
            newInventory.Add(item);
        }

        await _userDataService.UpdateAsync(userId, userId, new Dictionary<string, string>{
            ["LOADOUT"] = JsonSerializer.Serialize(request.NewSet),
            ["Inventory"] = JsonSerializer.Serialize(newInventory),
        });

        return new CompleteInventoryUpdateResponse
        {
            UserId = userId,
            //Error = "",
            //NewSet = request.NewSet,
            //ItemsToAdd = request.ItemsToAdd,
            //ItemsToRemove = request.ItemsToRemove,
            //ItemsToUpdateAmount = request.ItemsToUpdateAmount,
        };
    }
}
